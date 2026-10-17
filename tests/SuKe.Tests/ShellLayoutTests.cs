using System.Globalization;
using SuKe.UI.Shell;

namespace SuKe.Tests;

public class ShellLayoutTests
{
    [Theory]
    [InlineData(760, true)]
    [InlineData(1040, true)]
    [InlineData(1100, false)]
    [InlineData(1584, false)]
    public void Compact_layout_changes_at_the_declared_window_width(double width, bool expected)
    {
        var converter = new CompactWidthConverter();
        Assert.Equal(expected, converter.Convert(width, typeof(bool), "1100", CultureInfo.InvariantCulture));
    }
}
