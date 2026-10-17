using System.Windows;
using System.Windows.Controls;

namespace SuKe;

public partial class App
{
    private void OnOpenMainWindowClick(object sender, RoutedEventArgs e) => ShowSettingsWindow();

    private void ShowSettingsPage()
    {
        ShowSettingsWindow();
        if (_settingsWindow is { } window) ActivateSettingsPage(window);
    }

    private void ShowWorkspacesPage()
    {
        ShowSettingsWindow();
        if (_settingsWindow is { } window) ActivateWorkspacesPage(window);
    }

    internal static void ActivateSettingsPage(Window window) => ActivatePage(window, "NavSettings");

    internal static void ActivateWorkspacesPage(Window window) => ActivatePage(window, "NavWorkspaces");

    private static void ActivatePage(Window window, string navigationName)
    {
        if (window.FindName(navigationName) is RadioButton page) page.IsChecked = true;
        ActivateMainWindow(window);
    }

    internal static bool IsBackgroundLaunch(string[] args) =>
        args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

    internal static bool ShouldStartMinimized(bool backgroundLaunchRequested, bool trayIconAvailable) =>
        backgroundLaunchRequested && trayIconAvailable;

    internal static void ActivateMainWindow(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        if (!window.IsVisible) window.Show();
        window.Activate();
    }
}
