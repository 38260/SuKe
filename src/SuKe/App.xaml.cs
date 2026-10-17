using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using H.NotifyIcon;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe;

public partial class App : System.Windows.Application
{
    private TaskbarIcon?        _trayIcon;
    private LayoutCoordinator?  _coordinator;
    private MonitorService?     _monitorService;
    private WorkspaceService?   _workspaceService;
    private StorageService?     _storageService;
    private SettingsService?    _settingsService;
    private WindowService?      _windowService;
    private HotkeyService?     _hotkeyService;
    private UI.SettingsWindow? _settingsWindow;
    private UI.HelpReferenceWindow? _helpWindow;
    private int _shutdownStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 捕获本地化挂钩和后续主窗口初始化中的启动异常，避免进程消失时没有线索。
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        AppLogger.Info("app.startup_entered", "Application startup initialization entered");

        // Installs the runtime Chinese localization hooks before any window is created.
        Loc.Install();

        if (e.Args.Length > 0 &&
            e.Args[0].Equals("--export-diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            string destination = e.Args.Length > 1
                ? e.Args[1]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"SuKe-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
            try
            {
                AppLogger.ExportDiagnostics(destination);
                System.Windows.MessageBox.Show(
                    Loc.Format("A redacted diagnostic export was written to:{0}{1}", Environment.NewLine, destination),
                    Loc.T("SuKe Diagnostics"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                AppLogger.Error(
                    "diagnostics.export_failed",
                    "Could not create a diagnostic export",
                    ex,
                    LogField.Path("destinationPath", destination),
                    LogField.Public("errorCategory", "diagnostic_export"));
                System.Windows.MessageBox.Show(
                    Loc.T("SuKe could not create the diagnostic export."),
                    Loc.T("SuKe Diagnostics"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Length > 0 &&
            e.Args[0].Equals("--simulate-restore", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (e.Args.Length < 2)
                    throw new ArgumentException("--simulate-restore requires a fixture path.");
                RestoreSimulationResult result = RestoreSimulationRunner.Run(
                    RestoreSimulationRunner.Load(e.Args[1]));
                Console.Out.WriteLine(result.ToRedactedJson());
                Shutdown(result.Succeeded ? 0 : 2);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Shutdown(1);
            }
            return;
        }

        string? nativeMessagingPipe = BrowserIntegrationService.ResolveNativeMessagingPipe(e.Args);
        if (nativeMessagingPipe != null)
        {
            NativeMessagingHost.Run(nativeMessagingPipe);
            Shutdown(0);
            return;
        }

        bool backgroundLaunchRequested = IsBackgroundLaunch(e.Args);

        AppLogger.Info(
            "app.starting",
            "SuKe is starting");

        bool TryInitializeTrayIcon()
        {
            try
            {
                _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
                _trayIcon.ToolTipText = Loc.T(_trayIcon.ToolTipText);
                _trayIcon.ForceCreate();
                if (_trayIcon.IsCreated) return true;
                throw new InvalidOperationException("The notification area icon was not created.");
            }
            catch (Exception ex)
            {
                AppLogger.Warn(
                    "app.tray_initialization_failed",
                    "Could not initialize the notification area icon; continuing with the main window available",
                    ex,
                    LogField.Public("errorCategory", "tray_initialization"));
                try { _trayIcon?.Dispose(); } catch { }
                _trayIcon = null;
                return false;
            }
        }

        bool trayIconAvailable = backgroundLaunchRequested && TryInitializeTrayIcon();
        bool minimized = ShouldStartMinimized(backgroundLaunchRequested, trayIconAvailable);
        if (backgroundLaunchRequested && !trayIconAvailable)
            AppLogger.Warn(
                "app.background_launch_fallback",
                "Could not keep the background launch hidden because the notification area icon is unavailable");

        var storageService    = new StorageService();
        _storageService       = storageService;
        _monitorService       = new MonitorService();
        // Settings must exist before WindowService: it reads the dedicated-browser URL patterns
        // to decide whether address bars are queried during a snapshot.
        _settingsService      = new SettingsService(storageService);
        AppLogger.MinimumLevel = _settingsService.Settings.DiagnosticLogLevel;

        // Interface language and colour theme are stored preferences, so both are applied before
        // any window opens. Loc.Install() already ran above; switching the language here only flips
        // which direction its already-registered hooks translate.
        Loc.SetLanguage(_settingsService.Settings.UiLanguage);
        UI.ThemeService.Initialize(_settingsService.Settings.ThemeMode, _settingsService);
        Loc.LanguageChanged += OnInterfaceLanguageChanged;

        var windowService     = new WindowService(_settingsService);
        _windowService        = windowService;
        var jumpListService   = new JumpListService();
        var webAppService     = new WebAppService();
        var browserSessionBridge = new BrowserSessionBridge();
        var workspaceService  = new WorkspaceService(
            storageService,
            windowService,
            _monitorService,
            jumpListService,
            webAppService,
            browserSessionBridge,
            settingsService: _settingsService);

        _workspaceService = workspaceService;
        _coordinator      = new LayoutCoordinator(_monitorService, windowService, workspaceService);
        _coordinator.SeedDisplayTopologyBaseline();

        // Hotkeys (settings were created above, before WindowService)
        _hotkeyService   = new HotkeyService();
        _hotkeyService.Initialise();
        ApplyHotkeySettings();

        UI.TypographyService.Initialize(_settingsService.Settings.UiFontScale);

        string initialFingerprint = _monitorService.GetCurrentMonitorFingerprint();
        AppLogger.Info(
            "display.initial_topology",
            "Captured the initial display topology",
            LogField.Identifier("monitorFingerprint", initialFingerprint));
        if (minimized)
            AppLogger.Info(
                "app.started_minimized",
                "SuKe started minimized in the notification area");

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // ── Startup workspace restore (deferred to the dispatcher idle queue) ──
        var startupBehavior = _settingsService.Settings.StartupBehavior;
        if (startupBehavior != StartupBehavior.None)
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await HandleStartupRestoreAsync(startupBehavior);
            }, DispatcherPriority.ApplicationIdle);
        }

        if (!minimized) ShowSettingsWindow();
        ScheduleFirstRunOnboarding(minimized);
        if (!backgroundLaunchRequested)
            _ = Dispatcher.InvokeAsync(TryInitializeTrayIcon, DispatcherPriority.ApplicationIdle);
    }

    // ── Startup workspace restore ─────────────────────────────────────────

    private async Task HandleStartupRestoreAsync(StartupBehavior behavior)
    {
        try
        {
            var workspaces = _workspaceService!.GetAllWorkspaces();
            if (workspaces.Count == 0) return;

            WorkspaceSnapshot? target = null;
            // Skip on the startup prompt means "not now", not "do nothing": it lands the user on
            // the main interface instead of leaving the app with no visible window.
            bool openMainInterface = false;

            switch (behavior)
            {
                case StartupBehavior.RestoreDefault:
                    string? defaultId = _settingsService!.Settings.DefaultWorkspaceId;
                    if (!string.IsNullOrEmpty(defaultId))
                        target = workspaces.FirstOrDefault(w =>
                            w.WorkspaceId.Equals(defaultId, StringComparison.OrdinalIgnoreCase));
                    break;

                case StartupBehavior.RestoreLastUsed:
                    target = workspaces.OrderByDescending(w => w.SavedAt).FirstOrDefault();
                    break;

                case StartupBehavior.AskUser:
                    var dialog = new UI.StartupWorkspaceDialog(workspaces, _storageService!.Previews);
                    if (dialog.ShowDialog() == true)
                        target = dialog.SelectedWorkspace;
                    else
                        openMainInterface = dialog.SkipRequested;
                    break;
            }

            if (target == null && openMainInterface)
                ShowSettingsWindow();

            if (target != null)
            {
                DisplayTopologyStabilizationResult stabilization =
                    await _coordinator!.WaitForStableDisplayTopologyAsync();
                if (!stabilization.IsStable)
                {
                    AppLogger.Info(
                        "app.startup_restore_topology_timeout",
                        "Skipped startup restore because the display topology did not settle before the timeout",
                        LogField.Identifier("monitorFingerprint", stabilization.Snapshot.Fingerprint));
                    return;
                }

                AppLogger.Info(
                    "app.startup_restore_started",
                    "Started the configured startup restore",
                    LogField.Identifier("workspaceId", target.WorkspaceId),
                    LogField.Workspace("workspaceName", target.Name));
                await _coordinator!.RestoreWorkspaceAsync(target);
                ShowBalloon("Workspace Restored", Loc.Format("“{0}” restored on startup", target.Name));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "app.startup_restore_failed",
                "Startup restore failed",
                ex,
                LogField.Public("errorCategory", "startup_restore"));
        }
    }

    private async void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        string fingerprint = _monitorService?.GetCurrentMonitorFingerprint() ?? "unknown";
        AppLogger.Info(
            "display.settings_changed",
            "Received a display-settings change notification",
            LogField.Identifier("monitorFingerprint", fingerprint));
        if (_coordinator is not null)
            await _coordinator.HandleDisplayChangeAsync();
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is { } existing)
        {
            ActivateMainWindow(existing);
            return;
        }

        _settingsWindow = new UI.SettingsWindow(
            _workspaceService!,
            _storageService!,
            _coordinator!,
            _settingsService!,
            _monitorService!,
            () => ShowHelpWindow());
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        ActivateMainWindow(_settingsWindow);
    }

    private void OnOpenHelpClick(object sender, RoutedEventArgs e) => ShowHelpWindow();

    private void ShowHelpWindow(bool firstRun = false)
    {
        if (_helpWindow is { IsVisible: true })
        {
            _helpWindow.Activate();
            return;
        }

        var help = new UI.HelpReferenceWindow(firstRun);
        _helpWindow = help;
        help.SaveWorkspaceRequested += (_, _) => ShowSaveWorkspaceDialog();
        help.SettingsRequested += (_, _) => ShowSettingsPage();
        help.Closed += (_, _) =>
        {
            if (ReferenceEquals(_helpWindow, help))
                _helpWindow = null;
        };
        help.Show();
    }

    private void ScheduleFirstRunOnboarding(bool minimized)
    {
        if (_settingsService is null || _workspaceService is null)
            return;

        bool hasSavedWorkspaces = _workspaceService.GetAllWorkspaces().Count > 0;
        bool shouldShow = FirstRunOnboardingPolicy.ShouldShow(
            _settingsService.Settings.OnboardingCompleted,
            minimized,
            _settingsService.IsSaveBlocked,
            hasSavedWorkspaces);

        if (!shouldShow)
        {
            // A workspace is stronger evidence of an established installation than a missing
            // settings file. Record that fact silently instead of showing first-run UI later.
            if (!minimized &&
                !_settingsService.IsSaveBlocked &&
                !_settingsService.Settings.OnboardingCompleted &&
                hasSavedWorkspaces)
            {
                _settingsService.Settings.OnboardingCompleted = true;
                _settingsService.Save();
            }
            return;
        }

        // Persist before showing so an application restart cannot turn onboarding into spam.
        _settingsService.Settings.OnboardingCompleted = true;
        _settingsService.Save();
        AppLogger.Info(
            "onboarding.first_run_scheduled",
            "Scheduled the first interactive-launch guide");
        _ = Dispatcher.InvokeAsync(
            () => ShowHelpWindow(firstRun: true),
            DispatcherPriority.ApplicationIdle);
    }

    private async void ShowSaveWorkspaceDialog()
    {
        var workflow = new UI.SaveWorkspaceWorkflow(_workspaceService!, _settingsService);
        UI.SaveWorkspaceWorkflowResult result = await workflow.RunAsync();
        if (result.Status == UI.SaveWorkspaceWorkflowStatus.Saved)
        {
            ShowBalloon("Workspace Saved",
                Loc.Format("“{0}” saved — {1} window(s)", result.WorkspaceName, result.SelectedWindowCount));
        }
    }

    private void OnTrayMenuOpened(object sender, RoutedEventArgs e)
    {
        PopulateWorkspacesMenu();
        if (_trayIcon?.ContextMenu is { } trayMenu)
        {
            UI.TypographyService.Apply(trayMenu);
            // The tray menu lives outside every window, so the Loaded-time localization pass
            // is not guaranteed to have covered it before the menu is displayed. Re-apply on
            // each open; the walk is idempotent and leaves translated items untouched.
            Loc.Apply(trayMenu);
            AppLogger.Debug(
                "tray.menu_opened",
                "Tray context menu opened; top-level headers after localization",
                LogField.Public("headers", string.Join(
                    " | ",
                    trayMenu.Items
                        .OfType<System.Windows.Controls.MenuItem>()
                        .Select(i => i.Header as string ?? string.Empty))));

            foreach (object item in trayMenu.Items)
            {
                if (item is not System.Windows.Controls.MenuItem menuItem)
                    continue;
                if (menuItem.Name == "UndoLastRestoreMenuItem")
                {
                    menuItem.IsEnabled = _coordinator?.CanUndoLastRestore == true;
                }
                else if (menuItem.Name == "RestorePendingDisplayRecoveryMenuItem")
                {
                    bool hasPending = _coordinator?.HasPendingDisplayRecovery == true;
                    menuItem.IsEnabled = hasPending;
                    menuItem.Header = hasPending
                        ? Loc.Format("Restore Returned Layout: {0}", _coordinator!.PendingDisplayRecoveryName)
                        : Loc.T("Restore Returned Display Layout");
                }
                else if (menuItem.Name == "DismissPendingDisplayRecoveryMenuItem")
                {
                    menuItem.IsEnabled = _coordinator?.HasPendingDisplayRecovery == true;
                }
                else if (menuItem.Name == "CopyLastRestoreDiagnosticsMenuItem")
                {
                    menuItem.IsEnabled = RestoreDiagnosticsReportStore.HasLatest;
                }
            }
        }
    }

    private async void OnUndoLastRestoreClick(object sender, RoutedEventArgs e)
    {
        if (_coordinator is null) return;
        try
        {
            await UI.RestorePlanPreviewWorkflow.RunUndoAsync(_coordinator);
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "restore.undo_failed",
                "Undo Last Restore failed",
                ex,
                LogField.Public("errorCategory", "restore_undo"));
            ShowBalloon(
                "Undo Failed",
                "The previous desktop state could not be restored.",
                H.NotifyIcon.Core.NotificationIcon.Warning);
        }
    }

    private async void OnRestorePendingDisplayRecoveryClick(object sender, RoutedEventArgs e)
    {
        if (_coordinator is null) return;
        try
        {
            await _coordinator.RestorePendingDisplayRecoveryAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "display.recovery_failed",
                "The offered display recovery could not be restored",
                ex,
                LogField.Public("errorCategory", "display_recovery"));
            ShowBalloon(
                "Display Recovery Failed",
                "The returned display layout could not be restored.",
                H.NotifyIcon.Core.NotificationIcon.Warning);
        }
    }

    private void OnDismissPendingDisplayRecoveryClick(object sender, RoutedEventArgs e) =>
        _coordinator?.DismissPendingDisplayRecovery();

    /// <summary>
    /// Keeps the tray surface in step with the interface language.
    /// </summary>
    /// <remarks>
    /// The tray icon and its context menu live outside every window, so the localization walker's
    /// window hook never reaches them. Both are updated explicitly — the tooltip from the catalog
    /// and the already-realized menu items through a forced re-walk in the new direction.
    /// </remarks>
    private void OnInterfaceLanguageChanged()
    {
        if (_trayIcon is null) return;
        _trayIcon.ToolTipText = Loc.T(_trayIcon.ToolTipText);
        if (_trayIcon.ContextMenu is { } menu)
            Loc.Reapply(menu);
    }

    private void OnCopyLastRestoreDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (!RestoreDiagnosticsReportStore.TryGetLatest(out RestoreDiagnosticsReport? report) ||
            report is null)
            return;
        if (UI.DiagnosticClipboard.TryCopy(report.ToRedactedJson(), out string errorCategory))
        {
            ShowBalloon(
                "Restore Diagnostics Copied",
                "The latest redacted restore report is ready to paste.");
            return;
        }

        // A refused clipboard is a clipboard availability problem, not a restore failure.
        AppLogger.Warn(
            "restore.diagnostics_copy_failed",
            "Could not copy the latest restore diagnostics report",
            LogField.Public("errorCategory", errorCategory));
        ShowBalloon(
            "Diagnostics Not Copied",
            "SuKe could not copy the latest restore report.",
            H.NotifyIcon.Core.NotificationIcon.Warning);
    }

    private void OnRescueActiveWindowClick(object sender, RoutedEventArgs e)
    {
        if (_windowService is null || _monitorService is null || _settingsService is null)
            return;

        OffScreenWindowRescueResult result = _windowService.RescueForegroundWindow(
            _monitorService.GetCurrentMonitors(),
            new OffScreenWindowRescuePolicy
            {
                MinimumVisibleAreaRatio = _settingsService.Settings.MinimumVisibleWindowAreaRatio
            });
        switch (result.Status)
        {
            case OffScreenWindowRescueStatus.Rescued:
                ShowBalloon("Active Window Rescued", "The active window was moved into a reachable work area.");
                break;
            case OffScreenWindowRescueStatus.AlreadyVisible:
                ShowBalloon("Active Window Is Reachable", "The active window already has enough visible area.");
                break;
            case OffScreenWindowRescueStatus.NoActiveWindow:
                ShowBalloon("No Active Window", "Select an application window, then try again.",
                    H.NotifyIcon.Core.NotificationIcon.Warning);
                break;
            default:
                ShowBalloon("Window Rescue Needs Attention", "SuKe could not update the active window placement.",
                    H.NotifyIcon.Core.NotificationIcon.Warning);
                break;
        }
    }

    private void PopulateWorkspacesMenu()
    {
        var trayMenu = _trayIcon?.ContextMenu;
        if (trayMenu == null) return;

        System.Windows.Controls.MenuItem? workspacesItem = null;
        foreach (var item in trayMenu.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.Name == "WorkspacesMenu")
            {
                workspacesItem = mi;
                break;
            }
        }
        if (workspacesItem is null) return;

        workspacesItem.Items.Clear();

        var workspaces = GetOrderedWorkspaces();

        if (workspaces.Count == 0)
        {
            workspacesItem.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = Loc.T("(no saved workspaces)"), IsEnabled = false
            });
        }
        else
        {
            foreach (var ws in workspaces)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = Loc.Format("Restore: {0}", ws.Name)
                };
                var captured = ws;
                item.Click += (_, _) => OnRestoreWorkspaceClick(captured);
                workspacesItem.Items.Add(item);
            }

            workspacesItem.Items.Add(new System.Windows.Controls.Separator());

            foreach (var ws in workspaces)
            {
                var switchItem = new System.Windows.Controls.MenuItem
                {
                    Header = Loc.Format("Switch to: {0}", ws.Name)
                };
                var captured = ws;
                switchItem.Click += (_, _) => OnSwitchWorkspaceClick(captured);
                workspacesItem.Items.Add(switchItem);
            }

            workspacesItem.Items.Add(new System.Windows.Controls.Separator());

            foreach (var ws in workspaces)
            {
                var alignItem = new System.Windows.Controls.MenuItem
                {
                    Header = Loc.Format("Align + minimize others: {0}", ws.Name)
                };
                var captured = ws;
                alignItem.Click += (_, _) => OnAlignWorkspaceClick(captured);
                workspacesItem.Items.Add(alignItem);
            }
        }

        // Always append Save + Manage at the bottom
        workspacesItem.Items.Add(new System.Windows.Controls.Separator());
        var saveItem = new System.Windows.Controls.MenuItem { Header = Loc.T("Save Current Workspace...") };
        saveItem.Click += (_, _) => ShowSaveWorkspaceDialog();
        workspacesItem.Items.Add(saveItem);

        var manageItem = new System.Windows.Controls.MenuItem { Header = Loc.T("Manage Workspaces") };
        manageItem.Click += (_, _) => ShowWorkspacesPage();
        workspacesItem.Items.Add(manageItem);
    }

    private void OnRestoreWorkspaceClick(SuKe.Models.WorkspaceSnapshot snapshot)
    {
        if (_coordinator is not null)
            _ = UI.RestorePlanPreviewWorkflow.RunWorkspaceDefaultAsync(
                _coordinator,
                snapshot);
    }

    private void OnSwitchWorkspaceClick(SuKe.Models.WorkspaceSnapshot snapshot)
    {
        if (_coordinator is not null)
            _ = UI.RestorePlanPreviewWorkflow.RunSwitchAsync(_coordinator, snapshot);
    }

    private void OnAlignWorkspaceClick(SuKe.Models.WorkspaceSnapshot snapshot)
    {
        if (_coordinator is not null)
            _ = UI.RestorePlanPreviewWorkflow.RunAsync(
                _coordinator,
                snapshot,
                RestoreMode.AlignAndMinimize);
    }

    private async void OnExitClick(object sender, RoutedEventArgs e)
    {
        AppLogger.Info(
            "app.exit_requested",
            "User requested application exit");
        await DisposeOwnedServicesAsync();
        Current.Shutdown();
    }

    private async Task DisposeOwnedServicesAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_coordinator is not null)
            await _coordinator.DisposeAsync();
        if (_workspaceService is not null)
            await _workspaceService.DisposeAsync();
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
    }

    // ── Hotkey integration ────────────────────────────────────────────────

    /// <summary>
    /// (Re)registers or unregisters all global hotkeys based on the current
    /// settings.  Called from OnStartup and from SettingsWindow when the user
    /// toggles the switch or changes a shortcut.
    /// </summary>
    public void ApplyHotkeySettings()
    {
        if (_hotkeyService == null || _settingsService == null) return;

        _hotkeyService.UnregisterAll();

        if (!_settingsService.Settings.HotkeysEnabled) return;

        // Merge defaults with any user-customised shortcuts
        var shortcuts = HotkeyService.GetResolvedShortcuts(_settingsService.Settings);

        foreach (var shortcut in shortcuts)
        {
            Action? callback = shortcut.ActionId switch
            {
                "QuickSave"      => () => Dispatcher.Invoke(ShowSaveWorkspaceDialog),
                "RestoreDefault" => () => Dispatcher.Invoke(RestoreDefaultWorkspace),
                "RestoreSlot1"   => () => Dispatcher.Invoke(() => RestoreWorkspaceByIndex(0)),
                "RestoreSlot2"   => () => Dispatcher.Invoke(() => RestoreWorkspaceByIndex(1)),
                "RestoreSlot3"   => () => Dispatcher.Invoke(() => RestoreWorkspaceByIndex(2)),
                "SwitchSlot1"    => () => Dispatcher.Invoke(() => SwitchWorkspaceByIndex(0)),
                "SwitchSlot2"    => () => Dispatcher.Invoke(() => SwitchWorkspaceByIndex(1)),
                "SwitchSlot3"    => () => Dispatcher.Invoke(() => SwitchWorkspaceByIndex(2)),
                "OpenSettings"   => () => Dispatcher.Invoke(() => OnOpenSettingsClick(this, new RoutedEventArgs())),
                "SwitchDefault"  => () => Dispatcher.Invoke(SwitchDefaultWorkspace),
                _ => null,
            };

            if (callback != null)
                _hotkeyService.Register(shortcut.Modifiers, shortcut.Key, callback);
        }
    }

    private void RestoreDefaultWorkspace()
    {
        string? workspaceId = _settingsService?.Settings.DefaultWorkspaceId;
        if (string.IsNullOrEmpty(workspaceId)) return;

        var ws = _workspaceService?.GetAllWorkspaces()
            .FirstOrDefault(w => w.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));
        if (ws != null)
            _ = UI.RestorePlanPreviewWorkflow.RunWorkspaceDefaultAsync(
                _coordinator!,
                ws);
    }

    private void SwitchDefaultWorkspace()
    {
        string? workspaceId = _settingsService?.Settings.DefaultWorkspaceId;
        if (string.IsNullOrEmpty(workspaceId)) return;

        var ws = _workspaceService?.GetAllWorkspaces()
            .FirstOrDefault(w => w.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));
        if (ws != null)
            _ = UI.RestorePlanPreviewWorkflow.RunSwitchAsync(_coordinator!, ws);
    }

    private void RestoreWorkspaceByIndex(int index)
    {
        var workspaces = GetOrderedWorkspaces();
        if (index < workspaces.Count)
            _ = UI.RestorePlanPreviewWorkflow.RunWorkspaceDefaultAsync(
                _coordinator!,
                workspaces[index]);
    }

    private void SwitchWorkspaceByIndex(int index)
    {
        var workspaces = GetOrderedWorkspaces();
        if (index < workspaces.Count)
            _ = UI.RestorePlanPreviewWorkflow.RunSwitchAsync(_coordinator!, workspaces[index]);
    }

    /// <summary>
    /// Returns workspaces in the user's preferred display order (matching the
    /// Settings UI).  The first three entries map to Ctrl+Alt+1/2/3.
    /// </summary>
    private List<Models.WorkspaceSnapshot> GetOrderedWorkspaces()
    {
        var all   = _workspaceService?.GetAllWorkspaces() ?? new();
        var order = _settingsService?.Settings.WorkspaceOrderIds;
        if (order == null || order.Count == 0)
            return all.OrderByDescending(w => w.SavedAt).ToList();

        var result = new List<Models.WorkspaceSnapshot>();
        foreach (var workspaceId in order)
        {
            var ws = all.FirstOrDefault(w =>
                w.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));
            if (ws != null) result.Add(ws);
        }
        foreach (var ws in all.OrderByDescending(w => w.SavedAt))
        {
            if (!result.Any(r => r.WorkspaceId.Equals(ws.WorkspaceId, StringComparison.OrdinalIgnoreCase)))
                result.Add(ws);
        }
        return result;
    }

    // ── Balloon helper ────────────────────────────────────────────────────────

    public void ShowBalloon(string title, string message,
        H.NotifyIcon.Core.NotificationIcon icon = H.NotifyIcon.Core.NotificationIcon.Info)
    {
        if (_settingsService?.Settings.NotificationsEnabled == false)
            return;

        try
        {
            _trayIcon?.ShowNotification(Loc.T(title), Loc.T(message), icon);
        }
        catch (Exception ex)
        {
            AppLogger.Warn(
                "notification.show_failed",
                "Could not show a notification",
                ex,
                LogField.Public("errorCategory", "notification"));
        }
    }

    // ── Global exception handlers ─────────────────────────────────────────────

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error(
            "app.unhandled_dispatcher_exception",
            "An unhandled dispatcher exception reached the application boundary",
            e.Exception,
            LogField.Public("errorCategory", "unhandled_dispatcher_exception"));
        _trayIcon?.Dispose();   // prevent ghost tray icon
        _ = DisposeOwnedServicesAsync();
        e.Handled = false;      // let Windows show the crash dialog
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            AppLogger.Error(
                "app.unhandled_domain_exception",
                "An unhandled domain exception reached the application boundary",
                ex,
                LogField.Public("errorCategory", "unhandled_domain_exception"));
        _trayIcon?.Dispose();   // prevent ghost tray icon
        _ = DisposeOwnedServicesAsync();
    }
}

