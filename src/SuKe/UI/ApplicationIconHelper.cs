using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace SuKe.UI;

/// <summary>
/// Loads small application icons for UI surfaces that preview what a workspace will restore.
/// </summary>
/// <remarks>
/// Extracting an icon touches the file system and native shell APIs, so results are cached per
/// executable path and frozen. A frozen <see cref="BitmapSource"/> can be shared by any number of
/// <see cref="Image"/> elements, which lets several workspace rows reuse one decoded icon.
/// </remarks>
internal static class ApplicationIconHelper
{
    private static readonly Dictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The icon used when an executable has no associated icon.</summary>
    public const double DefaultSize = 20;

    /// <summary>Returns the shell icon for an executable, or <c>null</c> when it cannot be read.</summary>
    public static ImageSource? TryLoad(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(executablePath, out ImageSource? cached))
                return cached;

            ImageSource? loaded = Extract(executablePath);
            Cache[executablePath] = loaded;
            return loaded;
        }
    }

    private static ImageSource? Extract(string executablePath)
    {
        try
        {
            using DrawingIcon? icon = DrawingIcon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a small icon badge; falls back to the first letter of <paramref name="fallbackLabel"/>
    /// when the executable has no icon (uninstalled apps, store packages, missing paths).
    /// </summary>
    public static FrameworkElement CreateBadge(string? executablePath, string fallbackLabel,
        double size = DefaultSize)
    {
        ImageSource? source = TryLoad(executablePath);
        if (source is not null)
        {
            return new Image
            {
                Source       = source,
                Width        = size,
                Height       = size,
                Stretch      = Stretch.Uniform,
                ToolTip      = fallbackLabel,
                UseLayoutRounding = true,
            };
        }

        string letter = fallbackLabel.Length > 0 ? fallbackLabel[..1].ToUpperInvariant() : "?";
        var border = new Border
        {
            Width        = size,
            Height       = size,
            CornerRadius = new CornerRadius(4),
            ToolTip      = fallbackLabel,
            Child        = new TextBlock
            {
                Text              = letter,
                FontSize          = size * 0.55,
                FontWeight        = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };
        // 动态资源跟随宿主窗口主题，避免深色窗口中的回退图标使用系统浅色画刷。
        border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
        ((TextBlock)border.Child).SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return border;
    }
}
