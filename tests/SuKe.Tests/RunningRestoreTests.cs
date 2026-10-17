using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class RunningRestoreTests
{
    [Fact]
    public void Running_entries_have_no_mutations_and_missing_entries_keep_their_actions()
    {
        RestorePlan original = RestoreSelectionUiTests.Plan();
        RestorePlan approved = RestoreApprovalProjector.PreserveRunningEntries(original);
        Assert.True(approved.Entries[0].KeepRunning);
        Assert.Equal(RestorePlanEntryOutcome.Excluded, approved.Entries[0].Outcome);
        Assert.Empty(approved.Entries[0].Actions);
        Assert.False(approved.Entries[0].LaunchRequirement.IsRequired);
        Assert.Equal(1, Assert.Single(approved.Actions).EntryIndex);
        Assert.Contains(100, approved.ProtectedWindowHandles);
        Assert.Empty(approved.DisabledEntryIndexes);
        Assert.False(original.Entries[0].KeepRunning);
        Assert.Equal(2, original.Actions.Count);
        RestorePlanPreviewEntry displayed = RestorePlanPreviewBuilder.Build(approved).Entries[0];
        Assert.True(displayed.IsInitiallyEnabled);
        Assert.True(displayed.KeepRunning);
    }

    [Fact]
    public void Browser_and_explorer_sessions_of_running_windows_are_preserved()
    {
        RestorePlanEntry browser = RestoreSelectionUiTests.Entry(0, RestorePlanEntryOutcome.Matched) with
        {
            SavedIdentity = new SavedWindowIdentity { ProcessName = "chrome", BrowserFamily = "chrome" },
            ExplorerSession = new RestoreExplorerSession([@"D:\folder"], 0),
        };
        RestorePlanEntry missing = RestoreSelectionUiTests.Entry(1, RestorePlanEntryOutcome.LaunchRequired);
        RestoreBrowserSession Session(string id, string linked) => new("chrome", "", 0, 0, 0, 800, 600, "normal", [], [])
        {
            BrowserSessionId = id, LinkedEntryId = linked,
        };
        var global = new RestoreAction(null, RestoreActionKind.RestoreBrowserSession, null, "", "", false, null, "Browser");
        RestorePlan plan = new()
        {
            Mode = RestoreModeKind.Resume, Entries = [browser, missing],
            BrowserSessions = [Session("running", browser.EntryId), Session("missing", missing.EntryId), Session("legacy", "")],
            Actions = [global, .. browser.Actions, .. missing.Actions],
        };
        RestorePlan approved = RestoreApprovalProjector.PreserveRunningEntries(plan);
        Assert.Null(approved.Entries[0].ExplorerSession);
        Assert.Equal("missing", Assert.Single(approved.BrowserSessions).BrowserSessionId);
        Assert.Contains(approved.Actions, action => action.Kind == RestoreActionKind.RestoreBrowserSession);
        Assert.DoesNotContain(approved.Actions, action => action.EntryIndex == 0);

        approved = RestoreApprovalProjector.PreserveRunningEntries(plan with
        {
            BrowserSessions = [Session("running", browser.EntryId)],
            Actions = [global, .. browser.Actions, .. missing.Actions.Select(action => action with { Condition = RestoreActionCondition.BrowserSessionUnavailable })],
        });
        Assert.Empty(approved.BrowserSessions);
        Assert.Equal(RestoreActionCondition.Always, Assert.Single(approved.Actions).Condition);
    }

    [Theory]
    [InlineData(RestoreModeKind.Repair)]
    [InlineData(RestoreModeKind.MoveExisting)]
    [InlineData(RestoreModeKind.LaunchFresh)]
    [InlineData(RestoreModeKind.ExactSwitch)]
    [InlineData(RestoreModeKind.PreviewOnly)]
    [InlineData(RestoreModeKind.AlignAndMinimize)]
    public void Explicit_other_modes_keep_their_existing_meaning(RestoreModeKind mode)
    {
        RestorePlan plan = RestoreSelectionUiTests.Plan() with { Mode = mode };
        Assert.Same(plan, RestoreApprovalProjector.PreserveRunningEntries(plan));
    }

    [Fact]
    public void Ambiguous_matches_are_not_silently_skipped()
    {
        RestorePlan plan = RestoreSelectionUiTests.Plan();
        RestorePlanEntry ambiguous = plan.Entries[0] with
        {
            Warnings = [new RestorePlanIssue(RestorePlanIssueCode.AmbiguousMatch, RestorePlanIssueSeverity.Warning, "Choose a window")],
        };
        RestorePlan approved = RestoreApprovalProjector.PreserveRunningEntries(plan with { Entries = [ambiguous] });
        Assert.False(approved.Entries[0].KeepRunning);
    }

    [Fact]
    public async Task Real_executor_skips_all_running_targets_without_mutation_and_records_skipped_result()
    {
        RestorePlanEntry running = RestoreSelectionUiTests.Entry(0, RestorePlanEntryOutcome.Matched);
        RestorePlan plan = RestoreApprovalProjector.PreserveRunningEntries(new RestorePlan
        {
            WorkspaceId = Guid.NewGuid().ToString("D"),
            Mode = RestoreModeKind.Selective, Entries = [running], Actions = running.Actions,
            VirtualDesktopAssociationEnabled = true,
            VirtualDesktops = [new SavedVirtualDesktop { DesktopId = Guid.NewGuid().ToString("D") }],
        });
        var inventory = new FakeWindowInventory();
        var mutation = new RecordingWindowMutation();
        var launcher = new RecordingRestoreProcessLauncher();
        var browser = new FakeBrowserSessionConnector();
        var explorer = new FakeExplorerTabSessionRestorer();
        var desktops = new FakeVirtualDesktopAssociation();
        var executor = new RestoreExecutor(inventory, mutation, launcher, new FakeRestoreClock(), new FakeRestoreResourceBoundary(),
            browserConnector: browser, readinessProbe: new FakeAppReadinessProbe(inventory),
            explorerTabRestorer: explorer, virtualDesktops: desktops);
        RestoreExecutionResult result = await executor.ExecuteAsync(plan);
        Assert.Equal(RestoreExecutionEntryStatus.Excluded, Assert.Single(result.Entries).Status);
        Assert.Empty(mutation.Restores);
        Assert.Empty(mutation.MinimizeCalls);
        Assert.Empty(launcher.Launches);
        Assert.Equal(0, browser.RestoreCalls);
        Assert.Empty(explorer.Calls);
        Assert.Empty(desktops.MoveCalls);
        Assert.Empty(plan.VirtualDesktops);
        var record = RestoreOutcomeProjector.Project("test", plan, result, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        Assert.Equal(1, record.Windows.Planned);
        Assert.Equal(0, record.Windows.Succeeded);
        Assert.Equal(1, record.Windows.Skipped);
        Assert.Equal(RestoreOutcomeStatus.Skipped, record.Status);
        Assert.Equal("already_running", Assert.Single(record.Issues).ReasonCode);
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        DateTime started = DateTime.UtcNow.AddSeconds(-1);
        string id = storage.RestoreOutcomes.Begin(plan, started);
        storage.RestoreOutcomes.Complete(id, plan, result, started);
        WorkspaceActivityEntry activity = Assert.Single(new WorkspaceActivityReader(storage).Read());
        Assert.Equal(RestoreOutcomeStatus.Skipped, activity.Outcome!.Status);
        Assert.Equal(1, activity.Outcome.Windows.Skipped);
    }
}
