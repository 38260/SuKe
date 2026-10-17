using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using SuKe.Localization;
using SuKe.UI.Shell;

namespace SuKe.UI;

public partial class SettingsWindow
{
    private SettingsSectionNavigator? _settingsSectionNavigator;

    private void ConfigureSettingsPresentation()
    {
        // 保留所有原控件与保存处理器；极窄窗口中横向滚动可访问完整编辑区域。
        foreach (ScrollViewer scroll in Descendants(SettingsPage).OfType<ScrollViewer>())
        {
            if (scroll.Content is not StackPanel sections || !Descendants(sections).Contains(SectionSystem)) continue;
            sections.MinWidth = 600;
            sections.SetBinding(FrameworkElement.WidthProperty,
                new Binding(nameof(ScrollViewer.ViewportWidth)) { Source = scroll });
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.CanContentScroll = false;
            AutomationProperties.SetName(scroll, Loc.T("Settings content"));
            if (_settingsSectionNavigator is null)
            {
                var categories = new Dictionary<RadioButton, FrameworkElement>
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
                foreach (RadioButton category in categories.Keys) category.Checked -= OnSettingsCategoryChecked;
                _settingsSectionNavigator = new SettingsSectionNavigator(scroll, categories);
            }
        }

        foreach (RadioButton category in new[]
        {
            CategorySystem, CategoryAppearance, CategoryBrowser, CategoryPaths, CategoryMatching,
            CategoryRestore, CategoryStartup, CategoryHotkeys, CategoryMonitors,
        })
        {
            category.ToolTip = category.Content;
            AutomationProperties.SetName(category, category.Content?.ToString() ?? "");
        }
    }
}
