using SuKe.UI;

namespace SuKe.Tests;

/// <summary>放大查看器的缩放约定：铺满视口、滚轮档位、1:1 百分比与上下限。</summary>
public class ScreenPreviewZoomTests
{
    [Fact]
    public void Fit_uses_the_tighter_axis_and_leaves_no_gap_on_it()
    {
        // 2560×1440 的截图放进 1300×800 的视口：宽度更紧（0.508 对 0.556），所以宽度刚好占满。
        double fit = ScreenPreviewZoom.FitScale(2560, 1440, 1300, 800);
        Assert.Equal(1300d / 2560, fit, 6);

        (double width, double height) = ScreenPreviewZoom.Fit.RenderSize(2560, 1440, 1300, 800);
        Assert.Equal(1300, width, 3);
        Assert.True(height <= 800, $"高度不应超出视口，实际 {height:F1}。");
    }

    [Fact]
    public void Fit_scale_falls_back_when_the_viewport_is_not_measured_yet()
    {
        Assert.Equal(1, ScreenPreviewZoom.FitScale(2560, 1440, 0, 0));
        Assert.Equal(1, ScreenPreviewZoom.FitScale(0, 0, 1300, 800));
        Assert.Equal(1, ScreenPreviewZoom.FitScale(double.NaN, 1440, 1300, 800));
    }

    [Fact]
    public void One_wheel_notch_is_one_zoom_step_in_either_direction()
    {
        ScreenPreviewZoom zoomed = ScreenPreviewZoom.Fit.Wheel(120);
        Assert.Equal(1.1, zoomed.Factor, 6);
        Assert.False(zoomed.IsFit);
        Assert.True(ScreenPreviewZoom.Fit.IsFit);
        Assert.Equal(1, zoomed.Wheel(-120).Factor, 6);
    }

    [Fact]
    public void Zoom_stops_at_the_documented_limits()
    {
        ScreenPreviewZoom large = ScreenPreviewZoom.Fit;
        for (int index = 0; index < 200; index++) large = large.Wheel(120);
        Assert.Equal(ScreenPreviewZoom.MaximumFactor, large.Factor, 6);

        ScreenPreviewZoom small = ScreenPreviewZoom.Fit;
        for (int index = 0; index < 200; index++) small = small.Wheel(-120);
        Assert.Equal(ScreenPreviewZoom.MinimumFactor, small.Factor, 6);
    }

    [Fact]
    public void Actual_size_is_exactly_one_hundred_percent()
    {
        const double imageWidth = 2560;
        const double imageHeight = 1440;
        const double viewportWidth = 2560;
        const double viewportHeight = 1300;

        double fit = ScreenPreviewZoom.FitScale(imageWidth, imageHeight, viewportWidth, viewportHeight);
        ScreenPreviewZoom actual = ScreenPreviewZoom.ActualSize(fit);

        Assert.Equal(100, actual.Percent(imageWidth, imageHeight, viewportWidth, viewportHeight), 3);
        (double width, double height) = actual.RenderSize(imageWidth, imageHeight, viewportWidth, viewportHeight);
        Assert.Equal(imageWidth, width, 3);
        Assert.Equal(imageHeight, height, 3);
    }

    [Fact]
    public void Fit_percentage_reports_the_render_scale()
    {
        Assert.Equal(50, ScreenPreviewZoom.Fit.Percent(2560, 1440, 1280, 720), 6);
    }
}
