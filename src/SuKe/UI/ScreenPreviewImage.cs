using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace SuKe.UI;

/// <summary>解码保存时采集的屏幕截图。</summary>
/// <remarks>
/// 缩略图只需要几百像素宽，但放大查看必须是原始像素：查看器会按视口把图放大到铺满，
/// 若沿用缩略图分辨率，最大化后只是把一张小图放大成糊块。<paramref name="targetWidth"/> 为
/// <c>null</c> 时保留原始像素，供放大查看使用。
/// </remarks>
internal static class ScreenPreviewImage
{
    internal static BitmapSource? Decode(byte[] png, int? targetWidth = null)
    {
        try
        {
            using var stream = new MemoryStream(png, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (targetWidth is int width && width > 0) bitmap.DecodePixelWidth = width;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            // 损坏的单张截图不影响其余屏幕，也不影响工作区本身。
            return null;
        }
    }
}
