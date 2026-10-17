using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SuKe.Models;

namespace SuKe.Services;

internal sealed record ScreenPreview(int ScreenIndex, byte[] Png);

internal sealed record ScreenPreviewCaptureResult(int ScreenCount, IReadOnlyList<ScreenPreview> Images);

internal static class ScreenPreviewCapture
{
    internal static ScreenPreviewCaptureResult Capture(IReadOnlyList<MonitorInfo> monitors)
    {
        var images = new List<ScreenPreview>(monitors.Count);
        for (int index = 0; index < monitors.Count; index++)
        {
            MonitorInfo monitor = monitors[index];
            if (!monitor.HasValidBounds)
                continue;

            try
            {
                int width = checked(monitor.BoundsRight - monitor.BoundsLeft);
                int height = checked(monitor.BoundsBottom - monitor.BoundsTop);
                using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(
                        monitor.BoundsLeft, monitor.BoundsTop, 0, 0,
                        new Size(width, height),
                        CopyPixelOperation.SourceCopy);
                }

                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                images.Add(new ScreenPreview(index, stream.ToArray()));
            }
            catch (Exception ex)
            {
                // 单屏截图失败不应阻止工作区保存，其余屏幕仍可用于预览。
                AppLogger.Warn(
                    "workspace.screen_preview_capture_failed",
                    "Could not capture one screen preview",
                    ex,
                    LogField.Public("screenIndex", index),
                    LogField.Public("errorCategory", "screen_capture"));
            }
        }

        return new ScreenPreviewCaptureResult(monitors.Count, images);
    }
}
