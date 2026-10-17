using System.Windows;
using System.Windows.Controls;
using SuKe.Localization;
using SuKe.Models;

namespace SuKe.UI;

/// <summary>
/// Interface language and colour theme settings.
/// </summary>
/// <remarks>
/// Neither change is only a stored value: the language is applied by re-walking every open window
/// and the theme by replacing the palette dictionaries in place, so both are visible immediately
/// instead of on the next start.
/// </remarks>
public partial class SettingsWindow
{
    private void InitialiseAppearanceUI()
    {
        _suppressToggle = true;
        InterfaceLanguageCombo.SelectedIndex =
            _settingsService.Settings.UiLanguage == UiLanguage.English ? 1 : 0;
        ThemeModeCombo.SelectedIndex = _settingsService.Settings.ThemeMode switch
        {
            AppThemeMode.FollowSystem => 0,
            AppThemeMode.Light => 1,
            _ => 2
        };
        _suppressToggle = false;
    }

    private void OnInterfaceLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggle) return;

        UiLanguage language = InterfaceLanguageCombo.SelectedIndex == 1
            ? UiLanguage.English
            : UiLanguage.ChineseSimplified;
        if (_settingsService.Settings.UiLanguage == language) return;

        _settingsService.Settings.UiLanguage = language;
        _settingsService.Save();
        // Re-localizes this window, the tray menu, and every other open window in place.
        Loc.SetLanguage(language);
        // Summaries that were composed from code hold text built in the previous language; rebuild
        // them so the switch is complete rather than mostly complete.
        RefreshCodeGeneratedText();
    }

    private void OnThemeModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggle) return;

        AppThemeMode mode = ThemeModeCombo.SelectedIndex switch
        {
            1 => AppThemeMode.Light,
            2 => AppThemeMode.Dark,
            _ => AppThemeMode.FollowSystem
        };
        if (_settingsService.Settings.ThemeMode == mode) return;

        ThemeService.SetMode(mode, _settingsService, persist: true);
        // Rows built in code resolved their brushes when they were created; rebuild them so they
        // pick up the new palette.
        RefreshPersistentApplicationSummary();
    }

    /// <summary>Rebuilds every summary this window composes from code.</summary>
    private void RefreshCodeGeneratedText()
    {
        UpdateStatus();
        RefreshLogicalPathAliasSummary();
        RefreshLearnedMatchSummary();
        RefreshPersistentApplicationSummary();
        UpdateShellWidth();
    }
}
