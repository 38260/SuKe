using System;
using System.Windows;
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
/// </remarks>
internal sealed partial class ScreenPreviewViewerWindow : FluentWindow
{
    private readonly ScrollViewer _scroll;
    private readonly Image _image;
    private bool _actualSize;

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
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        _scroll = new ScrollViewer
        {
            Content = _image,
            // 左右留窄边：翻页按钮现在各占图片侧边一列，图片自身的横向边距只需一圈窄内衬。
            Margin = new Thickness(12, 12, 12, 12),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = false
        };
        // Re-fit whenever the viewport changes; the fit mode is expressed as a maximum size so the
        // image is never upscaled past its captured resolution.
        _scroll.SizeChanged += (_, _) => ApplyZoom();
        Grid.SetRow(_scroll, 2);
        root.Children.Add(_scroll);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 16)
        };
        var zoomToggle = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T("Actual size"),
            MinWidth = 128,
            Margin = new Thickness(0, 0, 8, 0)
        };
        zoomToggle.Click += (_, _) =>
        {
            _actualSize = !_actualSize;
            zoomToggle.Content = Loc.T(_actualSize ? "Fit to window" : "Actual size");
            ApplyZoom();
        };
        actions.Children.Add(zoomToggle);

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
            }
        };

        Content = root;
        ApplyZoom();
    }

    /// <summary>Switches between "fit the viewport" and 1:1 pixels.</summary>
    private void ApplyZoom()
    {
        if (_actualSize)
        {
            _image.Stretch = Stretch.None;
            _image.MaxWidth = double.PositiveInfinity;
            _image.MaxHeight = double.PositiveInfinity;
            return;
        }

        _image.Stretch = Stretch.Uniform;
        _image.MaxWidth = Math.Max(1, _scroll.ViewportWidth > 0
            ? _scroll.ViewportWidth
            : _scroll.ActualWidth - 2);
        _image.MaxHeight = Math.Max(1, _scroll.ViewportHeight > 0
            ? _scroll.ViewportHeight
            : _scroll.ActualHeight - 2);
    }
}
