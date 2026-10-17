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
        // 点开就是最大化：截图本身是整屏分辨率，小窗口里只能看到缩略效果。
        Assert.Equal(WindowState.Maximized, viewer.WindowState);
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
            // 每张图都从「铺满视口」开始：点开的那张如此，翻到的下一张也如此。
            Image image = Walk(viewer).OfType<Image>().Single();
            ScrollViewer viewport = Ancestor<ScrollViewer>(image)!;
            ImageSource first = image.Source;
            AssertFillsViewport(viewport, image);

            // 键盘翻页保持原语义：右/下前进，左/上后退，首尾停止。
            Press(viewer, Key.Right);
            Assert.Equal($"2 / {count}", indicator.Text);
            Assert.NotSame(first, image.Source);
            AssertFillsViewport(viewport, image);
            Press(viewer, Key.PageUp);
            Assert.Same(first, image.Source);
            Press(viewer, Key.PageDown);
            Assert.Equal($"2 / {count}", indicator.Text);
            Press(viewer, Key.Left);
            Assert.Equal($"1 / {count}", indicator.Text);

            // 实际大小是 1:1，不是「铺满」。
            Button actualSize = Walk(viewer).OfType<Button>()
                .Single(button => Equals(button.Content, Loc.T("Actual size")));
            Click(actualSize);
            Assert.Equal(image.Source.Width, image.Width, 1);
            Assert.Equal(image.Source.Height, image.Height, 1);
            Assert.Contains(Walk(viewer).OfType<Button>(),
                button => Equals(button.Content, Loc.T("Fit to window")));

            // 滚轮就是缩放：向上放大、向下缩小，比例直接体现在画面宽度上。
            double actualWidth = image.Width;
            Wheel(viewport, 120);
            Assert.True(image.Width > actualWidth,
                $"向上滚轮应放大：实际大小 {actualWidth}，滚动后 {image.Width}。");
            Wheel(viewport, -240);
            Assert.True(image.Width < actualWidth,
                $"向下滚轮应缩小：实际大小 {actualWidth}，滚动后 {image.Width}。");

            // 翻页回到铺满，不沿用上一张的滚轮倍数。
            Click(next);
            AssertFillsViewport(viewport, image);
            Assert.Contains(Walk(viewer).OfType<Button>(),
                button => Equals(button.Content, Loc.T("Actual size")));
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

    /// <summary>图片必须铺满视口的一条边，另一条边不溢出——这就是「每张图都最大化」的判据。</summary>
    internal static void AssertFillsViewport(ScrollViewer viewport, Image image)
    {
        double viewportWidth = viewport.ViewportWidth;
        double viewportHeight = viewport.ViewportHeight;
        Assert.True(viewportWidth > 0 && viewportHeight > 0,
            $"视口必须有尺寸，实际 {viewportWidth:F1}×{viewportHeight:F1}。");

        double widthRatio = image.Width / viewportWidth;
        double heightRatio = image.Height / viewportHeight;
        Assert.True(Math.Abs(Math.Max(widthRatio, heightRatio) - 1) <= 0.02,
            $"图片应铺满视口的一条边：图片 {image.Width:F1}×{image.Height:F1}，" +
            $"视口 {viewportWidth:F1}×{viewportHeight:F1}。");
        Assert.True(widthRatio <= 1.02 && heightRatio <= 1.02,
            $"图片不应超出视口：图片 {image.Width:F1}×{image.Height:F1}，" +
            $"视口 {viewportWidth:F1}×{viewportHeight:F1}。");
    }

    private static T? Ancestor<T>(DependencyObject node) where T : DependencyObject
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(node);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match) return match;
        }
        return null;
    }

    private static void Wheel(UIElement target, int delta)
    {
        target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
        });
        Pump();
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
