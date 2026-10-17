using SuKe.Models;
using SuKe.Services;
using SuKe.UI;

namespace SuKe.Tests;

public class WindowPreviewGroupingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preview_preserves_unknown_monitor_and_desktop_without_fabricating_a_monitor(bool haveMonitor)
    {
        using var directory = new TestDirectory();
        var monitor = new MonitorInfo { MonitorId = "known", FriendlyName = "Test" };
        var known = new WindowRecord { MonitorId = "known", ProcessName = "editor", TitleSnippet = "A" };
        var unknown = new WindowRecord { MonitorId = "missing", ProcessName = "editor", TitleSnippet = "" };
        var inventory = new FakeWindowInventory { Snapshot = [known, unknown] };
        var monitors = new FakeMonitorInventory { Monitors = haveMonitor ? [monitor] : [] };
        await using var service = new WorkspaceService(new StorageService(directory.Path), inventory,
            new RecordingWindowMutation(), monitors, new JumpListService());
        var preview = service.GetWindowPreviewForDialog();
        Assert.Equal(2, preview.Sum(group => group.Windows.Count));
        var unassigned = Assert.Single(preview, group => group.Monitor is null);
        Assert.Contains(unknown, unassigned.Windows);
        var groups = SaveWorkspaceDialog.BuildGroups(preview);
        Assert.Equal(2, groups.Sum(group => group.Windows.Count));
        var item = Assert.Single(groups.SelectMany(group => group.Windows), item => ReferenceEquals(item.Record, unknown));
        Assert.Equal("editor", item.TitleSnippet);
        Assert.True(item.IsSelected);
        Assert.Contains(groups, group => group.GroupHeader.Contains("Unidentified monitor") && group.Windows.Contains(item));
        Assert.Contains(groups, group => group.GroupHeader.Contains("Desktop unidentified"));
        Assert.Equal(haveMonitor ? 1 : 0, monitors.Monitors.Count);
    }
}
