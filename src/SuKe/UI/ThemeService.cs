using System;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using SuKe.Models;
using SuKe.Services;
using WpfApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;

namespace SuKe.UI;

/// <summary>
/// Owns the effective colour theme and applies it to the application.
/// </summary>
/// <remarks>
/// <para>
/// Two dictionaries are switched together. WPF-UI's own theme colours the controls, flyouts, and
/// dialogs; the shell palette (<c>Theme.Dark.xaml</c> / <c>Theme.Light.xaml</c>) colours the graphite
/// surfaces that the workspace, activity, and settings pages are built from. Because both are always
/// applied for the same mode, a window can never show a light surface with dark controls.
/// </para>
/// <para>
/// The palette is merged at <b>application</b> scope rather than per window. Shared styles such as
/// <c>WaPageTitle</c> live in an application-level dictionary, and a brush a style sets through
/// <c>DynamicResource</c> does not re-resolve when a *window*-level dictionary is swapped — the page
/// titles then kept the previous theme's text colour on the new background. Replacing the
/// application-level dictionary invalidates those references, which is also how WPF-UI switches its
/// own theme.
/// </para>
/// <para>
/// The palette is appended last so it wins the resource lookup over WPF-UI's brush keys. Nothing here
/// changes any setting on its own: <see cref="SetMode"/> persists through the caller-supplied
/// settings service.
/// </para>
/// </remarks>
internal static class ThemeService
{
    private static readonly Uri DarkPaletteSource =
        new("pack://application:,,,/SuKe;component/UI/Shell/Theme.Dark.xaml");

    private static readonly Uri LightPaletteSource =
        new("pack://application:,,,/SuKe;component/UI/Shell/Theme.Light.xaml");

    private static readonly object Sync = new();
    private static ResourceDictionary? _appliedPalette;
    private static bool _systemEventsHooked;

    /// <summary>当前主题偏好；尚未载入配置时也默认亮色。</summary>
    internal static AppThemeMode Mode { get; private set; } = AppThemeMode.Light;

    /// <summary>Raised after the effective theme changed and the application was updated.</summary>
    internal static event Action? Changed;

    /// <summary>是否使用深色；未知设置回退到默认亮色。</summary>
    internal static bool IsDark => Mode switch
    {
        AppThemeMode.Dark => true,
        AppThemeMode.FollowSystem => SystemPrefersDark(),
        _ => false
    };

    /// <summary>Applies the stored preference. Called once during startup, before any window opens.</summary>
    internal static void Initialize(AppThemeMode mode, SettingsService? settings)
    {
        Mode = mode;
        HookSystemEvents();
        Apply();
    }

    /// <summary>Stores and applies a new preference, optionally persisting it.</summary>
    internal static void SetMode(AppThemeMode mode, SettingsService? settings, bool persist)
    {
        Mode = mode;
        if (persist && settings is not null)
        {
            settings.Settings.ThemeMode = mode;
            settings.Save();
        }

        HookSystemEvents();
        Apply();
    }

    /// <summary>The palette dictionary currently merged into the application, for verification.</summary>
    internal static Uri? AppliedPaletteSource
    {
        get
        {
            lock (Sync)
                return _appliedPalette?.Source;
        }
    }

    private static void Apply()
    {
        Application? application = Application.Current;
        if (application is null) return;

        bool dark = IsDark;
        // WPF-UI first: it may replace its own dictionary, and the palette must stay last so its
        // overrides keep winning.
        ApplicationThemeManager.Apply(
            dark ? WpfApplicationTheme.Dark : WpfApplicationTheme.Light);

        lock (Sync)
        {
            var dictionaries = application.Resources.MergedDictionaries;
            if (_appliedPalette is not null)
                dictionaries.Remove(_appliedPalette);
            _appliedPalette = new ResourceDictionary
            {
                Source = dark ? DarkPaletteSource : LightPaletteSource
            };
            dictionaries.Add(_appliedPalette);
        }

        AppLogger.Info(
            "theme.applied",
            "Applied the interface theme",
            LogField.Public("mode", Mode),
            LogField.Public("effective", dark ? "dark" : "light"));

        Changed?.Invoke();
    }

    /// <summary>
    /// Keeps "follow the system" honest: Windows raises this when the app theme changes.
    /// </summary>
    /// <remarks>
    /// The handler is installed once and does nothing unless the current mode follows the system, so
    /// switching away from that mode cannot leave the palette tracking Windows.
    /// </remarks>
    private static void HookSystemEvents()
    {
        if (_systemEventsHooked)
            return;
        _systemEventsHooked = true;
        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        catch (Exception ex)
        {
            // A session that forbids window-station hooks simply keeps the last applied theme.
            _systemEventsHooked = false;
            AppLogger.Warn(
                "theme.system_watcher_unavailable",
                "Could not subscribe to system theme changes",
                ex,
                LogField.Public("errorCategory", "theme_watcher"));
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Mode != AppThemeMode.FollowSystem)
            return;
        Application? application = Application.Current;
        if (application is null)
            return;
        application.Dispatcher.BeginInvoke(new Action(Apply));
    }

    /// <summary>跟随 Windows 的应用主题；读取失败时使用亮色。</summary>
    private static bool SystemPrefersDark()
    {
        try
        {
            return ApplicationThemeManager.GetSystemTheme() switch
            {
                SystemTheme.Dark => true,
                SystemTheme.Light => false,
                // HC1/HC2/HCBlack are the dark high-contrast schemes; HCWhite is the light one.
                SystemTheme.HC1 or SystemTheme.HC2 or SystemTheme.HCBlack => true,
                SystemTheme.HCWhite => false,
                // 未识别的系统主题使用应用默认亮色。
                _ => false
            };
        }
        catch (Exception)
        {
            return false;
        }
    }
}
