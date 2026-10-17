using System;

namespace SuKe.UI;

/// <summary>放大查看器的缩放状态：1.0 表示「适应窗口」，其余倍数相对它累积。</summary>
/// <remarks>
/// 这里是纯计算，不碰任何 WPF 元素。把它单独拆出来的原因是几条约定必须能被测试直接锁住：
/// 适应窗口必须真正填满视口（截图本身就是整屏分辨率，缩进去看等于白看），滚轮每次一格，
/// 百分比相对原始像素计算（100% 即 1:1）。
/// </remarks>
internal readonly struct ScreenPreviewZoom
{
    /// <summary>缩小下限：再小就没有可读性，也容易让人以为图挂了。</summary>
    internal const double MinimumFactor = 0.1;

    /// <summary>放大上限：再大只是马赛克，还会把滚动范围撑得难以控制。</summary>
    internal const double MaximumFactor = 12;

    private const double WheelStep = 1.1;

    private ScreenPreviewZoom(double factor) => Factor = factor;

    /// <summary>相对「适应窗口」的倍数。</summary>
    internal double Factor { get; }

    internal bool IsFit => Math.Abs(Factor - 1) < 1e-9;

    internal static ScreenPreviewZoom Fit => new(1);

    internal static ScreenPreviewZoom At(double factor) =>
        new(Math.Clamp(double.IsFinite(factor) ? factor : 1, MinimumFactor, MaximumFactor));

    /// <summary>一格滚轮（<paramref name="delta"/> = 120）缩放一档；向上滚即放大。</summary>
    internal ScreenPreviewZoom Wheel(int delta) =>
        delta == 0 ? this : At(Factor * Math.Pow(WheelStep, delta / 120d));

    /// <summary>让图片等比例放进视口的倍数；尺寸不可用时退回 1。</summary>
    internal static double FitScale(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (!(imageWidth > 0) || !(imageHeight > 0) || !(viewportWidth > 0) || !(viewportHeight > 0))
            return 1;
        return Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
    }

    /// <summary>1:1 像素对应的状态；<paramref name="fitScale"/> 是适应窗口时的倍数。</summary>
    internal static ScreenPreviewZoom ActualSize(double fitScale) =>
        At(fitScale > 0 ? 1 / fitScale : 1);

    /// <summary>当前缩放下的渲染尺寸（DIP）。</summary>
    internal (double Width, double Height) RenderSize(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight)
    {
        double scale = FitScale(imageWidth, imageHeight, viewportWidth, viewportHeight) * Factor;
        return (Math.Max(1, imageWidth * scale), Math.Max(1, imageHeight * scale));
    }

    /// <summary>相对原始像素的百分比，100% 就是 1:1。</summary>
    internal double Percent(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight) =>
        FitScale(imageWidth, imageHeight, viewportWidth, viewportHeight) * Factor * 100;
}
