using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuKe.UI;

namespace SuKe.Tests;

/// <summary>
/// 放大查看器的真实 WPF 行为：打开即最大化、每张截图都铺满视口、滚轮缩放并以百分比反馈。
/// </summary>
[Collection("WPF presentation")]
public class ScreenPreviewViewerUiTests
{
    [Fact]
    public void Viewer_opens_maximized_fills_every_page_and_zooms_with_the_wheel()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            // 与真实截图同尺寸（2560×1440）：查看器的「铺满」必须按原始像素计算，而不是缩略图尺寸。
            ScreenPreviewPage[] pages = CreatePages();
            var viewer = new ScreenPreviewViewerWindow(pages, 0) { ShowInTaskbar = false };
            // FluentWindow 的模板来自 WPF-UI 主题字典；不合并就不会生成可视树。
            viewer.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
            try
            {
                viewer.Show();
                SmoothScrollUiTests.Pump(150);

                Assert.Equal(WindowState.Maximized, viewer.WindowState);

                Image image = ActivityDetailsUiTests.Walk(viewer).OfType<Image>().Single();
                ScrollViewer viewport = Ancestor<ScrollViewer>(image)!;

                // 第一张就铺满；翻页后第二张同样铺满（不是停在上一张的尺寸/倍数）。
                ScreenPreviewPagingChecks.AssertFillsViewport(viewport, image);
                TextBlock zoomLevel = ActivityDetailsUiTests.Walk(viewer).OfType<TextBlock>()
                    .Single(text => AutomationProperties.GetAutomationId(text) == "ScreenPreviewZoomLevel");
                string fitLabel = zoomLevel.Text;
                Assert.EndsWith("%", fitLabel);

                double fitWidth = image.Width;
                Wheel(viewport, 120);
                SmoothScrollUiTests.Pump(60);
                Assert.True(image.Width > fitWidth + 1,
                    $"向上滚轮应放大：{fitWidth:F1} → {image.Width:F1}。");
                Assert.NotEqual(fitLabel, zoomLevel.Text);

                // 连续向下滚能让画面小于「适应窗口」，说明是连续缩放而不是两档切换。
                Wheel(viewport, -480);
                SmoothScrollUiTests.Pump(60);
                Assert.True(image.Width < fitWidth,
                    $"向下滚轮应缩小到适应窗口以下：{fitWidth:F1} → {image.Width:F1}。");

                Press(viewer, Key.Right);
                SmoothScrollUiTests.Pump(60);
                Assert.Equal(fitLabel, zoomLevel.Text);
                ScreenPreviewPagingChecks.AssertFillsViewport(viewport, image);
            }
            finally { viewer.Close(); }
        });
    }

    /// <summary>
    /// 翻页、侧边按钮与实际大小/适应窗口的既有约定，在最大化窗口上仍然成立。
    /// </summary>
    /// <remarks>
    /// 这段原本只在 <c>ShellUiTests</c> 里跑，而该宿主因加载 App.xaml BAML 失败长期无法执行；
    /// 把同一套检查挂到独立窗口上，改成缩放模型后才能真的验证到。
    /// </remarks>
    [Fact]
    public void Existing_paging_affordances_still_hold_on_a_maximized_window()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            ScreenPreviewPage[] pages = CreatePages();
            var viewer = new ScreenPreviewViewerWindow(pages, 1) { ShowInTaskbar = false };
            viewer.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
            try
            {
                viewer.Show();
                SmoothScrollUiTests.Pump(150);
                ScreenPreviewPagingChecks.Verify(viewer, pages.Length, 1);
            }
            finally { viewer.Close(); }
        });
    }

    private static ScreenPreviewPage[] CreatePages() =>
        new[] { Colors.Red, Colors.Green, Colors.Blue }
            .Select((color, index) => new ScreenPreviewPage(
                new DrawingImage(new GeometryDrawing(
                    new SolidColorBrush(color),
                    null,
                    new RectangleGeometry(new Rect(0, 0, 2560, 1440)))),
                $"Screen {index + 1}",
                null))
            .ToArray();

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
    }

    private static void Press(Window window, Key key)
    {
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window), Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
    }
}
