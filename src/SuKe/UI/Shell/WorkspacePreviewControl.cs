using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.UI.Shell;

/// <summary>在工作区卡片展示本机保存的真实屏幕截图。</summary>
/// <remarks>
/// 截图附件是每块显示器一张 PNG，读取需要解开 zip 并解码位图。列表没有虚拟化，所有卡片会同时
/// 显示，因此这里做三件事：读取与解码放到后台线程、缩略图按实际显示尺寸（含 DPI）解码、
/// 结果按 (workspaceId, savedAt) 缓存。显隐切换因此不再重复读盘和解码；重新保存后 SavedAt 变化
/// 也会自然失效，不会用工作区编号永久缓存旧图。
/// </remarks>
public sealed class WorkspacePreviewControl : StackPanel
{
    private const int MinimumThumbnailWidth = 240;
    private const int MaximumThumbnailWidth = 960;

    private WorkspaceSnapshot? _workspace;
    private WorkspacePreviewStore? _store;
    private bool _showScreenshots = true;
    private int _renderGeneration;

    internal void Configure(WorkspaceSnapshot workspace, WorkspacePreviewStore store)
    {
        _workspace = workspace;
        _store = store;
        _renderGeneration++;
        RenderPreview();
    }

    private void RenderPreview()
    {
        int generation = _renderGeneration;
        Children.Clear();
        if (_workspace is not WorkspaceSnapshot workspace || _store is not WorkspacePreviewStore store) return;

        if (PreviewCache.TryGet(workspace) is PreviewCacheEntry cached)
        {
            BuildPreview(workspace, cached);
            return;
        }

        // 第一次显示这份工作区：先读盘并解码，完成后再填充，避免阻塞 UI 线程。
        _ = LoadAndBuildAsync(workspace, store, ThumbnailWidthPixels(), generation);
    }

    private async Task LoadAndBuildAsync(
        WorkspaceSnapshot workspace,
        WorkspacePreviewStore store,
        int thumbnailWidth,
        int generation)
    {
        PreviewCacheEntry entry = await Task.Run(() => BuildEntry(workspace, store, thumbnailWidth))
            .ConfigureAwait(true);

        // 页面卸载、工作区被替换或重新渲染后丢弃过期结果，避免旧图显示到新卡片上。
        if (generation != _renderGeneration || !ReferenceEquals(_workspace, workspace)) return;

        PreviewCache.Store(workspace.WorkspaceId, entry);
        Children.Clear();
        BuildPreview(workspace, entry);
    }

    private static PreviewCacheEntry BuildEntry(
        WorkspaceSnapshot workspace,
        WorkspacePreviewStore store,
        int thumbnailWidth)
    {
        long savedAtTicks = workspace.SavedAt.Ticks;
        IReadOnlyList<ScreenPreview> images;
        try
        {
            images = store.Load(workspace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            images = Array.Empty<ScreenPreview>();
        }

        var thumbnails = new List<PreviewThumbnail>(images.Count);
        foreach (ScreenPreview image in images)
        {
            ImageSource? source = Decode(image.Png, thumbnailWidth);
            if (source is null) continue;
            thumbnails.Add(new PreviewThumbnail(image.ScreenIndex, source));
        }

        return new PreviewCacheEntry(savedAtTicks, thumbnails.Count > 0, thumbnails);
    }

    /// <summary>按目标显示宽度解码，并冻结以便跨线程交给 UI 线程。</summary>
    private static ImageSource? Decode(byte[] png, int targetWidth)
    {
        try
        {
            using var stream = new MemoryStream(png, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = targetWidth;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            // 不用空白图形冒充截图；其余可解码的显示器仍可展示。
            return null;
        }
    }

    /// <summary>缩略图的实际像素宽度：按卡片里真实的展示宽度并乘上当前 DPI 缩放。</summary>
    private int ThumbnailWidthPixels()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;

        // 两张并排的缩略图各自占卡片内容宽度的一半。
        double available = ActualWidth > 0 ? ActualWidth : 520;
        double column = Math.Clamp((available - 16) / 2, 240, 480);
        return (int)Math.Clamp(Math.Ceiling(column * scale), MinimumThumbnailWidth, MaximumThumbnailWidth);
    }

    private void BuildPreview(WorkspaceSnapshot workspace, PreviewCacheEntry entry)
    {
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        if (entry.HasPreview)
        {
            var reveal = new Wpf.Ui.Controls.Button
            {
                Content = Loc.T(_showScreenshots ? "Hide screen previews" : "Show screen previews"),
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Right,
            };
            DockPanel.SetDock(reveal, Dock.Right);
            reveal.Click += (_, _) => { _showScreenshots = !_showScreenshots; RenderPreview(); };
            toolbar.Children.Add(reveal);
        }
        toolbar.Children.Add(new TextBlock
        {
            Text = Loc.T(!entry.HasPreview
                ? "No saved screen previews. Save this workspace again to capture its monitors."
                : _showScreenshots ? "Saved screen previews" : "Screen previews are hidden"),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.LightSlateGray,
        });
        Children.Add(toolbar);

        if (entry.Thumbnails.Count == 0) return;

        var monitors = new UniformGrid { Columns = Math.Min(2, entry.Thumbnails.Count) };
        foreach (PreviewThumbnail thumbnail in entry.Thumbnails)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var view = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = new Image { Source = thumbnail.Image, Stretch = Stretch.Uniform },
                MaxHeight = 260,
                Visibility = _showScreenshots ? Visibility.Visible : Visibility.Collapsed,
            };
            var frame = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 16, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(84, 91, 100)),
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4), Child = view,
                Visibility = _showScreenshots ? Visibility.Visible : Visibility.Collapsed,
            };
            MonitorInfo? monitor = workspace.Monitors.FirstOrDefault(item => item.Index == thumbnail.ScreenIndex);

            // A single click opens the enlarged viewer. The frame handles the button-down itself so
            // the click cannot reach the workspace card's own actions behind it. The viewer decodes
            // from the original PNG on demand, so a card row only pays for its thumbnail.
            int screenIndex = thumbnail.ScreenIndex;
            frame.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                OpenViewer(workspace, screenIndex);
            };
            frame.Cursor = Cursors.Hand;
            frame.ToolTip = ScreenPreviewText.ZoomHint;

            panel.Children.Add(frame);
            panel.Children.Add(new TextBlock
            {
                Text = monitor is null
                    ? ScreenPreviewText.ScreenLabel(thumbnail.ScreenIndex)
                    : $"{monitor.FriendlyName} · {monitor.WidthPixels} × {monitor.HeightPixels}",
                TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11,
                Foreground = Brushes.LightSlateGray, Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = _showScreenshots ? Visibility.Visible : Visibility.Collapsed,
            });
            monitors.Children.Add(panel);
        }

        if (monitors.Children.Count > 0)
            Children.Add(monitors);
    }

    /// <summary>
    /// 打开放大查看器。查看器需要清晰的原图，因此在这里才按较大尺寸解码，且只解码当前工作区。
    /// </summary>
    private void OpenViewer(WorkspaceSnapshot workspace, int selectedScreenIndex)
    {
        if (_store is not WorkspacePreviewStore store) return;

        IReadOnlyList<ScreenPreview> images;
        try
        {
            images = store.Load(workspace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            return;
        }

        var pages = new List<ScreenPreviewPage>(images.Count);
        int selectedPage = 0;
        foreach (ScreenPreview image in images)
        {
            if (Decode(image.Png, 1200) is not ImageSource enlarged) continue;
            MonitorInfo? monitor = workspace.Monitors.FirstOrDefault(item => item.Index == image.ScreenIndex);
            string label = ScreenPreviewText.ScreenLabel(image.ScreenIndex);
            string? subtitle = monitor is null
                ? null
                : $"{monitor.FriendlyName} · {monitor.WidthPixels} × {monitor.HeightPixels}";
            if (image.ScreenIndex == selectedScreenIndex) selectedPage = pages.Count;
            pages.Add(new ScreenPreviewPage(enlarged, label, subtitle));
        }

        if (pages.Count == 0) return;
        new ScreenPreviewViewerWindow(pages, selectedPage, Window.GetWindow(this)).ShowDialog();
    }
}

/// <summary>一块显示器缩略图：索引与已冻结的解码结果。</summary>
internal sealed record PreviewThumbnail(int ScreenIndex, ImageSource Image);

/// <summary>一份工作区预览的解码结果；<c>SavedAtTicks</c> 用于在重新保存后失效。</summary>
internal sealed record PreviewCacheEntry(
    long SavedAtTicks,
    bool HasPreview,
    IReadOnlyList<PreviewThumbnail> Thumbnails);

/// <summary>有界、按工作区缓存已解码缩略图的进程内缓存。</summary>
internal static class PreviewCache
{
    /// <summary>缓存上限：每份缩略图集合都对应一屏分辨率级别的内存占用。</summary>
    private const int MaximumEntries = 24;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, PreviewCacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> Order = new();

    /// <summary>命中要求工作区标识与保存时间都一致，否则视为未缓存。</summary>
    internal static PreviewCacheEntry? TryGet(WorkspaceSnapshot workspace)
    {
        lock (Sync)
        {
            return Entries.TryGetValue(workspace.WorkspaceId, out PreviewCacheEntry? entry) &&
                   entry.SavedAtTicks == workspace.SavedAt.Ticks
                ? entry
                : null;
        }
    }

    internal static void Store(string workspaceId, PreviewCacheEntry entry)
    {
        lock (Sync)
        {
            if (Entries.ContainsKey(workspaceId))
            {
                Entries[workspaceId] = entry;
                return;
            }

            Entries[workspaceId] = entry;
            Order.Enqueue(workspaceId);
            while (Order.Count > MaximumEntries)
                Entries.Remove(Order.Dequeue());
        }
    }
}
