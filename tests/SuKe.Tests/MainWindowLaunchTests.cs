namespace SuKe.Tests;

public class MainWindowLaunchTests
{
    [Fact]
    public void Direct_executable_launch_opens_the_main_window() =>
        Assert.False(App.IsBackgroundLaunch([]));

    [Fact]
    public void Windows_login_launch_remains_in_the_tray() =>
        Assert.True(App.IsBackgroundLaunch(["--minimized"]));

    [Fact]
    public void Background_switch_is_case_insensitive_and_can_follow_other_arguments() =>
        Assert.True(App.IsBackgroundLaunch(["--other", "--MINIMIZED"]));

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void Launch_is_minimized_only_when_the_tray_icon_is_available(
        bool backgroundLaunchRequested,
        bool trayIconAvailable,
        bool expected) =>
        Assert.Equal(expected, App.ShouldStartMinimized(backgroundLaunchRequested, trayIconAvailable));
}
