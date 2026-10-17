using System.Windows;
using SuKe.UI.Shell;

namespace SuKe.UI;

public partial class SettingsWindow
{
    private void OnWorkspaceToolsClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnCardPreviewLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is WorkspacePreviewControl { DataContext: WorkspaceRow row } preview)
            preview.Configure(row.Source, _storageService.Previews);
    }
}
