using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SuKe.Localization;
using Wpf.Ui.Controls;

namespace SuKe.UI;

public partial class SettingsWindow
{
    private void InitialisePresentation()
    {
        // 主题（Wpf.Ui 控件 + 石墨调色板）由 ThemeService 在应用级统一挂载；
        // 本窗口只补充参考图专用样式，其中已不含任何字面颜色。
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SuKe;component/UI/Shell/ReferenceStyles.xaml", UriKind.Relative),
        });
        SetResourceReference(BackgroundProperty, "WaCanvasBrush");
        UseLayoutRounding = true;
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            ConfigureSettingsPresentation();
            foreach (TitleBar titleBar in Descendants(this).OfType<TitleBar>())
            {
                titleBar.ShowMinimize = true;
                titleBar.ShowMaximize = true;
            }
            UpdateShellWidth();
        };
        SizeChanged += (_, _) => UpdateShellWidth();
    }

    private void UpdateShellWidth()
    {
        if (!IsLoaded) return;
        bool compact = ActualWidth < 980;
        NavRail.Width = compact ? 88 : Math.Clamp(ActualWidth * 0.184, 220, 292);
        SettingsCategoryColumn.Width = new GridLength(ActualWidth < 1180 ? 184 : Math.Min(304, ActualWidth * 0.192));
        foreach (RadioButton nav in new[] { NavWorkspaces, NavActivity, NavSettings })
        {
            string label = nav == NavWorkspaces ? Loc.T("Workspaces")
                : nav == NavActivity ? Loc.T("Activity") : Loc.T("Settings");
            nav.Content = compact ? "" : label;
            nav.ToolTip = label;
            AutomationProperties.SetName(nav, label);
        }

        BrandPanel.Children.OfType<StackPanel>().First().Visibility =
            compact ? Visibility.Collapsed : Visibility.Visible;
        NavRail.ToolTip = StatusText.Text;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
                yield return descendant;
        }
    }
}
