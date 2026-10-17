using SuKe.UI;

namespace SuKe.Tests;

public class ScreenPreviewNavigationTests
{
    [Fact]
    public void Opens_the_clicked_page_and_stops_at_both_ends()
    {
        var pages = new ScreenPreviewNavigation(3, 1);
        Assert.Equal(1, pages.Index);
        Assert.True(pages.HasPrevious);
        Assert.True(pages.HasNext);
        Assert.True(pages.Move(1));
        Assert.Equal(2, pages.Index);
        Assert.False(pages.HasNext);
        Assert.False(pages.Move(1));
        Assert.Equal(2, pages.Index);
        Assert.True(pages.Move(-1));
        Assert.True(pages.Move(-1));
        Assert.Equal(0, pages.Index);
        Assert.False(pages.HasPrevious);
        Assert.False(pages.Move(-1));
    }

    [Fact]
    public void A_single_image_disables_both_directions()
    {
        var pages = new ScreenPreviewNavigation(1, 0);
        Assert.False(pages.HasPrevious);
        Assert.False(pages.HasNext);
        Assert.False(pages.Move(int.MaxValue));
        Assert.False(pages.Move(int.MinValue));
        Assert.Equal(0, pages.Index);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, -1)]
    [InlineData(2, 2)]
    public void Invalid_page_ranges_are_rejected(int count, int selected)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenPreviewNavigation(count, selected));
    }
}
