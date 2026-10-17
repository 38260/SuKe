using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SuKe.UI.Shell;

namespace SuKe.Tests;

/// <summary>驱动真实 WPF 布局和动画，避免只验证滚轮距离计算而遗漏事件时序。</summary>
[Collection("WPF presentation")]
public class SmoothScrollUiTests
{
    [Fact]
    public void Wheel_input_reaches_its_target_after_asynchronous_scroll_notifications()
    {
        RunSta(() =>
        {
            var content = new Border { Height = 2400 };
            var viewer = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SmoothScrollBehavior.SetEnabled(viewer, true);
            var window = new Window { Content = viewer, Width = 400, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(40);
                Assert.True(viewer.ScrollableHeight > 1000);
                var offsets = new List<double>();
                viewer.ScrollChanged += (_, e) =>
                {
                    if (ReferenceEquals(e.OriginalSource, viewer) && e.VerticalChange != 0)
                        offsets.Add(viewer.VerticalOffset);
                };
                double step = SmoothScrollBehavior.WheelStepDips(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
                Assert.True(Wheel(content, -120).Handled);
                Pump(350);
                Assert.True(Math.Abs(viewer.VerticalOffset - step) < 1,
                    $"Expected {step}, actual {viewer.VerticalOffset}; samples: {string.Join(", ", offsets)}");
                Assert.True(offsets.Zip(offsets.Skip(1), (a, b) => b >= a - 0.1).All(x => x),
                    "Animation must not snap back to the beginning.");

                viewer.ScrollToVerticalOffset(500);
                Pump(50);
                Assert.Equal(500, viewer.VerticalOffset, 1);
                Wheel(content, -120);
                Pump(350);
                Assert.Equal(500 + step, viewer.VerticalOffset, 1);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Pointer_and_programmatic_scroll_take_over_without_snapping_to_an_old_animation()
    {
        RunSta(() =>
        {
            var content = new Border { Height = 2400 };
            var viewer = new ScrollViewer { Content = content };
            SmoothScrollBehavior.SetEnabled(viewer, true);
            var window = new Window { Content = viewer, Width = 400, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(40);
                Wheel(content, -120);
                Pump(50);
                var pointer = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseDownEvent,
                };
                content.RaiseEvent(pointer);
                Assert.False(pointer.Handled);
                viewer.ScrollToVerticalOffset(300);
                Pump(300);
                Assert.Equal(300, viewer.VerticalOffset, 1);

                Wheel(content, -120);
                Pump(40);
                viewer.ScrollToVerticalOffset(600);
                Pump(300);
                Assert.Equal(600, viewer.VerticalOffset, 1);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Continuous_wheel_and_small_reverse_input_retarget_from_the_visible_position()
    {
        RunSta(() =>
        {
            var content = new Border { Height = 2400 };
            var viewer = new ScrollViewer { Content = content };
            SmoothScrollBehavior.SetEnabled(viewer, true);
            var window = new Window { Content = viewer, Width = 400, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(40);
                viewer.ScrollToVerticalOffset(500);
                Pump(40);
                var samples = new List<double>();
                viewer.ScrollChanged += (_, e) =>
                {
                    if (ReferenceEquals(e.OriginalSource, viewer) && e.VerticalChange != 0)
                        samples.Add(viewer.VerticalOffset);
                };
                double step = SmoothScrollBehavior.WheelStepDips(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
                Wheel(content, -120);
                Pump(30);
                double firstVisible = viewer.VerticalOffset;
                Wheel(content, -120);
                Wheel(content, -120);
                Pump(300);
                Assert.True(Math.Abs(500 + step * 3 - viewer.VerticalOffset) < 0.1,
                    $"Expected {500 + step * 3}, actual {viewer.VerticalOffset}; samples: {string.Join(", ", samples)}");
                Assert.True(samples.All(offset => offset >= 499.9));
                Assert.True(samples.Zip(samples.Skip(1), (a, b) => b >= a - 0.1).All(x => x));
                Assert.True(firstVisible > 500);

                Wheel(content, -120);
                Pump(45);
                double reversalStart = viewer.VerticalOffset;
                samples.Clear();
                Wheel(content, 40);
                Pump(300);
                double reverseStep = SmoothScrollBehavior.WheelStepDips(40, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
                Assert.Equal(reversalStart + reverseStep, viewer.VerticalOffset, 1);
                Assert.True(samples.All(offset => offset <= reversalStart + 0.1));
                Assert.True(samples.Zip(samples.Skip(1), (a, b) => b <= a + 0.1).All(x => x));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Inner_scrolling_does_not_cancel_the_outer_glide_and_wheel_is_deferred_to_the_inner_viewer()
    {
        RunSta(() =>
        {
            var innerContent = new Border { Height = 800 };
            var inner = new ScrollViewer { Content = innerContent, Height = 120 };
            var tail = new Border { Height = 2400 };
            var panel = new StackPanel();
            panel.Children.Add(inner);
            panel.Children.Add(tail);
            var outer = new ScrollViewer { Content = panel };
            SmoothScrollBehavior.SetEnabled(outer, true);
            var window = new Window { Content = outer, Width = 400, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(40);
                Assert.False(Wheel(innerContent, -120).Handled);
                Assert.Equal(0, outer.VerticalOffset);
                Wheel(tail, -120);
                Pump(30);
                inner.ScrollToVerticalOffset(40);
                Pump(300);
                double step = SmoothScrollBehavior.WheelStepDips(-120, SystemParameters.WheelScrollLines, outer.ViewportHeight);
                Assert.Equal(step, outer.VerticalOffset, 1);
                Assert.Equal(40, inner.VerticalOffset, 1);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Page_reload_keeps_the_wheel_working_and_boundary_input_is_not_swallowed()
    {
        RunSta(() =>
        {
            var content = new Border { Height = 2400 };
            var viewer = new ScrollViewer { Content = content };
            SmoothScrollBehavior.SetEnabled(viewer, true);
            var window = new Window { Content = viewer, Width = 400, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(40);
                Assert.False(Wheel(content, 120).Handled);
                Wheel(content, -120);
                Pump(30);
                window.Content = null;
                Pump(30);
                window.Content = viewer;
                Pump(40);
                double start = viewer.VerticalOffset;
                double step = SmoothScrollBehavior.WheelStepDips(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
                Wheel(content, -120);
                Pump(300);
                Assert.Equal(start + step, viewer.VerticalOffset, 1);
                viewer.ScrollToBottom();
                Pump(40);
                Assert.False(Wheel(content, -120).Handled);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>设置页左侧目录把滚轮转给正文：目录保持不动，正文按一格推进。</summary>
    [Fact]
    public void Forwarded_wheel_scrolls_the_content_and_leaves_the_category_rail_still()
    {
        RunSta(() =>
        {
            var categoryContent = new StackPanel { Height = 320 };
            var categories = new ScrollViewer
            {
                Content = categoryContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Width = 120,
            };
            var article = new Border { Height = 2400 };
            var content = new ScrollViewer
            {
                Content = article,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            SmoothScrollBehavior.ForwardWheel(categories, content);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(categories, 0);
            Grid.SetColumn(content, 1);
            grid.Children.Add(categories);
            grid.Children.Add(content);

            var window = new Window { Content = grid, Width = 420, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(60);
                Assert.True(content.ScrollableHeight > 1000);
                double step = SmoothScrollBehavior.WheelStepDips(
                    -120, SystemParameters.WheelScrollLines, content.ViewportHeight);

                Assert.True(Wheel(categories, -120).Handled, "目录上的滚轮应由转发逻辑接管。");
                Pump(350);

                Assert.Equal(step, content.VerticalOffset, 1);
                Assert.Equal(0, categories.VerticalOffset, 1);
            }
            finally { window.Close(); }
        });
    }


    internal static MouseWheelEventArgs Wheel(UIElement source, int delta)
    {
        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
        };
        source.RaiseEvent(args);
        return args;
    }

    internal static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    internal static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF scroll verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

/// <summary>WPF 主题缓存和呈现时钟涉及进程级状态，界面测试独占运行以免相互干扰。</summary>
[CollectionDefinition("WPF presentation", DisableParallelization = true)]
public sealed class PresentationUiCollection { }
