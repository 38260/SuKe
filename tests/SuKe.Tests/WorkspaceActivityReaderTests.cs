using System.Reflection;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class WorkspaceActivityReaderTests
{
    [Fact]
    public void Snapshot_details_count_content_without_carrying_private_targets()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var workspace = new WorkspaceSnapshot
        {
            Name = "Design", SavedWithFiles = false,
            Monitors = [new MonitorInfo { MonitorId = "screen", FriendlyName = "Display", WidthPixels = 2560, HeightPixels = 1440, Dpi = 144, IsPrimary = true }],
            VirtualDesktops = [new SavedVirtualDesktop { DesktopId = Guid.NewGuid().ToString("D"), Name = "Private desktop" }],
            Entries = [
                new WorkspaceEntry { ProcessName = "editor", MonitorId = "screen", FilePath = @"D:\private\secret.txt", ExplorerTabPaths = [@"D:\private"] },
                new WorkspaceEntry { ProcessName = "EDITOR", MonitorId = "screen", TerminalTabs = [new TerminalTab { StartingDirectory = @"D:\private" }] },
                new WorkspaceEntry { ProcessName = @"D:\private\app.exe" }],
            BrowserSessions = [new BrowserSession { Tabs = [new BrowserTab { Url = "https://private.invalid", Title = "Private title" }] }],
        };
        storage.SaveWorkspace(workspace);
        WorkspaceActivitySnapshot snapshot = Assert.Single(new WorkspaceActivityReader(storage).Read()).Snapshot!;
        Assert.False(snapshot.SavedWithFiles);
        Assert.Equal(1, snapshot.BrowserTabCount);
        Assert.Equal(1, snapshot.ExplorerTabCount);
        Assert.Equal(1, snapshot.TerminalTabCount);
        Assert.Equal(1, snapshot.VirtualDesktopCount);
        Assert.Equal(2, snapshot.Applications.Count);
        Assert.Equal(2, snapshot.Applications[0].WindowCount);
        Assert.Equal(144u, Assert.Single(snapshot.Monitors).Dpi);
        Assert.Equal(2, snapshot.Monitors[0].WindowCount);
        Assert.Equal(1, snapshot.UnassignedWindowCount);
        string json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checkpoint_details_describe_the_captured_desktop_instead_of_the_target_workspace()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var target = new WorkspaceSnapshot { Name = "Target", Entries = [new WorkspaceEntry { ProcessName = "target" }] };
        storage.SaveWorkspace(target);
        storage.Checkpoints.Save(new WorkspaceSnapshot { Entries = [new WorkspaceEntry { ProcessName = "before" }] },
            WorkspaceCheckpointTrigger.Restore, target.WorkspaceId);
        WorkspaceActivityEntry checkpoint = Assert.Single(new WorkspaceActivityReader(storage).Read(), entry => entry.Kind == WorkspaceActivityKind.Checkpoint);
        Assert.Equal("before", Assert.Single(checkpoint.Snapshot!.Applications).Name);
        Assert.Null(checkpoint.Outcome);
    }

    [Fact]
    public void Empty_storage_produces_no_activity()
    {
        using var directory = new TestDirectory();

        Assert.Empty(new WorkspaceActivityReader(new StorageService(directory.Path)).Read());
    }

    [Fact]
    public void Saved_workspace_becomes_a_saved_event_with_counts()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        storage.SaveWorkspace(new WorkspaceSnapshot
        {
            Name = "Design",
            SavedAt = new DateTime(2026, 3, 1, 9, 15, 0, DateTimeKind.Utc),
            Monitors = { new MonitorInfo { Index = 1 }, new MonitorInfo { Index = 2 } },
            Entries = { new WorkspaceEntry { ExecutablePath = @"C:\Windows\explorer.exe" } },
        });

        WorkspaceActivityEntry entry = Assert.Single(new WorkspaceActivityReader(storage).Read());

        Assert.Equal(WorkspaceActivityKind.WorkspaceSaved, entry.Kind);
        Assert.Equal(WorkspaceActivityCategory.Saved, entry.Category);
        Assert.Null(entry.Trigger);
        Assert.Equal("Design", entry.WorkspaceName);
        Assert.Equal(2, entry.MonitorCount);
        Assert.Equal(1, entry.WindowCount);
        Assert.Equal(
            new DateTime(2026, 3, 1, 9, 15, 0, DateTimeKind.Utc).ToLocalTime(),
            entry.OccurredAtLocal);
    }

    [Fact]
    public void Checkpoint_triggers_are_classified_into_restore_and_system_groups()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var target = new WorkspaceSnapshot { Name = "Focus" };
        storage.SaveWorkspace(target);

        storage.Checkpoints.Save(
            new WorkspaceSnapshot { Entries = { new WorkspaceEntry() } },
            WorkspaceCheckpointTrigger.Restore,
            target.WorkspaceId);
        storage.Checkpoints.Save(
            new WorkspaceSnapshot { Entries = { new WorkspaceEntry() } },
            WorkspaceCheckpointTrigger.AutomaticDisplayRestore,
            target.WorkspaceId);

        IReadOnlyList<WorkspaceActivityEntry> entries = new WorkspaceActivityReader(storage).Read();

        Assert.Equal(3, entries.Count);
        Assert.Equal(
            WorkspaceActivityCategory.Restore,
            Assert.Single(entries, entry => entry.Trigger == WorkspaceCheckpointTrigger.Restore).Category);
        Assert.Equal(
            WorkspaceActivityCategory.System,
            Assert.Single(entries, entry => entry.Trigger == WorkspaceCheckpointTrigger.AutomaticDisplayRestore).Category);
    }

    [Fact]
    public void Checkpoint_names_only_a_target_that_still_exists()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var kept = new WorkspaceSnapshot { Name = "Design" };
        var removed = new WorkspaceSnapshot { Name = "Temporary" };
        storage.SaveWorkspace(kept);
        storage.SaveWorkspace(removed);

        storage.Checkpoints.Save(new WorkspaceSnapshot(), WorkspaceCheckpointTrigger.WorkspaceSwitch, kept.WorkspaceId);
        storage.Checkpoints.Save(new WorkspaceSnapshot(), WorkspaceCheckpointTrigger.Undo, removed.WorkspaceId);
        storage.DeleteWorkspace(removed);

        IReadOnlyList<WorkspaceActivityEntry> checkpoints = new WorkspaceActivityReader(storage).Read()
            .Where(entry => entry.Kind == WorkspaceActivityKind.Checkpoint)
            .ToArray();

        Assert.Equal("Design", Assert.Single(checkpoints, entry => entry.Trigger == WorkspaceCheckpointTrigger.WorkspaceSwitch).WorkspaceName);
        Assert.Null(
            Assert.Single(checkpoints, entry => entry.Trigger == WorkspaceCheckpointTrigger.Undo).WorkspaceName);
    }

    [Fact]
    public void Events_are_ordered_newest_first()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        storage.SaveWorkspace(new WorkspaceSnapshot
        {
            Name = "Older",
            SavedAt = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
        });
        storage.SaveWorkspace(new WorkspaceSnapshot
        {
            Name = "Newer",
            SavedAt = new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc),
        });

        IReadOnlyList<WorkspaceActivityEntry> entries = new WorkspaceActivityReader(storage).Read();

        Assert.Equal(new[] { "Newer", "Older" }, entries.Select(entry => entry.WorkspaceName).ToArray());
        Assert.Equal(
            entries.Select(entry => entry.OccurredAtLocal).OrderByDescending(occurred => occurred).ToArray(),
            entries.Select(entry => entry.OccurredAtLocal).ToArray());
    }

    [Fact]
    public void Activity_entries_never_carry_paths_titles_or_command_lines()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        storage.SaveWorkspace(new WorkspaceSnapshot
        {
            Name = "Design",
            SavedAt = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            Entries =
            {
                new WorkspaceEntry
                {
                    ExecutablePath = @"D:\sensitive\app.exe",
                    FilePath = @"D:\sensitive\secret.docx",
                    ProcessName = "sensitive-process",
                    WindowClassName = "SensitiveClass",
                },
            },
        });

        foreach (WorkspaceActivityEntry entry in new WorkspaceActivityReader(storage).Read())
        {
            foreach (PropertyInfo property in entry.GetType().GetProperties())
            {
                if (property.GetValue(entry) is not string text) continue;
                Assert.DoesNotContain("sensitive", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\\", text, StringComparison.Ordinal);
                Assert.DoesNotContain("/", text, StringComparison.Ordinal);
            }
        }
    }
}
