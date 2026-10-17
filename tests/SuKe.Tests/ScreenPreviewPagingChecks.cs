using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.ExceptionServices;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;
using SuKe.UI;

namespace SuKe.Tests;

internal static class ScreenPreviewPagingChecks
{
    internal static void VerifyStartup(WorkspaceSnapshot workspace, WorkspacePreviewStore previews, Window owner)
    {
        var dialog = new StartupWorkspaceDialog([workspace], previews) { Owner = owner };
        try
        {
            dialog.Show();
            Pump();
            Border[] frames = Walk(dialog).OfType<Border>()
                .Where(frame => Equals(frame.ToolTip, ScreenPreviewText.ZoomHint)).ToArray();
            Assert.Equal(previews.Load(workspace).Count, frames.Length);
            for (int index = 0; index < frames.Length; index++)
            {
                Exception? failure = null;
                bool opened = false;
                int selected = index;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
                timer.Tick += (_, _) =>
                {
                    ScreenPreviewViewerWindow? viewer = Application.Current.Windows.OfType<ScreenPreviewViewerWindow>().FirstOrDefault();
                    if (viewer is null) return;
                    timer.Stop();
                    opened = true;
                    try { Verify(viewer, frames.Length, selected); }
                    catch (Exception ex) { failure = ex; }
                    finally { viewer.Close(); }
                };
                timer.Start();
                try
                {
                    frames[index].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    });
                    Assert.True(opened);
                    if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
                }
                finally { timer.Stop(); }
                Assert.Null(dialog.SelectedWorkspace);
            }
        }
        finally { dialog.Close(); }
    }

    internal static void Verify(ScreenPreviewViewerWindow viewer, int count, int selectedIndex)
    {
        viewer.WindowState = WindowState.Maximized;
        Pump();
        var previous = Find<Button>(viewer, "ScreenPreviewPrevious");
        var next = Find<Button>(viewer, "ScreenPreviewNext");
        var indicator = Find<TextBlock>(viewer, "ScreenPreviewPageNumber");
        Assert.Equal($"{selectedIndex + 1} / {count}", indicator.Text);
        Assert.Equal(selectedIndex > 0, previous.IsEnabled);
        Assert.Equal(selectedIndex + 1 < count, next.IsEnabled);

        // 翻页按钮必须落在图片左右两侧，而不是底部工具条里。
        VerifySidePlacement(viewer, previous, next);
        while (previous.IsEnabled) Click(previous);
        Assert.Equal($"1 / {count}", indicator.Text);
        Assert.False(previous.IsEnabled);
        if (count > 1)
        {
            var image = Walk(viewer).OfType<Image>().Single();
            ImageSource first = image.Source;
            Press(viewer, Key.Right);
            Assert.Equal($"2 / {count}", indicator.Text);
            Assert.NotSame(first, image.Source);
            Press(viewer, Key.PageUp);
            Assert.Same(first, image.Source);
            Press(viewer, Key.PageDown);
            Assert.Equal($"2 / {count}", indicator.Text);
            Press(viewer, Key.Left);
            Assert.Equal($"1 / {count}", indicator.Text);

            Button actualSize = Walk(viewer).OfType<Button>()
                .Single(button => Equals(button.Content, Loc.T("Actual size")));
            Click(actualSize);
            Assert.Equal(Stretch.None, image.Stretch);
            Click(next);
            Assert.Equal(Stretch.None, image.Stretch);
            Button fit = Walk(viewer).OfType<Button>()
                .Single(button => Equals(button.Content, Loc.T("Fit to window")));
            Click(fit);
            Assert.Equal(Stretch.Uniform, image.Stretch);
        }
        while (next.IsEnabled) Click(next);
        string last = indicator.Text;
        Press(viewer, Key.Right);
        Assert.Equal(last, indicator.Text);
        Assert.False(next.IsEnabled);
        Assert.Equal(WindowState.Maximized, viewer.WindowState);
    }

    private static T Find<T>(DependencyObject root, string id) where T : DependencyObject =>
        Walk(root).OfType<T>().Single(item => AutomationProperties.GetAutomationId(item) == id);

    /// <summary>
    /// Asserts the paging affordances really sit on the left and right of the picture, and that they are
    /// still reachable once the window is maximized.
    /// </summary>
    /// <remarks>
    /// Placement is checked against the image viewport instead of the <see cref="Image"/> element: in
    /// actual-size mode that element is larger than what is visible, so its layout rectangle is not the
    /// picture the user sees. Vertical position is compared with the viewport centre, which is what rules
    /// out the previous bottom-toolbar layout.
    /// </remarks>
    private static void VerifySidePlacement(ScreenPreviewViewerWindow viewer, Button previous, Button next)
    {
        Image image = Walk(viewer).OfType<Image>().Single();
        ScrollViewer? viewport = null;
        for (DependencyObject? node = VisualTreeHelper.GetParent(image);
             node is not null && viewport is null;
             node = VisualTreeHelper.GetParent(node))
        {
            viewport = node as ScrollViewer;
        }
        Assert.NotNull(viewport);

        Rect viewRect = Bounds(viewport, viewer);
        Rect imageRect = Bounds(image, viewer);
        Rect prevRect = Bounds(previous, viewer);
        Rect nextRect = Bounds(next, viewer);
        Assert.True(imageRect.Width > 0 && imageRect.Height > 0,
            $"The picture must be laid out. Image={imageRect}.");
        Assert.True(prevRect.Width >= 32 && prevRect.Height >= 32,
            $"The previous button must be a real target. Button={prevRect}.");
        Assert.True(nextRect.Width >= 32 && nextRect.Height >= 32,
            $"The next button must be a real target. Button={nextRect}.");

        // 左右两侧：上一张完全在画面左边，下一张完全在画面右边，两者都不压住图片。
        Assert.True(prevRect.Right <= viewRect.Left + 0.5,
            $"The previous button must sit left of the picture. Button={prevRect}, Viewport={viewRect}.");
        Assert.True(nextRect.Left >= viewRect.Right - 0.5,
            $"The next button must sit right of the picture. Button={nextRect}, Viewport={viewRect}.");

        // 垂直方向与画面同高居中；底部工具条方案会让它们落在视口下方。
        double viewCentre = (viewRect.Top + viewRect.Bottom) / 2;
        double tolerance = Math.Max(8, viewRect.Height * 0.06);
        Assert.True(Math.Abs((prevRect.Top + prevRect.Bottom) / 2 - viewCentre) <= tolerance,
            $"The previous button must be vertically centred on the picture. Button={prevRect}, Viewport={viewRect}.");
        Assert.True(Math.Abs((nextRect.Top + nextRect.Bottom) / 2 - viewCentre) <= tolerance,
            $"The next button must be vertically centred on the picture. Button={nextRect}, Viewport={viewRect}.");

        // 最大化后按钮仍在窗口内，并且是那个位置上最顶层的元素（没有被图片或其它内容盖住）。
        foreach (Button button in new[] { previous, next })
        {
            Rect rect = Bounds(button, viewer);
            var centre = new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));
            Assert.True(centre.X >= 0 && centre.Y >= 0
                        && centre.X <= viewer.ActualWidth && centre.Y <= viewer.ActualHeight,
                $"{AutomationProperties.GetName(button)} must stay inside the maximized window. " +
                $"Centre={centre}, Window={viewer.ActualWidth}x{viewer.ActualHeight}.");
            // WPF 的输入命中会跳过禁用按钮；边界按钮仍需验证没有被其它视觉元素遮挡。
            DependencyObject? hit = button.IsEnabled
                ? viewer.InputHitTest(centre) as DependencyObject
                : VisualTreeHelper.HitTest(viewer, centre)?.VisualHit;
            Assert.True(IsWithin(hit, button),
                $"{AutomationProperties.GetName(button)} must be the topmost element at its own centre " +
                $"(pointer at {centre} on a {viewer.ActualWidth}x{viewer.ActualHeight} window; " +
                $"hit={(viewer.InputHitTest(centre) as DependencyObject)?.GetType().Name ?? "null"}; " +
                $"hitChain=[{Chain(viewer.InputHitTest(centre) as DependencyObject)}]; " +
                $"buttonRect={rect}; visible={button.IsVisible} enabled={button.IsEnabled} " +
                $"hitTestVisible={button.IsHitTestVisible}).");
        }
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            if (node is FrameworkContentElement content)
            {
                node = content.Parent;
                continue;
            }
            if (node is not (Visual or System.Windows.Media.Media3D.Visual3D)) return false;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    /// <summary>诊断用：把命中元素到根的视觉树链渲染成一行文本。</summary>
    private static string Chain(DependencyObject? node)
    {
        var parts = new List<string>();
        while (node is not null)
        {
            parts.Add(node.GetType().Name);
            if (node is not (Visual or System.Windows.Media.Media3D.Visual3D)) break;
            node = VisualTreeHelper.GetParent(node);
        }
        return string.Join(" < ", parts);
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump();
    }

    private static void Press(Window window, Key key)
    {
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window), Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        Pump();
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject nested in Walk(child)) yield return nested;
        }
    }
}
