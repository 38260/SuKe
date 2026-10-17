using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SuKe.Localization;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using TitleBar = Wpf.Ui.Controls.TitleBar;

namespace SuKe.UI;

/// <summary>
/// Enlarged, read-only view of one saved screen screenshot.
/// </summary>
/// <remarks>
/// The viewer is deliberately opened from a single click on any preview thumbnail, so it stays
/// cheap and predictable: it owns one bitmap that the caller already decoded, never reloads the
/// preview archive, and cannot change anything about the workspace it came from. Escape closes it.
/// <para>
/// 打开即最大化：截图本身就是整屏分辨率，放在 1180×760 的窗口里只能看到缩略效果。
/// 每张图都按视口等比例铺满（<see cref="ScreenPreviewZoom"/>），滚轮以指针为中心缩放，
/// 放大后可以直接拖动画面平移。窗口尺寸变化（最大化/还原）后会重新铺满，不会停在旧尺寸上。
/// </para>
/// </remarks>
internal sealed partial class ScreenPreviewViewerWindow : FluentWindow
{
    private readonly ScrollViewer _scroll;
    private readonly Image _image;
    private readonly TextBlock _zoomLevel;
    private readonly Wpf.Ui.Controls.Button _zoomToggle;

    private ScreenPreviewZoom _zoom = ScreenPreviewZoom.Fit;
    private bool _applyingZoom;
    private bool _dragging;
    private Point _dragOrigin;
    private double _dragOffsetX;
    private double _dragOffsetY;

    internal ScreenPreviewViewerWindow(
        ImageSource image,
        string title,
        string? subtitle = null,
        Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        Title = string.IsNullOrWhiteSpace(title) ? Loc.T("Screen preview") : title;
        Owner = owner;
        Width = 1180;
        Height = 760;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        // 点开即铺满整块屏幕；还原后回到构造尺寸，图片同样会重新填满较小的视口。
        WindowState = WindowState.Maximized;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TitleBar
        {
            Title = Title,
            ShowMinimize = false,
            ShowMaximize = true
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleText = new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Margin = new Thickness(20, 10, 20, 0),
                TextWrapping = TextWrapping.Wrap
            };
            // A monitor name and resolution are device data, not catalog copy.
            Loc.SetSkip(subtitleText, true);
            Grid.SetRow(subtitleText, 1);
            root.Children.Add(subtitleText);
        }

        _image = new Image
        {
            Source = image,
            // 尺寸由 ApplyZoom 精确给出且始终与图像同比例，Fill 只是把它铺在计算出的框里。
            Stretch = Stretch.Fill,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Cursor = Cursors.Arrow
        };
        _image.MouseLeftButtonDown += OnImageLeftButtonDown;
        _image.MouseMove += OnImageMouseMove;
        _image.MouseLeftButtonUp += OnImageLeftButtonUp;
        _image.LostMouseCapture += (_, _) => _dragging = false;

        _scroll = new ScrollViewer
        {
            Content = _image,
            // 左右留窄边：翻页按钮现在各占图片侧边一列，图片自身的横向边距只需一圈窄内衬。
            Margin = new Thickness(12, 12, 12, 12),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = false
        };
        // 视口变化（最大化、还原、拖窗口边缘）都要重新铺满，否则会停在旧尺寸上。
        _scroll.SizeChanged += (_, _) => ApplyZoom();
        _scroll.ScrollChanged += (_, e) =>
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) ApplyZoom();
        };
        // 滚轮就是缩放：查看器的目的是看清截图细节，滚动交给拖动与滚动条。
        _scroll.PreviewMouseWheel += OnPreviewMouseWheel;
        Grid.SetRow(_scroll, 2);
        root.Children.Add(_scroll);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 16)
        };

        _zoomLevel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            MinWidth = 52,
            FontSize = 13,
            TextAlignment = TextAlignment.Right
        };
        AutomationProperties.SetAutomationId(_zoomLevel, "ScreenPreviewZoomLevel");
        actions.Children.Add(_zoomLevel);

        _zoomToggle = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T("Actual size"),
            MinWidth = 128,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _zoomToggle.Click += (_, _) =>
        {
            if (_zoom.IsFit) SetActualSize();
            else SetFit();
        };
        actions.Children.Add(_zoomToggle);

        var close = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T("Close"),
            MinWidth = 88,
            IsDefault = true
        };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key is Key.D0 or Key.NumPad0)
            {
                e.Handled = true;
                SetFit();
            }
            else if (e.Key is Key.D1 or Key.NumPad1)
            {
                e.Handled = true;
                SetActualSize();
            }
        };

        Content = root;
        ApplyZoom();
    }

    /// <summary>当前可用的图片区域；首帧 Viewport 尚未测量时退回元素尺寸。</summary>
    private (double Width, double Height) Viewport()
    {
        double width = _scroll.ViewportWidth;
        double height = _scroll.ViewportHeight;
        if (!(width > 0)) width = Math.Max(1, _scroll.ActualWidth);
        if (!(height > 0)) height = Math.Max(1, _scroll.ActualHeight);
        return (width, height);
    }

    /// <summary>把当前缩放应用到图片尺寸上，并刷新百分比与按钮文案。</summary>
    private void ApplyZoom()
    {
        if (_applyingZoom || _image.Source is not ImageSource source) return;
        _applyingZoom = true;
        try
        {
            (double viewportWidth, double viewportHeight) = Viewport();
            (double width, double height) =
                _zoom.RenderSize(source.Width, source.Height, viewportWidth, viewportHeight);
            _image.Width = width;
            _image.Height = height;
            _image.Cursor = CanPan(width, height, viewportWidth, viewportHeight)
                ? Cursors.SizeAll
                : Cursors.Arrow;

            int percent = (int)Math.Round(_zoom.Percent(source.Width, source.Height,
                viewportWidth, viewportHeight));
            _zoomLevel.Text = $"{percent}%";
            AutomationProperties.SetName(_zoomLevel, Loc.Format("Zoom {0} percent", percent));
            _zoomToggle.Content = Loc.T(_zoom.IsFit ? "Actual size" : "Fit to window");
        }
        finally
        {
            _applyingZoom = false;
        }
    }

    private static bool CanPan(double width, double height, double viewportWidth, double viewportHeight) =>
        width > viewportWidth + 0.5 || height > viewportHeight + 0.5;

    private void SetFit()
    {
        _zoom = ScreenPreviewZoom.Fit;
        ApplyZoom();
        _scroll.ScrollToHome();
    }

    private void SetActualSize()
    {
        if (_image.Source is not ImageSource source) return;
        (double viewportWidth, double viewportHeight) = Viewport();
        _zoom = ScreenPreviewZoom.ActualSize(
            ScreenPreviewZoom.FitScale(source.Width, source.Height, viewportWidth, viewportHeight));
        ApplyZoom();
        _scroll.ScrollToHome();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        ZoomAt(e.Delta, e.GetPosition(_scroll));
    }

    /// <summary>以指针为锚点缩放：指针下的那一点在缩放前后保持不动。</summary>
    private void ZoomAt(int delta, Point anchor)
    {
        double extentWidth = _scroll.ExtentWidth;
        double extentHeight = _scroll.ExtentHeight;
        double anchorX = extentWidth > 0 ? (_scroll.HorizontalOffset + anchor.X) / extentWidth : 0.5;
        double anchorY = extentHeight > 0 ? (_scroll.VerticalOffset + anchor.Y) / extentHeight : 0.5;

        ScreenPreviewZoom next = _zoom.Wheel(delta);
        if (Math.Abs(next.Factor - _zoom.Factor) < 1e-9) return;
        _zoom = next;
        ApplyZoom();

        // 内容尺寸要等布局刷新后才可用，否则会按旧范围换算，画面会跳到左上角。
        _scroll.UpdateLayout();
        _scroll.ScrollToHorizontalOffset((anchorX * _scroll.ExtentWidth) - anchor.X);
        _scroll.ScrollToVerticalOffset((anchorY * _scroll.ExtentHeight) - anchor.Y);
    }

    private void OnImageLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_image.Source is null) return;
        (double viewportWidth, double viewportHeight) = Viewport();
        if (!CanPan(_image.Width, _image.Height, viewportWidth, viewportHeight)) return;

        _dragging = true;
        _dragOrigin = e.GetPosition(_scroll);
        _dragOffsetX = _scroll.HorizontalOffset;
        _dragOffsetY = _scroll.VerticalOffset;
        _image.CaptureMouse();
        e.Handled = true;
    }

    private void OnImageMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragging = false;
            return;
        }

        Vector moved = e.GetPosition(_scroll) - _dragOrigin;
        _scroll.ScrollToHorizontalOffset(_dragOffsetX - moved.X);
        _scroll.ScrollToVerticalOffset(_dragOffsetY - moved.Y);
        e.Handled = true;
    }

    private void OnImageLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _image.ReleaseMouseCapture();
        e.Handled = true;
    }
}
