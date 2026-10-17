using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SuKe.Localization;
using SuKe.Services;
using SuKe.UI.Shell;

namespace SuKe.UI;

/// <summary>
/// Shell behaviour for the main window: the three-page navigation, the settings category rail,
/// the status line, and the workspace card actions.
/// </summary>
/// <remarks>
/// Everything here is presentation orchestration. Restoring and previewing still go through the
/// same workflow classes the tray menu uses, so no business path is duplicated.
/// </remarks>
public partial class SettingsWindow
{
    private bool _shellReady;
    private ActivityPage? _activityPage;
    private Dictionary<RadioButton, FrameworkElement>? _categorySections;

    /// <summary>
    /// The nav and category <c>Checked</c> handlers fire while the markup is still being parsed,
    /// before the sibling pages exist as fields, so the shell only starts reacting once
    /// initialisation completes.
    /// </summary>
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        _shellReady = true;
        Loaded += OnShellLoaded;
        Activated += (_, _) => UpdateStatus();
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
        ShowSelectedPage();
    }

    // ── Main navigation ───────────────────────────────────────────────────────

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (!_shellReady) return;
        ShowSelectedPage();
    }

    private void ShowSelectedPage()
    {
        bool workspaces = NavWorkspaces.IsChecked == true;
        bool activity = NavActivity.IsChecked == true;

        WorkspacePage.Visibility = workspaces ? Visibility.Visible : Visibility.Collapsed;
        ActivityPageHost.Visibility = activity ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = !workspaces && !activity ? Visibility.Visible : Visibility.Collapsed;

        if (activity) EnsureActivityPage();
    }

    /// <summary>
    /// Builds the activity page on first use and rereads it every time it is shown, so events
    /// recorded during this session appear without reopening the window.
    /// </summary>
    private void EnsureActivityPage()
    {
        if (_activityPage is null)
        {
            _activityPage = new ActivityPage(new WorkspaceActivityReader(_storageService));
            ActivityPageHost.Content = _activityPage;
        }

        _activityPage.RefreshActivity();
    }

    // ── Settings categories ───────────────────────────────────────────────────

    /// <summary>
    /// Scrolls to the chosen category. Every setting stays rendered, so nothing can become
    /// uneditable or unsavable because of the navigation.
    /// </summary>
    private void OnSettingsCategoryChecked(object sender, RoutedEventArgs e)
    {
        if (!_shellReady || sender is not RadioButton chip) return;

        _categorySections ??= new Dictionary<RadioButton, FrameworkElement>
        {
            [CategorySystem] = SectionSystem,
            [CategoryAppearance] = SectionAppearance,
            [CategoryBrowser] = SectionBrowser,
            [CategoryPaths] = SectionPaths,
            [CategoryMatching] = SectionMatching,
            [CategoryRestore] = SectionRestore,
            [CategoryStartup] = SectionStartup,
            [CategoryHotkeys] = SectionHotkeys,
            [CategoryMonitors] = SectionMonitors,
        };

        if (_categorySections.TryGetValue(chip, out FrameworkElement? section))
            section.BringIntoView();
    }

    // ── Status line ───────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        int monitors = _monitorService.GetCurrentMonitors().Count;
        StatusText.Text = Loc.Format(
            monitors == 1
                ? "Ready  ·  {0} monitor connected"
                : "Ready  ·  {0} monitors connected",
            monitors);
    }

    // ── Workspace card actions ────────────────────────────────────────────────

    private void OnWorkspaceRestoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: WorkspaceRow row }) return;
        _ = RestorePlanPreviewWorkflow.RunWorkspaceDefaultAsync(_coordinator, row.Source, this);
    }

    private void OnWorkspaceScreenPreview(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: WorkspaceRow row }) return;
        new WorkspaceScreenPreviewDialog(row.Source, _storageService.Previews) { Owner = this }.ShowDialog();
    }
}
