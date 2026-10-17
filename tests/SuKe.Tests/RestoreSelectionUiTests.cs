using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SuKe.Models;
using SuKe.Services;
using SuKe.UI;

namespace SuKe.Tests;

[Collection("WPF presentation")]
public class RestoreSelectionUiTests
{
    [Fact]
    public void Select_all_and_individual_choices_stay_in_sync_without_enabling_unavailable_entries()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var dialog = new RestorePlanPreviewDialog(Plan());
            dialog.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
            try
            {
                dialog.Show();
                SmoothScrollUiTests.Pump(30);
                var all = Master(dialog);
                var rows = Rows(dialog);
                Assert.True(all.IsChecked);
                Click(all);
                Assert.False(all.IsChecked);
                Assert.All(rows, row => Assert.False(row.IsChecked));
                rows[0].IsChecked = true;
                Assert.Null(all.IsChecked);
                Click(all);
                Assert.True(all.IsChecked);
                Assert.True(rows[0].IsChecked);
                Assert.True(rows[1].IsChecked);
                Assert.False(rows[2].IsEnabled);
                Assert.False(rows[2].IsChecked);
                Assert.False(rows[3].IsEnabled);
                Assert.False(rows[3].IsChecked);
                rows[1].IsChecked = false;
                Assert.Null(all.IsChecked);
                Click(all);
                Click(all);
                Assert.False(all.IsChecked);
                var restore = ActivityDetailsUiTests.Walk(dialog).OfType<Wpf.Ui.Controls.Button>()
                    .Single(button => button.IsDefault);
                Assert.False(restore.IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Empty_plan_has_disabled_unchecked_select_all()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var dialog = new RestorePlanPreviewDialog(new RestorePlan());
            dialog.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
            try
            {
                dialog.Show();
                Assert.False(Master(dialog).IsEnabled);
                Assert.False(Master(dialog).IsChecked);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Selected_running_item_can_be_toggled_and_approved_as_skipped()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanEntry running = Entry(0, RestorePlanEntryOutcome.Matched);
            var dialog = new RestorePlanPreviewDialog(new RestorePlan
            {
                Mode = RestoreModeKind.Resume, Entries = [running], Actions = running.Actions,
            });
            dialog.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
            // 模态窗口先建立真实所属窗口，与生产界面提供 Owner 的方式一致。
            var owner = new Window { Width = 400, Height = 300, ShowInTaskbar = false };
            owner.Show();
            dialog.Owner = owner;
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var all = Master(dialog);
                    Assert.True(Assert.Single(Rows(dialog)).IsChecked);
                    Click(all);
                    Assert.False(Rows(dialog)[0].IsChecked);
                    Click(all);
                    var restore = ActivityDetailsUiTests.Walk(dialog).OfType<Wpf.Ui.Controls.Button>().Single(button => button.IsDefault);
                    Assert.True(restore.IsEnabled);
                    restore.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }
                catch { dialog.Close(); throw; }
            }));
            try
            {
                Assert.True(dialog.ShowDialog());
                Assert.True(Assert.Single(dialog.ApprovedPlan!.Entries).KeepRunning);
                Assert.Empty(dialog.ApprovedPlan.Actions);
                Assert.Empty(dialog.ApprovedPlan.DisabledEntryIndexes);
            }
            finally { dialog.Close(); owner.Close(); }
        });
    }

    internal static CheckBox Master(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests.Walk(dialog)
        .OfType<CheckBox>().Single(check => AutomationProperties.GetName(check) == "Select all restore entries");

    internal static CheckBox[] Rows(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests.Walk(dialog)
        .OfType<CheckBox>().Where(check => check.Tag is int).OrderBy(check => (int)check.Tag).ToArray();

    internal static void Click(CheckBox check) => check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    internal static RestorePlan Plan()
    {
        RestorePlanEntry[] entries = Enumerable.Range(0, 4).Select(index => Entry(index, index switch
        {
            0 => RestorePlanEntryOutcome.Matched,
            1 => RestorePlanEntryOutcome.LaunchRequired,
            2 => RestorePlanEntryOutcome.Excluded,
            _ => RestorePlanEntryOutcome.Cancelled,
        })).ToArray();
        return new RestorePlan { WorkspaceName = "Selection", Mode = RestoreModeKind.Resume, Entries = entries, Actions = entries.SelectMany(entry => entry.Actions).ToArray() };
    }

    internal static RestorePlanEntry Entry(int index, RestorePlanEntryOutcome outcome) => new(
        index, "entry-" + index, outcome, "Fixture", new SavedWindowIdentity { ProcessName = "app-" + index }, [],
        index == 0 ? new RestorePlanCandidate(100, 123, true, 100, WindowMatchConfidence.Exact, [], null, false) : null,
        new RestoreTargetPlacement("screen", 0, RestoreMonitorMappingKind.ExactStableId, 0, 0, 800, 600, 1, 96, 96, false),
        RestoreLaunchRequirement.None("Fixture"),
        outcome is RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Cancelled ? Array.Empty<RestoreAction>() :
        [new RestoreAction(index, index == 0 ? RestoreActionKind.RestoreExistingWindow : RestoreActionKind.LaunchApplication,
            index == 0 ? 100 : null, "", "", false, null, "Fixture")], [], []);
}
