using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.UI;

internal enum SaveWorkspaceWorkflowStatus
{
    Cancelled,
    Saved,
    Failed
}

/// <summary>Result returned to a tray or Settings presenter after the shared save workflow.</summary>
internal sealed record SaveWorkspaceWorkflowResult(
    SaveWorkspaceWorkflowStatus Status,
    string WorkspaceName = "",
    int SelectedWindowCount = 0,
    WorkspaceCaptureResult? Capture = null);

/// <summary>
/// Owns the common Save Workspace UI and capture/persistence sequence. Callers remain responsible
/// only for context-specific success presentation, such as a tray balloon or Settings refresh.
/// </summary>
internal sealed class SaveWorkspaceWorkflow
{
    private readonly WorkspaceService _workspaceService;
    private readonly SettingsService? _settingsService;

    internal SaveWorkspaceWorkflow(
        WorkspaceService workspaceService,
        SettingsService? settingsService)
    {
        _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        _settingsService = settingsService;
    }

    internal async Task<SaveWorkspaceWorkflowResult> RunAsync(Window? owner = null)
    {
        List<(MonitorInfo? Monitor, List<WindowRecord> Windows)> windowPreview;
        IReadOnlyList<VirtualDesktopInfo> desktops = [];
        bool enumerationFailed = false;
        try
        {
            (windowPreview, desktops) = await Task.Run(() =>
                (_workspaceService.GetWindowPreviewForDialog(), VirtualDesktopCatalog.Read()));
        }
        catch (Exception ex)
        {
            AppLogger.Warn(
                "workspace.preview_enumeration_failed",
                "Could not enumerate windows for the save dialog",
                ex,
                LogField.Public("errorCategory", "window_enumeration"));
            windowPreview = [];
            enumerationFailed = true;
        }

        var dialog = new SaveWorkspaceDialog(windowPreview, _settingsService, desktops,
            _workspaceService.LastWindowCaptureDiagnostics, enumerationFailed);
        if (owner != null)
            dialog.Owner = owner;
        if (dialog.ShowDialog() != true)
            return new SaveWorkspaceWorkflowResult(SaveWorkspaceWorkflowStatus.Cancelled);

        // WPF-bound dialog properties must be read before capture work moves to background threads.
        string name = dialog.WorkspaceName;
        bool saveFiles = dialog.SaveFiles;
        List<WindowRecord> selectedWindows = dialog.SelectedWindows;

        ScreenPreviewCaptureResult screenPreview = new(-1, []);
        try
        {
            // 截图前让保存对话框和设置窗口退出画面，避免把本应用盖在工作区上。
            bool hideOwner = owner?.IsVisible == true;
            if (hideOwner) owner!.Hide();
            try
            {
                await Task.Delay(150);
                screenPreview = await Task.Run(_workspaceService.CaptureScreenPreviews);
            }
            finally
            {
                if (hideOwner) owner!.Show();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn(
                "workspace.screen_preview_capture_failed",
                "Could not capture screen previews",
                ex,
                LogField.Public("errorCategory", "screen_capture"));
        }

        SaveProgressWindow? progressWindow = null;
        if (saveFiles)
        {
            progressWindow = new SaveProgressWindow(name);
            if (owner != null)
                progressWindow.Owner = owner;
            progressWindow.Show();
        }

        IProgress<SaveProgressReport>? progress = progressWindow == null
            ? null
            : new Progress<SaveProgressReport>(progressWindow.ApplyReport);

        bool? ownerWasEnabled = owner?.IsEnabled;
        if (owner != null)
            owner.IsEnabled = false;

        try
        {
            WorkspaceCaptureResult capture = await _workspaceService.CaptureWorkspaceAsync(
                name,
                saveFiles: saveFiles,
                selectedWindows: selectedWindows,
                progress: progress);
            _workspaceService.PersistCapture(
                capture,
                WorkspaceArtifactKind.NamedWorkspace,
                IncompleteBrowserCapturePolicy.SavePartialWorkspace);
            bool previewComplete = false;
            try
            {
                _workspaceService.SaveScreenPreviews(capture.Snapshot, screenPreview.Images);
                previewComplete = screenPreview.ScreenCount > 0 &&
                    screenPreview.Images.Count == screenPreview.ScreenCount;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(
                    "workspace.screen_preview_save_failed",
                    "Could not save screen previews",
                    ex,
                    LogField.Identifier("workspaceId", capture.Snapshot.WorkspaceId),
                    LogField.Public("errorCategory", "screen_preview_storage"));
                try { _workspaceService.DeleteScreenPreviews(capture.Snapshot.WorkspaceId); }
                catch (Exception cleanupError)
                {
                    AppLogger.Warn(
                        "workspace.screen_preview_cleanup_failed",
                        "Could not remove an outdated screen preview",
                        cleanupError,
                        LogField.Identifier("workspaceId", capture.Snapshot.WorkspaceId),
                        LogField.Public("errorCategory", "screen_preview_storage"));
                }
            }
            AppLogger.Info(
                "workspace.saved",
                "Saved a named workspace",
                LogField.Identifier("workspaceId", capture.Snapshot.WorkspaceId),
                LogField.Workspace("workspaceName", name),
                LogField.Public("saveFiles", saveFiles),
                LogField.Public("browserCaptureStatus", capture.BrowserCapture.Status));
            if (!previewComplete)
                MessageBox.Show(
                    ScreenPreviewText.SaveWarning,
                    "SuKe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            return new SaveWorkspaceWorkflowResult(
                SaveWorkspaceWorkflowStatus.Saved,
                name,
                selectedWindows.Count,
                capture);
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "workspace.save_failed",
                "Workspace capture or persistence failed",
                ex,
                LogField.Workspace("workspaceName", name),
                LogField.Public("errorCategory", "workspace_save"));
            MessageBox.Show(
                Loc.Format("Failed to save workspace: {0}", ex.Message),
                Loc.T("SuKe"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return new SaveWorkspaceWorkflowResult(
                SaveWorkspaceWorkflowStatus.Failed,
                name,
                selectedWindows.Count);
        }
        finally
        {
            if (owner != null && ownerWasEnabled.HasValue)
                owner.IsEnabled = ownerWasEnabled.Value;
            progressWindow?.Close();
        }
    }
}
