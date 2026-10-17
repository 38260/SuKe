using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SuKe.UI.Shell;

/// <summary>设置分类：选中即定位正文，正文滚动时反向同步选中项。</summary>
/// <remarks>
/// 两个方向必须共用同一套「这一段在哪」的换算，否则点选与滚动会互相打架：
/// <list type="number">
/// <item>用户点选/键盘改选 → 滚到该段（鼠标短缓出，键盘直接落位）。</item>
/// <item>用户自己滚正文 → 选中项跟着走到视口顶部所在的那一段（scroll spy）。</item>
/// </list>
/// 反向同步设置 <see cref="RadioButton.IsChecked"/> 会再次触发 <see cref="OnChecked"/>，
/// 因此用 <c>_syncingFromScroll</c> 抑制回跳；用户主动点选后的一段缓出期间也不做同步，
/// 免得高亮在半路经过的分类上闪一下（超时或到位后自动解除）。
/// </remarks>
internal sealed class SettingsSectionNavigator
{
    /// <summary>判定「当前段」时视口顶部向下预留的余量。</summary>
    /// <remarks>
    /// 太小会把「标题已经进入视口、但还没滚到顶」的那一段判成下一段；96 大约是卡片内标题行加首行的
    /// 高度，正好覆盖「用户已经看到这一段开头」的时刻。
    /// </remarks>
    private const double DetectionMargin = 96;

    /// <summary>程序化定位的等待上限：超过它说明动画被打断，恢复反向同步。</summary>
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromMilliseconds(600);

    private readonly ScrollViewer _viewer;
    private readonly IReadOnlyDictionary<RadioButton, FrameworkElement> _sections;

    /// <summary>按纵向位置排好的分类；首次布局完成后建立（构造时窗口还没测量）。</summary>
    private (RadioButton Category, FrameworkElement Anchor)[]? _ordered;

    private bool _fromKeyboard;
    private bool _syncingFromScroll;
    private bool _navigating;
    private long _navigationStartedAt;
    private double _navigationTarget;

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

        viewer.ScrollChanged += OnScrollChanged;
    }

    private void OnChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingFromScroll) return;
        if (sender is not RadioButton category || !_sections.TryGetValue(category, out FrameworkElement? section)) return;
        if (_viewer.Content is not Visual content || !section.IsVisible) return;

        double top = Top(Anchor(section), content);
        _navigating = true;
        _navigationStartedAt = Stopwatch.GetTimestamp();
        _navigationTarget = Math.Clamp(top, 0, Math.Max(0, _viewer.ScrollableHeight));
        SmoothScrollBehavior.ScrollTo(_viewer, top, animate: !_fromKeyboard);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _viewer)) return;
        if (NavigationInProgress()) return;
        SyncSelectionFromScroll();
    }

    /// <summary>用户点选后的缓出期间不做反向同步；到位或超时即解除。</summary>
    private bool NavigationInProgress()
    {
        if (!_navigating) return false;
        bool arrived = Math.Abs(_viewer.VerticalOffset - _navigationTarget) <= 2;
        if (!arrived && Stopwatch.GetElapsedTime(_navigationStartedAt) < NavigationTimeout) return true;
        _navigating = false;
        return false;
    }

    /// <summary>把选中项移动到当前视口顶部所在的那一段。</summary>
    private void SyncSelectionFromScroll()
    {
        if (_viewer.Content is not Visual content) return;
        (RadioButton Category, FrameworkElement Anchor)[] ordered = Ordered(content);

        double detection = _viewer.VerticalOffset + DetectionMargin;
        (RadioButton Category, FrameworkElement Anchor) current = ordered[0];
        foreach ((RadioButton Category, FrameworkElement Anchor) entry in ordered)
            if (Top(entry.Anchor, content) <= detection) current = entry;

        // 滚到底时最后一段的标题可能还没越过检测线，但它显然已经是当前段。
        if (_viewer.ScrollableHeight > 0 && _viewer.VerticalOffset >= _viewer.ScrollableHeight - 2)
            current = ordered[^1];

        if (current.Category.IsChecked == true) return;

        _syncingFromScroll = true;
        try
        {
            current.Category.IsChecked = true;
        }
        finally
        {
            _syncingFromScroll = false;
        }
    }

    private (RadioButton Category, FrameworkElement Anchor)[] Ordered(Visual content) =>
        _ordered ??= _sections
            .OrderBy(pair => Top(Anchor(pair.Value), content))
            .Select(pair => (pair.Key, Anchor(pair.Value)))
            .ToArray();

    /// <summary>分段可能包在卡片 <see cref="Border"/> 里，滚动应该停在卡片上沿而不是内部标题上。</summary>
    private static FrameworkElement Anchor(FrameworkElement section) =>
        section.Parent is Border card ? card : section;

    private static double Top(FrameworkElement element, Visual content) =>
        element.TransformToAncestor(content).Transform(new Point()).Y;
}
