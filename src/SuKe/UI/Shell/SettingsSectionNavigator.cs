using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SuKe.UI.Shell;

/// <summary>设置分类只改变正文纵向位置；键盘直接定位，鼠标短缓出且可连续改选。</summary>
internal sealed class SettingsSectionNavigator
{
    private readonly ScrollViewer _viewer;
    private readonly IReadOnlyDictionary<RadioButton, FrameworkElement> _sections;
    private bool _fromKeyboard;

    internal SettingsSectionNavigator(ScrollViewer viewer, IReadOnlyDictionary<RadioButton, FrameworkElement> sections)
    {
        _viewer = viewer;
        _sections = sections;
        SmoothScrollBehavior.SetEnabled(viewer, true);
        foreach (RadioButton category in sections.Keys)
        {
            category.PreviewKeyDown += (_, _) => _fromKeyboard = true;
            category.PreviewMouseDown += (_, _) => _fromKeyboard = false;
            category.Checked += OnChecked;
        }
    }

    private void OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton category || !_sections.TryGetValue(category, out FrameworkElement? section)) return;
        if (_viewer.Content is not Visual content || !section.IsVisible) return;
        FrameworkElement anchor = section.Parent is Border card ? card : section;
        double top = anchor.TransformToAncestor(content).Transform(new Point()).Y;
        SmoothScrollBehavior.ScrollTo(_viewer, top, animate: !_fromKeyboard);
    }
}
