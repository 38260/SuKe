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
        ScrollViewer? contentScroll = null;

        // 保留所有原控件与保存处理器；极窄窗口中横向滚动可访问完整编辑区域。
        foreach (ScrollViewer scroll in Descendants(SettingsPage).OfType<ScrollViewer>())
        {
            if (scroll.Content is not StackPanel sections || !Descendants(sections).Contains(SectionSystem)) continue;
            contentScroll = scroll;
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

        // 左侧目录是固定索引，不随正文滚动：滚轮落在目录上时把这一格转给右侧正文。
        // 目录本身仍保留滚动条，窗口过矮时可以用它翻到下面的分类。
        ScrollViewer? categoryScroll = Descendants(SettingsPage).OfType<ScrollViewer>()
            .FirstOrDefault(scroll => scroll.Content is StackPanel panel && Descendants(panel).Contains(CategorySystem));
        if (categoryScroll is not null && contentScroll is not null)
        {
            categoryScroll.Focusable = false;
            SmoothScrollBehavior.ForwardWheel(categoryScroll, contentScroll);
            AutomationProperties.SetName(categoryScroll, Loc.T("Settings categories"));
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
