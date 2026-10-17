using System.Text.Json;
using System.Text.Json.Serialization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

/// <summary>
/// 工作区删除记录：工作区文档被删掉之后，活动流里唯一还能证明它存在过的东西。
/// </summary>
public class WorkspaceDeletionTests
{
    [Fact]
    public void Deleting_a_workspace_writes_one_record_carrying_its_last_saved_summary()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var workspace = new WorkspaceSnapshot
        {
            Name = "随机过程",
            SavedAt = new DateTime(2026, 10, 17, 8, 30, 0, DateTimeKind.Utc),
            Monitors = [new MonitorInfo { MonitorId = "screen-1", FriendlyName = "DM55", WidthPixels = 2560, HeightPixels = 1440, Dpi = 120, IsPrimary = true }],
            Entries = [
                new WorkspaceEntry { ProcessName = "explorer", MonitorId = "screen-1" },
                new WorkspaceEntry { ProcessName = "explorer", MonitorId = "screen-1" }],
        };
        storage.SaveWorkspace(workspace);

        storage.DeleteWorkspace(workspace);

        WorkspaceDeletionRecord record = Assert.Single(storage.Deletions.Read());
        Assert.False(string.IsNullOrWhiteSpace(record.DeletionId));
        Assert.Equal(workspace.WorkspaceId, record.WorkspaceId);
        Assert.Equal("随机过程", record.WorkspaceName);
        Assert.Equal(2, record.WindowCount);
        Assert.Equal(1, record.MonitorCount);
        // 原保存时间要留下来，否则用户看不出这个工作区最后一次保存是什么时候。
        Assert.Equal(
            new DateTime(2026, 10, 17, 8, 30, 0, DateTimeKind.Utc),
            record.SavedAtUtc);
        Assert.Equal(120u, Assert.Single(record.Snapshot!.Monitors).Dpi);
    }

    [Fact]
    public void A_deleted_workspace_is_reported_as_a_deletion_and_no_longer_as_a_saved_workspace()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var survivor = new WorkspaceSnapshot { Name = "Keep", Entries = [new WorkspaceEntry()] };
        var doomed = new WorkspaceSnapshot { Name = "Drop", Entries = [new WorkspaceEntry()] };
        storage.SaveWorkspace(survivor);
        storage.SaveWorkspace(doomed);

        storage.DeleteWorkspace(doomed);

        IReadOnlyList<WorkspaceActivityEntry> entries = new WorkspaceActivityReader(storage).Read();
        // 只剩幸存的工作区（保存）+ 被删工作区（删除）两条，不要重复也不要丢失。
        Assert.Equal(2, entries.Count);

        WorkspaceActivityEntry deletion = Assert.Single(entries,
            entry => entry.Kind == WorkspaceActivityKind.WorkspaceDeleted);
        Assert.Equal(WorkspaceActivityCategory.Deleted, deletion.Category);
        Assert.Equal("Drop", deletion.WorkspaceName);
        Assert.Equal(1, deletion.WindowCount);
        Assert.Null(deletion.Outcome);
        Assert.Equal(
            new DateTime(2026, 10, 17, 8, 30, 0, DateTimeKind.Utc).ToLocalTime()
                .Date,
            deletion.OccurredAtLocal.Date);

        WorkspaceActivityEntry saved = Assert.Single(entries,
            entry => entry.Kind == WorkspaceActivityKind.WorkspaceSaved);
        Assert.Equal("Keep", saved.WorkspaceName);
        Assert.Equal(WorkspaceActivityCategory.Saved, saved.Category);
    }

    [Fact]
    public void Deletion_history_is_bounded_by_the_retention_limit()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        int total = WorkspaceDeletionStore.MaximumRecords + 5;
        for (int index = 0; index < total; index++)
        {
            var workspace = new WorkspaceSnapshot { Name = $"WS{index:D3}" };
            storage.SaveWorkspace(workspace);
            storage.DeleteWorkspace(workspace);
        }

        IReadOnlyList<WorkspaceDeletionRecord> records = storage.Deletions.Read();

        Assert.Equal(WorkspaceDeletionStore.MaximumRecords, records.Count);
        // 裁剪按写入时间保留最新的，因此最早那几个应当先被丢掉。
        Assert.DoesNotContain(records, record => record.WorkspaceName == "WS000");
    }

    [Fact]
    public void A_deletion_record_carries_counts_but_no_private_targets()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var workspace = new WorkspaceSnapshot
        {
            Name = "Secret project",
            Monitors = [new MonitorInfo { MonitorId = "screen", FriendlyName = "Secret display" }],
            Entries = [
                new WorkspaceEntry
                {
                    ProcessName = "editor",
                    MonitorId = "screen",
                    FilePath = @"D:\private\secret.txt",
                    LaunchArg = @"D:\private\secret.txt",
                    ExplorerTabPaths = [@"D:\private"],
                }],
        };
        storage.SaveWorkspace(workspace);

        storage.DeleteWorkspace(workspace);

        WorkspaceDeletionRecord record = Assert.Single(storage.Deletions.Read());
        string json = JsonSerializer.Serialize(record, SerializerOptions);
        Assert.DoesNotContain("private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.txt", json, StringComparison.OrdinalIgnoreCase);
        // 工作区名是用户数据、本就该显示，这条只保证不夹带路径类信息。
        Assert.Contains("Secret project", json, StringComparison.Ordinal);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
