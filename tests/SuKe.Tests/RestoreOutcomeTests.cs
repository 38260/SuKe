using System.IO;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

/// <summary>
/// 恢复结果记录与统计的针对性回归。这里刻意只驱动真实的 <see cref="RestoreExecutionResult"/>，
/// 不替换状态汇总逻辑，也不拿已保存内容量推测成功数。
/// </summary>
public class RestoreOutcomeTests
{
    [Fact]
    public void Every_target_succeeded_yields_all_succeeded_and_a_closed_sum()
    {
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched), ("b", RestorePlanEntryOutcome.Matched));
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries(
                (0, RestoreExecutionEntryStatus.Restored),
                (1, RestoreExecutionEntryStatus.Restored)));

        RestoreOutcomeRecord record = Project(plan, result);

        Assert.Equal(RestoreOutcomeStatus.Succeeded, record.Status);
        Assert.Equal(2, record.Windows.Planned);
        Assert.Equal(2, record.Windows.Succeeded);
        AssertClosed(record.Windows);
        Assert.True(record.Windows.Recorded);
        Assert.Empty(record.Issues);
    }

    [Fact]
    public void Partial_failure_splits_success_failure_and_unconfirmed_without_double_counting()
    {
        RestorePlan plan = Plan(
            ("ok", RestorePlanEntryOutcome.Matched),
            ("bad", RestorePlanEntryOutcome.Matched),
            ("slow", RestorePlanEntryOutcome.MatchedAndLaunchRequired));
        // 一个窗口经历了启动、等待、移动和验证等多个动作，但在统计里仍然只是一个窗口。
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.CompletedWithFailures,
            Entries(
                (0, RestoreExecutionEntryStatus.Restored),
                (1, RestoreExecutionEntryStatus.Failed),
                (2, RestoreExecutionEntryStatus.LaunchRequested)),
            Actions(
                (0, 0, RestoreActionKind.LaunchApplication, RestoreExecutionActionStatus.Succeeded),
                (1, 1, RestoreActionKind.LaunchApplication, RestoreExecutionActionStatus.Failed),
                (2, 2, RestoreActionKind.AwaitWindowAppearance, RestoreExecutionActionStatus.Succeeded),
                (3, 2, RestoreActionKind.RestoreExistingWindow, RestoreExecutionActionStatus.Succeeded)));

        RestoreOutcomeRecord record = Project(plan, result);

        Assert.Equal(RestoreOutcomeStatus.PartiallyCompleted, record.Status);
        Assert.Equal(3, record.Windows.Planned);
        Assert.Equal(1, record.Windows.Succeeded);
        Assert.Equal(1, record.Windows.Failed);
        Assert.Equal(1, record.Windows.Unconfirmed);
        // 各状态之和必须与计划数闭合。
        AssertClosed(record.Windows);
        Assert.Contains(record.Issues, issue => issue.ReasonCode == "entry_failed");
        Assert.Contains(record.Issues, issue => issue.ReasonCode == "launch_requested_not_confirmed");
    }

    [Fact]
    public void A_failed_action_with_no_success_reports_failure()
    {
        RestorePlan plan = Plan(("only", RestorePlanEntryOutcome.Matched));
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.CompletedWithFailures,
            Entries((0, RestoreExecutionEntryStatus.Failed)));

        Assert.Equal(RestoreOutcomeStatus.Failed, Project(plan, result).Status);
    }

    [Fact]
    public void Cancellation_and_rejection_and_stale_plans_are_kept_apart_from_success()
    {
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched));

        RestoreExecutionResult cancelled = Result(
            plan,
            RestoreExecutionStatus.Cancelled,
            Entries((0, RestoreExecutionEntryStatus.Cancelled)),
            wasCancelled: true);
        Assert.Equal(RestoreOutcomeStatus.Cancelled, Project(plan, cancelled).Status);
        Assert.Equal(1, Project(plan, cancelled).Windows.Cancelled);
        Assert.Equal(0, Project(plan, cancelled).Windows.Succeeded);

        RestoreExecutionResult blocked = Result(
            plan,
            RestoreExecutionStatus.Rejected,
            Entries((0, RestoreExecutionEntryStatus.Blocked)));
        RestoreOutcomeRecord blockedRecord = Project(plan, blocked);
        Assert.Equal(RestoreOutcomeStatus.Blocked, blockedRecord.Status);
        Assert.Equal(0, blockedRecord.Windows.Succeeded);
        Assert.Contains(blockedRecord.Issues, issue => issue.ReasonCode == "operation_blocked");

        RestoreExecutionResult stale = Result(
            plan,
            RestoreExecutionStatus.StalePlan,
            Entries((0, RestoreExecutionEntryStatus.Stale)));
        Assert.Equal(RestoreOutcomeStatus.StalePlan, Project(plan, stale).Status);
        Assert.Equal(1, Project(plan, stale).Windows.Stale);
    }

    [Fact]
    public void A_moved_window_with_failed_placement_verification_does_not_count_as_success()
    {
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched));
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.CompletedWithFailures,
            new[]
            {
                new RestoreExecutionEntryResult(
                    0,
                    "entry-0",
                    RestoreExecutionEntryStatus.Restored,
                    42,
                    "The window was moved.",
                    PlacementVerification: WindowPlacementVerificationState.Rejected),
            });

        RestoreOutcomeRecord record = Project(plan, result);

        Assert.Equal(0, record.Windows.Succeeded);
        Assert.Equal(1, record.Windows.Failed);
        Assert.Equal(RestoreOutcomeStatus.Failed, record.Status);
        Assert.Contains(record.Issues, issue => issue.ReasonCode == "placement_verification_failed");
    }

    [Fact]
    public void Disabled_entries_are_not_planned_targets()
    {
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched), ("b", RestorePlanEntryOutcome.Matched)) with
        {
            DisabledEntryIndexes = new HashSet<int> { 1 },
        };
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries((0, RestoreExecutionEntryStatus.Restored)));

        Assert.Equal(1, Project(plan, result).Windows.Planned);
    }

    [Fact]
    public void Content_types_are_counted_separately_and_unreported_types_are_not_zero_filled()
    {
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched), ("b", RestorePlanEntryOutcome.Matched)) with
        {
            Entries = new[]
            {
                Entry(0, "a", RestorePlanEntryOutcome.Matched) with
                {
                    ExplorerSession = new RestoreExplorerSession(new[] { @"C:\one", @"C:\two" }, 1),
                },
                Entry(1, "b", RestorePlanEntryOutcome.Matched),
            },
            Actions = new[]
            {
                new RestoreAction(
                    0,
                    RestoreActionKind.RestoreExplorerTabs,
                    null,
                    "",
                    "",
                    false,
                    null,
                    "Reconcile Explorer tabs."),
            },
            BrowserSessions = new[]
            {
                new RestoreBrowserSession(
                    "chrome", "Inbox", 0, 0, 0, 800, 600, "normal",
                    new[]
                    {
                        new RestoreBrowserTab("https://a.example", "A", 0, true, false, 0),
                        new RestoreBrowserTab("https://b.example", "B", 1, false, false, 0),
                        new RestoreBrowserTab("https://c.example", "C", 2, false, false, 0),
                    },
                    Array.Empty<RestoreBrowserTabGroup>()),
            },
        };

        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.CompletedWithFailures,
            Entries(
                (0, RestoreExecutionEntryStatus.Restored),
                (1, RestoreExecutionEntryStatus.Restored)),
            Actions(
                (0, 0, RestoreActionKind.RestoreExplorerTabs, RestoreExecutionActionStatus.Failed),
                (1, null, RestoreActionKind.RestoreBrowserSession, RestoreExecutionActionStatus.Succeeded)));

        RestoreOutcomeRecord record = Project(plan, result);

        Assert.Equal(2, record.Windows.Planned);
        Assert.Equal(2, record.Windows.Succeeded);
        Assert.Equal(2, record.ExplorerTabs.Planned);
        Assert.Equal(0, record.ExplorerTabs.Succeeded);
        Assert.Equal(2, record.ExplorerTabs.Failed);
        Assert.Equal(3, record.BrowserTabs.Planned);
        Assert.Equal(3, record.BrowserTabs.Succeeded);
        // 计划里没有终端标签页，这一类型不能补成 0 或 100% 成功。
        Assert.False(record.TerminalTabs.Recorded);
        Assert.Equal(0, record.TerminalTabs.Planned);
    }

    [Fact]
    public void The_same_application_in_several_windows_is_counted_per_window()
    {
        RestorePlan plan = Plan(
            ("first", RestorePlanEntryOutcome.Matched),
            ("second", RestorePlanEntryOutcome.Matched));
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries(
                (0, RestoreExecutionEntryStatus.Restored),
                (1, RestoreExecutionEntryStatus.Restored)));

        RestoreOutcomeRecord record = Project(plan, result);

        Assert.Equal(2, record.Windows.Planned);
        Assert.Equal(2, record.Windows.Succeeded);
    }

    [Fact]
    public void Start_and_complete_produce_one_idempotent_record_for_the_operation()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched));
        DateTime started = new(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc);

        string operationId = storage.RestoreOutcomes.Begin(plan, started);
        RestoreOutcomeRecord running = Assert.Single(storage.RestoreOutcomes.Read());
        Assert.False(running.IsComplete);
        Assert.Equal(1, running.Windows.Planned);
        Assert.Equal(0, running.Windows.Succeeded);
        Assert.Equal(RestoreOutcomeStatus.Interrupted, running.Status);

        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries((0, RestoreExecutionEntryStatus.Restored)));

        // 完成时覆盖写入同一个操作标识，并重复写一次，记录数量不变。
        storage.RestoreOutcomes.Complete(operationId, plan, result, started);
        storage.RestoreOutcomes.Complete(operationId, plan, result, started);

        RestoreOutcomeRecord completed = Assert.Single(storage.RestoreOutcomes.Read());
        Assert.Equal(operationId, completed.OperationId);
        Assert.True(completed.IsComplete);
        Assert.Equal(RestoreOutcomeStatus.Succeeded, completed.Status);
        Assert.Equal(1, completed.Windows.Succeeded);
        Assert.Single(Directory.GetFiles(Path.Combine(directory.Path, "restore-results"), "*.json"));
    }

    [Fact]
    public void A_record_without_a_final_result_is_reported_as_interrupted_never_as_success()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched));

        storage.RestoreOutcomes.Begin(plan, DateTime.UtcNow);

        RestoreOutcomeRecord record = Assert.Single(storage.RestoreOutcomes.Read());
        Assert.Equal(RestoreOutcomeStatus.Interrupted, record.Status);
        Assert.Equal(0, record.Windows.Succeeded);
        Assert.Null(record.EndedAtUtc);
    }

    [Fact]
    public void A_persist_failure_never_throws_and_is_reported_separately()
    {
        using var directory = new TestDirectory();
        var store = new RestoreOutcomeStore(directory.Path, new ThrowingAtomicFileWriter());
        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched));

        string operationId = store.Begin(plan, DateTime.UtcNow);
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries((0, RestoreExecutionEntryStatus.Restored)));
        store.Complete(operationId, plan, result, DateTime.UtcNow);

        // 写入失败既不能抛出，也不能留下一条声称成功的历史记录。
        Assert.True(store.WriteFailed);
        Assert.Empty(store.Read());
    }

    [Fact]
    public void Retention_keeps_the_history_bounded()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        string resultsDirectory = Path.Combine(directory.Path, "restore-results");
        Directory.CreateDirectory(resultsDirectory);

        // 直接铺满历史文件，然后触发一次真实写入来观察裁剪结果。
        for (int index = 0; index < RestoreOutcomeStore.MaximumRecords + 12; index++)
        {
            string id = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(resultsDirectory, id + ".json"), "{}");
            File.SetLastWriteTimeUtc(
                Path.Combine(resultsDirectory, id + ".json"),
                DateTime.UtcNow.AddMinutes(index));
        }

        storage.RestoreOutcomes.Begin(Plan(("a", RestorePlanEntryOutcome.Matched)), DateTime.UtcNow);

        Assert.True(Directory.GetFiles(resultsDirectory, "*.json").Length <= RestoreOutcomeStore.MaximumRecords);
    }

    [Fact]
    public void A_result_is_linked_to_its_checkpoint_by_identifier_not_by_time()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var named = new WorkspaceSnapshot { Name = "Desk", SavedAt = DateTime.UtcNow };
        storage.NamedWorkspaces.Save(named);

        storage.Checkpoints.Save(
            new WorkspaceSnapshot { Name = "Before", SavedAt = DateTime.UtcNow },
            WorkspaceCheckpointTrigger.Restore,
            named.WorkspaceId);
        string checkpointId = Assert.Single(storage.Checkpoints.GetRetainedHistory()).Checkpoint!.CheckpointId;

        RestorePlan plan = Plan(("a", RestorePlanEntryOutcome.Matched)) with
        {
            WorkspaceId = named.WorkspaceId,
            WorkspaceName = named.Name,
        };
        RestoreExecutionResult result = Result(
            plan,
            RestoreExecutionStatus.Completed,
            Entries((0, RestoreExecutionEntryStatus.Restored))) with
        {
            Checkpoint = new RestoreCheckpointOutcome(
                RestoreCheckpointStatus.Created,
                WorkspaceCheckpointTrigger.Restore,
                checkpointId,
                DateTime.UtcNow,
                "Checkpoint captured.",
                TimeSpan.FromSeconds(1)),
        };

        string operationId = storage.RestoreOutcomes.Begin(plan, DateTime.UtcNow);
        storage.RestoreOutcomes.Complete(operationId, plan, result, DateTime.UtcNow);

        var reader = new WorkspaceActivityReader(storage);
        WorkspaceActivityEntry[] entries = reader.Read().ToArray();

        // 同一次恢复只应产生一条活动：开始与结果按检查点标识合并，列表里不出现第二行。
        WorkspaceActivityEntry operation = Assert.Single(entries, entry => entry.Outcome is not null);
        Assert.Equal(checkpointId, operation.CheckpointId);
        Assert.Equal(WorkspaceActivityKind.Checkpoint, operation.Kind);
        Assert.Equal(RestoreOutcomeStatus.Succeeded, operation.Outcome!.Status);
        Assert.Equal(1, operation.Outcome.Windows.Succeeded);
        Assert.Single(entries, entry => entry.Category != WorkspaceActivityCategory.Saved);
    }

    [Fact]
    public void A_checkpoint_without_a_saved_result_is_reported_as_unknown()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var named = new WorkspaceSnapshot { Name = "Desk", SavedAt = DateTime.UtcNow };
        storage.NamedWorkspaces.Save(named);
        storage.Checkpoints.Save(
            new WorkspaceSnapshot { Name = "Before", SavedAt = DateTime.UtcNow },
            WorkspaceCheckpointTrigger.Restore,
            named.WorkspaceId);

        var reader = new WorkspaceActivityReader(storage);

        WorkspaceActivityEntry operation = Assert.Single(
            reader.Read(),
            entry => entry.Kind == WorkspaceActivityKind.Checkpoint);
        Assert.Null(operation.Outcome);
        Assert.Equal(WorkspaceActivityCategory.Restore, operation.Category);
    }

    // ── 装置 ──────────────────────────────────────────────────────────────────

    private static readonly DateTime Started = new(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc);

    private static RestoreOutcomeRecord Project(RestorePlan plan, RestoreExecutionResult result) =>
        RestoreOutcomeProjector.Project("0123456789abcdef0123456789abcdef", plan, result, Started, Started.AddSeconds(4));

    private static int Sum(RestoreOutcomeCounts counts) =>
        counts.Succeeded + counts.Failed + counts.Skipped + counts.Cancelled +
        counts.Blocked + counts.Stale + counts.Unconfirmed;

    /// <summary>每个目标只归入一个状态，因此各状态之和必须等于计划数。</summary>
    private static void AssertClosed(RestoreOutcomeCounts counts) =>
        Assert.Equal(counts.Planned, Sum(counts));

    private static RestorePlan Plan(params (string Id, RestorePlanEntryOutcome Outcome)[] entries) => new()
    {
        WorkspaceId = "workspace-1",
        WorkspaceName = "Desk",
        SnapshotSavedAt = Started,
        Mode = RestoreModeKind.Resume,
        Entries = entries.Select((entry, index) => Entry(index, entry.Id, entry.Outcome)).ToArray(),
        Actions = Array.Empty<RestoreAction>(),
    };

    private static RestorePlanEntry Entry(int index, string id, RestorePlanEntryOutcome outcome) => new(
        index,
        id,
        outcome,
        "Planned.",
        new SavedWindowIdentity { EntryId = id, ProcessName = "editor" },
        Array.Empty<RestorePlanCandidate>(),
        null,
        new RestoreTargetPlacement(
            "monitor-1", 0, RestoreMonitorMappingKind.ExactStableId,
            0, 0, 800, 600, 1, 96, 96, false),
        RestoreLaunchRequirement.None("Nothing to launch."),
        Array.Empty<RestoreAction>(),
        Array.Empty<RestorePlanIssue>(),
        Array.Empty<RestorePlanIssue>());

    private static RestoreExecutionResult Result(
        RestorePlan plan,
        RestoreExecutionStatus status,
        IReadOnlyList<RestoreExecutionEntryResult> entries,
        IReadOnlyList<RestoreExecutionActionResult>? actions = null,
        bool wasCancelled = false) =>
        new(
            plan.WorkspaceId,
            status,
            wasCancelled,
            entries,
            actions ?? Array.Empty<RestoreExecutionActionResult>(),
            new HashSet<long>());

    private static IReadOnlyList<RestoreExecutionEntryResult> Entries(
        params (int Index, RestoreExecutionEntryStatus Status)[] entries) =>
        entries
            .Select(entry => new RestoreExecutionEntryResult(
                entry.Index,
                "entry-" + entry.Index,
                entry.Status,
                null,
                "Executor explanation."))
            .ToArray();

    private static IReadOnlyList<RestoreExecutionActionResult> Actions(
        params (int Index, int? EntryIndex, RestoreActionKind Kind, RestoreExecutionActionStatus Status)[] actions) =>
        actions
            .Select(action => new RestoreExecutionActionResult(
                action.Index,
                action.EntryIndex,
                action.Kind,
                action.Status,
                null,
                null,
                "Executor explanation."))
            .ToArray();
}
