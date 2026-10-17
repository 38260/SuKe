using System.Text.Json;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class SelectedWindowCaptureTests
{
    [Theory]
    [InlineData((int)SelectedWindowFailure.WindowClosed)]
    [InlineData((int)SelectedWindowFailure.ReadUnavailable)]
    [InlineData((int)SelectedWindowFailure.IdentityChanged)]
    public async Task Invalid_selected_window_prevents_success_and_persistence(int reasonValue)
    {
        var reason = (SelectedWindowFailure)reasonValue;
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var selected = Record(101);
        var live = Record(101);
        var inventory = new FakeWindowInventory { IsAliveProvider = _ => reason != SelectedWindowFailure.WindowClosed };
        if (reason != SelectedWindowFailure.ReadUnavailable)
            inventory.Live[selected.SourceWindowHandle] = (reason == SelectedWindowFailure.IdentityChanged ? 999u : 777u, live);
        await using var service = new WorkspaceService(storage, inventory, new RecordingWindowMutation(),
            new FakeMonitorInventory(), new JumpListService());
        var error = await Assert.ThrowsAsync<SelectedWindowCaptureException>(() => service.CaptureWorkspaceAsync(
            "test", saveFiles: false, selectedWindows: [selected], captureBrowserSessions: false));
        Assert.Equal(reason, Assert.Single(error.Failures));
        Assert.Empty(storage.LoadAllWorkspaces());
    }

    [Fact]
    public async Task Selected_set_survives_unknown_monitor_and_saves_without_persisting_native_handles()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var windows = new[] { Record(101), Record(102) };
        var inventory = new FakeWindowInventory();
        foreach (var window in windows) inventory.Live[window.SourceWindowHandle] = (777, window);
        await using var service = new WorkspaceService(storage, inventory, new RecordingWindowMutation(),
            new FakeMonitorInventory(), new JumpListService());
        var capture = await service.CaptureWorkspaceAsync("test", saveFiles: false,
            selectedWindows: windows.ToList(), captureBrowserSessions: false);
        Assert.Equal(2, capture.Snapshot.Entries.Count);
        Assert.Empty(capture.Snapshot.Monitors);
        service.PersistCapture(capture, WorkspaceArtifactKind.NamedWorkspace, IncompleteBrowserCapturePolicy.SavePartialWorkspace);
        Assert.Equal(2, Assert.Single(storage.LoadAllWorkspaces()).Entries.Count);
        Assert.DoesNotContain("SourceWindowHandle", JsonSerializer.Serialize(capture.Snapshot));
        Assert.Equal(3, inventory.LiveInventoryCalls);
    }

    [Fact]
    public async Task Window_closing_during_capture_is_reported()
    {
        using var directory = new TestDirectory();
        var selected = Record(101);
        var inventory = new FakeWindowInventory { IsAliveProvider = _ => true };
        inventory.LiveProvider = call => call == 1 ? new() { [selected.SourceWindowHandle] = (777, selected) } : new();
        await using var service = new WorkspaceService(new StorageService(directory.Path), inventory,
            new RecordingWindowMutation(), new FakeMonitorInventory(), new JumpListService());
        await Assert.ThrowsAsync<SelectedWindowCaptureException>(() => service.CaptureWorkspaceAsync("test",
            saveFiles: false, selectedWindows: [selected], captureBrowserSessions: false));
    }

    [Fact]
    public async Task Window_closing_before_persistence_cannot_be_reported_as_saved()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var selected = Record(101);
        var inventory = new FakeWindowInventory();
        inventory.Live[selected.SourceWindowHandle] = (777, selected);
        await using var service = new WorkspaceService(storage, inventory, new RecordingWindowMutation(),
            new FakeMonitorInventory(), new JumpListService());
        var capture = await service.CaptureWorkspaceAsync("test", saveFiles: false,
            selectedWindows: [selected], captureBrowserSessions: false);
        inventory.Live.Clear();
        var error = Assert.Throws<SelectedWindowCaptureException>(() => service.PersistCapture(capture,
            WorkspaceArtifactKind.NamedWorkspace, IncompleteBrowserCapturePolicy.SavePartialWorkspace));
        Assert.Equal(SelectedWindowFailure.WindowClosed, Assert.Single(error.Failures));
        Assert.Empty(storage.LoadAllWorkspaces());
    }

    private static WindowRecord Record(int handle) => new()
    {
        SourceWindowHandle = new IntPtr(handle), SourceProcessId = 777,
        ProcessName = "editor", ExecutablePath = @"C:\Apps\editor.exe", ClassName = "EditorWindow",
        MonitorId = "unknown", NormalRight = 800, NormalBottom = 600
    };
}
