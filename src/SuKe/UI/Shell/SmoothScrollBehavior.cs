using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SuKe.UI.Shell;

/// <summary>
/// 为指定的正文滚动容器提供短缓出、可累积、可反向的滚轮平滑滚动。
/// </summary>
/// <remarks>
/// <para>
/// 该行为是<em>局部启用</em>的：只有显式设置附加属性的 <see cref="ScrollViewer"/> 会收到轮询处理，
/// 不使用类处理器，因此不会影响设置页、下拉框、多行文本框或任何嵌套滚动容器。
/// </para>
/// <para>
/// 使用视图所属 Dispatcher 的短时定时器推进真实偏移，避免快速重定向时动画时钟停更。
/// 通过 <see cref="ScrollViewer.ScrollToVerticalOffset"/> 滚动，真实偏移、
/// 滚动条位置、命中测试与可访问性因此保持与原生滚动一致（不使用 RenderTransform 伪造）。
/// </para>
/// <para>
/// 每次滚轮输入都从<em>当前实际位置</em>重新定向：同方向连续输入累积目标，反方向输入立即生效，
/// 不会排队播放多个动画，也不附加第二套长惯性。系统关闭动画时退化为即时滚动。
/// </para>
/// </remarks>
public static class SmoothScrollBehavior
{
    /// <summary>每行滚轮对应的初始 DIP 换算值。</summary>
    private const double DipsPerLine = 24;

    /// <summary>一个标准滚轮刻度。</summary>
    private const double WheelNotch = 120;

    /// <summary>小于该值的位置变化不再写入，避免动画帧造成无意义的滚动调用。</summary>
    private const double MinimumOffsetChange = 0.05;

    private static readonly TimeSpan GlideDuration = TimeSpan.FromMilliseconds(160);
    private static readonly KeySpline GlideSpline = CreateGlideSpline();

    private static KeySpline CreateGlideSpline()
    {
        var spline = new KeySpline(0.215, 0.61, 0.355, 1);
        spline.Freeze();
        return spline;
    }

    /// <summary>在指定滚动容器上启用平滑滚动。</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    /// <summary>设置是否启用平滑滚动。</summary>
    public static void SetEnabled(DependencyObject element, bool value) =>
        (element ?? throw new ArgumentNullException(nameof(element))).SetValue(EnabledProperty, value);

    /// <summary>返回是否启用平滑滚动。</summary>
    public static bool GetEnabled(DependencyObject element) =>
        (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(EnabledProperty);

    internal static void ScrollTo(ScrollViewer viewer, double offset, bool animate)
    {
        SetEnabled(viewer, true);
        if (viewer.GetValue(StateProperty) is ScrollGlide glide) glide.NavigateTo(offset, animate);
    }

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(ScrollGlide),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(null));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer) return;
        if (e.NewValue is true) Attach(viewer);
        else Detach(viewer);
    }

    private static void Attach(ScrollViewer viewer)
    {
        if (viewer.GetValue(StateProperty) is ScrollGlide) return;

        var glide = new ScrollGlide(viewer);
        viewer.SetValue(StateProperty, glide);
        viewer.PreviewMouseWheel += glide.OnPreviewMouseWheel;
        viewer.ScrollChanged += glide.OnScrollChanged;
        viewer.PreviewKeyDown += glide.OnPreviewKeyDown;
        viewer.PreviewMouseDown += glide.OnPreviewMouseDown;
        viewer.Unloaded += glide.OnUnloaded;
    }

    private static void Detach(ScrollViewer viewer)
    {
        if (viewer.GetValue(StateProperty) is not ScrollGlide glide) return;

        viewer.PreviewMouseWheel -= glide.OnPreviewMouseWheel;
        viewer.ScrollChanged -= glide.OnScrollChanged;
        viewer.PreviewKeyDown -= glide.OnPreviewKeyDown;
        viewer.PreviewMouseDown -= glide.OnPreviewMouseDown;
        viewer.Unloaded -= glide.OnUnloaded;
        glide.Release();
        viewer.ClearValue(StateProperty);
    }

    private static bool AnimationsEnabled()
    {
        try
        {
            // 对应“在 Windows 中显示动画”的辅助功能设置。
            return SystemParameters.ClientAreaAnimation;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// 把一个滚轮刻度换算成“偏移变化量”。
    /// </summary>
    /// <remarks>
    /// <paramref name="delta"/> 为正表示滚轮远离用户，文档应当向上走，因此偏移<em>减小</em>：返回值带负号。
    /// 每行按 <see cref="DipsPerLine"/> 换算，<paramref name="wheelScrollLines"/> 为 0 时禁用滚轮，负值时按
    /// “一次滚动一个屏幕”处理。小于一个刻度的 Delta 按比例保留，不取整成整数刻度。
    /// </remarks>
    internal static double WheelStepDips(int delta, int wheelScrollLines, double viewportHeight)
    {
        double notches = (double)delta / WheelNotch;
        double distance = wheelScrollLines < 0
            ? notches * Math.Max(viewportHeight * 0.9, DipsPerLine)
            : notches * wheelScrollLines * DipsPerLine;
        return -distance;
    }

    /// <summary>
    /// 计算下一个动画目标：同方向持续输入累积目标，反方向输入以<em>当前实际位置</em>重新定向，
    /// 因此不会先走完旧方向，也不会排队播放多个动画。结果始终落在可滚动范围内。
    /// </summary>
    internal static double NextTarget(
        double currentOffset,
        double pendingTarget,
        bool animating,
        int pendingDirection,
        double step,
        double maximum)
    {
        int direction = Math.Sign(step);
        double target = animating && direction != 0 && direction == pendingDirection
            ? pendingTarget + step
            : currentOffset + step;
        return Math.Clamp(target, 0, Math.Max(0, maximum));
    }

    /// <summary>
    /// 在给定偏移下，这个方向是否还有内容可以滚。
    /// </summary>
    /// <remarks>Delta 为负代表向下滚动（需要尚未到底），为正在于向上滚动（需要偏移大于 0）。</remarks>
    internal static bool CanScrollFurther(double offset, double scrollableHeight, int delta) =>
        delta < 0 ? offset < scrollableHeight : offset > 0;

    /// <summary>命中元素自身存在可继续滚动的祖先滚动容器时，把滚轮交给它处理。</summary>
    private static bool ShouldDeferToInnerScroller(DependencyObject? source, ScrollViewer outer, int delta)
    {
        for (DependencyObject? node = source; node is not null; node = GetParent(node))
        {
            if (ReferenceEquals(node, outer)) return false;
            if (node is not ScrollViewer inner) continue;
            if (CanScrollFurther(inner.VerticalOffset, inner.ScrollableHeight, delta)) return true;
        }
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject node)
    {
        // 文本内容元素（如 Run）不属于可视树，需要按内容模型向上走。
        if (node is ContentElement content)
            return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        return LogicalTreeHelper.GetParent(node);
    }

    /// <summary>每个启用平滑滚动的容器持有一份状态。</summary>
    private sealed class ScrollGlide
    {
        private readonly ScrollViewer _viewer;
        private bool _animating;
        private readonly DispatcherTimer _timer;
        private double _startOffset;
        private long _startedAt;
        private readonly System.Collections.Generic.Queue<double> _requestedOffsets = new();
        private double _target;
        private int _direction;

        internal ScrollGlide(ScrollViewer viewer)
        {
            _viewer = viewer;
            _target = viewer.VerticalOffset;
            _timer = new DispatcherTimer(DispatcherPriority.Render, viewer.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _timer.Tick += OnGlideTick;
        }

        internal void Release()
        {
            Cancel();
            _timer.Tick -= OnGlideTick;
        }

        internal void OnUnloaded(object sender, RoutedEventArgs e) => Cancel();

        internal void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => Cancel();

        internal void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 键盘滚动由原生滚动容器直接完成，这里只负责取消残留的滚轮动画。
            switch (e.Key)
            {
                case Key.Home:
                case Key.End:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Up:
                case Key.Down:
                case Key.Space:
                    Cancel();
                    break;
            }
        }

        internal void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, _viewer)) return;

            // ScrollToVerticalOffset 排队后才触发通知，不能用调用栈内的布尔标志判断来源。
            if (e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0 &&
                IsRequestedOffset(e.VerticalOffset)) return;

            // 滚动条拖动、键盘、程序化滚动或内容高度变化都会走到这里：
            // 取消残留动画并以新的实际位置重新作为起点。
            Cancel();
            _target = _viewer.VerticalOffset;

            // 内容变短后目标必须落在新的可滚动范围内。
            double maximum = Math.Max(0, _viewer.ScrollableHeight);
            if (_target > maximum)
            {
                _target = maximum;
                Apply(maximum);
            }
        }

        internal void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || e.Delta == 0) return;

            if (ShouldDeferToInnerScroller(e.OriginalSource as DependencyObject, _viewer, e.Delta)) return;

            double step = WheelStepDips(e.Delta, SystemParameters.WheelScrollLines, _viewer.ViewportHeight);
            if (Math.Abs(step) < MinimumOffsetChange) return;

            double current = _viewer.VerticalOffset;
            double maximum = Math.Max(0, _viewer.ScrollableHeight);
            int direction = Math.Sign(step);

            double target = NextTarget(current, _target, _animating, _direction, step, maximum);

            if (Math.Abs(target - current) < 0.5)
            {
                // 已经在边界上：不吞掉事件，让外层滚动容器按原生规则接手。
                Cancel();
                _target = current;
                return;
            }

            e.Handled = true;
            _target = target;
            _direction = direction;

            StartGlide(current, target, animate: true);
        }

        internal void NavigateTo(double offset, bool animate)
        {
            double current = _viewer.VerticalOffset;
            _target = Math.Clamp(offset, 0, Math.Max(0, _viewer.ScrollableHeight));
            _direction = Math.Sign(_target - current);
            StartGlide(current, _target, animate);
        }

        private void StartGlide(double current, double target, bool animate)
        {
            if (!animate || !AnimationsEnabled() || Math.Abs(target - current) < MinimumOffsetChange)
            {
                Cancel();
                Apply(target, force: true);
                return;
            }

            _animating = true;
            _startOffset = current;
            _startedAt = Stopwatch.GetTimestamp();
            _timer.Start();
        }

        private void OnGlideTick(object? sender, EventArgs e)
        {
            double progress = Math.Clamp(Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds / GlideDuration.TotalMilliseconds, 0, 1);
            Apply(_startOffset + (_target - _startOffset) * GlideSpline.GetSplineProgress(progress), force: progress >= 1);
            if (progress >= 1)
            {
                _animating = false;
                _timer.Stop();
            }
        }

        private void Apply(double value, bool force = false)
        {
            if (!force && Math.Abs(value - _viewer.VerticalOffset) < MinimumOffsetChange) return;
            _requestedOffsets.Enqueue(value);
            _viewer.ScrollToVerticalOffset(value);
        }

        private bool IsRequestedOffset(double value)
        {
            // 连续输入会在同一轮布局前排入多个偏移；WPF 可以合并命令，通知也可能晚一帧。
            while (_requestedOffsets.TryDequeue(out double requested))
                if (Math.Abs(value - requested) < MinimumOffsetChange) return true;
            return false;
        }

        private void Cancel()
        {
            _animating = false;
            _timer.Stop();
            _requestedOffsets.Clear();
        }
    }

}
