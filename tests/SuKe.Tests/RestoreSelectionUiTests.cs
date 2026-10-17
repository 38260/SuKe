using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;
using SuKe.UI;
using WpfButton = Wpf.Ui.Controls.Button;

namespace SuKe.Tests;

[Collection("WPF presentation")]
public class RestoreSelectionUiTests
{
    [Fact]
    public void Reference_example_shows_eight_apps_ten_windows_and_five_restorable()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanPreviewDialog dialog = Open(ReferencePlan());
            try
            {
                Assert.Equal(8, GroupChecks(dialog).Length);
                // Only the multi-window application exposes child rows; single-window apps use the group box.
                Assert.Equal(3, WindowChecks(dialog).Length);
                Assert.True(GroupChecks(dialog).All(check => check.IsChecked == true));
                Assert.True(Master(dialog).IsChecked);

                string text = TextContent(dialog);
                Assert.Contains(Loc.Format("{0} apps · {1} windows", 8, 10), text);
                Assert.Contains(Loc.Format("Restore {0} · skip {1}", 2, 1), text);
                Assert.Contains(
                    Loc.Format("Selected {0} windows · restoring {1} · skipping {2} already running",
                        10, 5, 5),
                    text);
                Assert.Equal(Loc.Format("Restore {0} windows", 5), Primary(dialog).Content);
                Assert.True(Primary(dialog).IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Select_all_group_and_window_boxes_share_three_states()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanPreviewDialog dialog = Open(ReferencePlan());
            try
            {
                CheckBox master = Master(dialog);
                CheckBox chrome = GroupChecks(dialog)
                    .Single(check => AutomationProperties.GetName(check)!
                        .Contains("Chrome", StringComparison.OrdinalIgnoreCase));
                CheckBox[] windows = WindowChecks(dialog);

                Assert.True(chrome.IsChecked);

                // One window cleared: the group and the select-all box become partial.
                windows[0].IsChecked = false;
                Assert.Null(chrome.IsChecked);
                Assert.Null(master.IsChecked);
                Assert.Contains(
                    Loc.Format("Selected {0} windows · restoring {1} · skipping {2} already running",
                        9, 4, 5),
                    TextContent(dialog));
                Assert.Equal(Loc.Format("Restore {0} windows", 4), Primary(dialog).Content);

                foreach (CheckBox window in windows) window.IsChecked = false;
                Assert.False(chrome.IsChecked);

                // Clicking a partial group selects every window again.
                windows[0].IsChecked = true;
                Click(chrome);
                Assert.True(chrome.IsChecked);
                Assert.All(windows, window => Assert.True(window.IsChecked));

                Click(master);
                Assert.False(master.IsChecked);
                Assert.All(GroupChecks(dialog), check => Assert.False(check.IsChecked));
                Assert.All(WindowChecks(dialog), window => Assert.False(window.IsChecked));
                Assert.False(Primary(dialog).IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Expanding_and_collapsing_a_group_does_not_change_selection()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanPreviewDialog dialog = Open(ReferencePlan());
            try
            {
                CheckBox[] before = WindowChecks(dialog);
                bool[] states = before.Select(check => check.IsChecked == true).ToArray();

                WpfButton collapse = Toggle(dialog, "Chrome");
                Click(collapse);
                Assert.All(WindowChecks(dialog), check => Assert.Equal(Visibility.Collapsed,
                    AncestorVisibility(check)));
                Assert.Equal(states, WindowChecks(dialog).Select(check => check.IsChecked == true));

                Click(collapse);
                Assert.All(WindowChecks(dialog), check => Assert.Equal(Visibility.Visible,
                    AncestorVisibility(check)));
                Assert.Equal(states, WindowChecks(dialog).Select(check => check.IsChecked == true));
                Assert.Contains(
                    Loc.Format("Selected {0} windows · restoring {1} · skipping {2} already running",
                        10, 5, 5),
                    TextContent(dialog));
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Empty_plan_has_disabled_unchecked_select_all()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanPreviewDialog dialog = Open(new RestorePlan());
            try
            {
                Assert.False(Master(dialog).IsEnabled);
                Assert.False(Master(dialog).IsChecked);
                Assert.Empty(GroupChecks(dialog));
                Assert.False(Primary(dialog).IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Selected_running_item_can_be_toggled_and_approved_as_skipped()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanEntry running = RunningEntry(0, "Code", @"C:\Apps\Code.exe", "项目 A");
            RestorePlanPreviewDialog dialog = Open(new RestorePlan
            {
                Mode = RestoreModeKind.Resume, Entries = [running], Actions = running.Actions,
            });
            // 模态窗口先建立真实所属窗口，与生产界面提供 Owner 的方式一致。
            var owner = new Window { Width = 400, Height = 300, ShowInTaskbar = false };
            owner.Show();
            dialog.Owner = owner;
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        CheckBox group = Assert.Single(GroupChecks(dialog));
                        Assert.True(group.IsChecked);
                        Assert.Contains(Loc.T("Already running · skipped"), TextContent(dialog));

                        Click(group);
                        Assert.False(group.IsChecked);

                        Click(group);
                        Assert.True(dialog.ShowDialog());
                        Assert.True(Assert.Single(dialog.ApprovedPlan!.Entries).KeepRunning);
                        Assert.Empty(dialog.ApprovedPlan.Actions);
                    }
                    finally { dialog.Close(); owner.Close(); }
                }));
        });
    }

    [Fact]
    public void Blocking_entry_is_reported_but_not_counted_as_restorable()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            RestorePlanEntry blocked = Entry(0, RestorePlanEntryOutcome.Blocked, "Doc",
                @"C://Apps//Doc.exe", "缺失文档") with
            {
                BlockingErrors =
                [
                    new RestorePlanIssue(
                        RestorePlanIssueCode.MissingResource,
                        RestorePlanIssueSeverity.BlockingError,
                        "The saved document is missing.")
                ]
            };
            RestorePlanEntry ready = Entry(1, RestorePlanEntryOutcome.LaunchRequired, "Sheet",
                @"C://Apps//Sheet.exe", "正常文档");
            RestorePlanPreviewDialog dialog = Open(new RestorePlan
            {
                Mode = RestoreModeKind.Resume,
                Entries = [blocked, ready],
                Actions = ready.Actions,
                BlockingErrors = blocked.BlockingErrors
            });
            try
            {
                string text = TextContent(dialog);
                Assert.Contains(
                    Loc.Format("Selected {0} windows · restoring {1} · skipping {2} already running",
                        2, 1, 0),
                    text);
                Assert.Contains(Loc.T("Not restorable"), text);
                // A blocking entry keeps the approval disabled until it is cleared.
                Assert.False(Primary(dialog).IsEnabled);

                // 单窗口分组框本身就是该窗口的勾选框，直接改状态等同于用户点击。
                GroupChecks(dialog).Single(check =>
                    AutomationProperties.GetName(check)!.Contains("Doc", StringComparison.Ordinal))
                    .IsChecked = false;
                Assert.Contains(
                    Loc.Format("Selected {0} windows · restoring {1} · skipping {2} already running",
                        1, 1, 0),
                    TextContent(dialog));
                Assert.True(Primary(dialog).IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static RestorePlanPreviewDialog Prepare(RestorePlan plan)
    {
        var dialog = new RestorePlanPreviewDialog(plan);
        dialog.Resources.MergedDictionaries.Add(ActivityDetailsUiTests.Resources());
        dialog.ShowInTaskbar = false;
        return dialog;
    }

    private static RestorePlanPreviewDialog Open(RestorePlan plan)
    {
        RestorePlanPreviewDialog dialog = Prepare(plan);
        dialog.Show();
        SmoothScrollUiTests.Pump(60);
        return dialog;
    }

    internal static CheckBox Master(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests
        .Walk(dialog).OfType<CheckBox>()
        .Single(check => AutomationProperties.GetName(check) == Loc.T("Select all restore entries"));

    // 无障碍名称会随界面语言翻译，因此用词条本身拼出前后缀，断言不依赖英文原文。
    private static string GroupNameSuffix =>
        Loc.Format("Include every {0} window", "\u0001").Split('\u0001')[1];

    private static string GroupNamePrefix =>
        Loc.Format("Include every {0} window", "\u0001").Split('\u0001')[0];

    internal static CheckBox[] GroupChecks(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests
        .Walk(dialog).OfType<CheckBox>()
        .Where(check => AutomationProperties.GetName(check) is { } name &&
            name.StartsWith(GroupNamePrefix, StringComparison.Ordinal) &&
            name.EndsWith(GroupNameSuffix, StringComparison.Ordinal))
        .OrderBy(check => check.Tag)
        .ToArray();

    internal static CheckBox[] WindowChecks(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests
        .Walk(dialog).OfType<CheckBox>()
        .Where(check => AutomationProperties.GetName(check) is { } name &&
            name.StartsWith(GroupNamePrefix, StringComparison.Ordinal) &&
            !name.EndsWith(GroupNameSuffix, StringComparison.Ordinal))
        .OrderBy(check => check.Tag)
        .ToArray();

    internal static WpfButton Primary(RestorePlanPreviewDialog dialog) => ActivityDetailsUiTests
        .Walk(dialog).OfType<WpfButton>().Single(button => button.IsDefault);

    private static WpfButton Toggle(RestorePlanPreviewDialog dialog, string appName) =>
        ActivityDetailsUiTests.Walk(dialog).OfType<WpfButton>()
            .Single(button => AutomationProperties.GetName(button) is { } name &&
                name.Contains(appName, StringComparison.OrdinalIgnoreCase));

    internal static void Click(CheckBox check) =>
        check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    internal static void Click(WpfButton button) =>
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    internal static string TextContent(DependencyObject root) => string.Join("\n",
        ActivityDetailsUiTests.Walk(root).OfType<TextBlock>().Select(text => text.Text));

    private static Visibility AncestorVisibility(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: Visibility.Collapsed }) return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    /// <summary>验收样例：8 个应用 / 10 个窗口，5 个待恢复、5 个已运行跳过。</summary>
    /// <summary>四条目基准计划：一个已匹配、一个需启动、一个排除、一个取消。</summary>
    internal static RestorePlan Plan()
    {
        RestorePlanEntry[] entries = Enumerable.Range(0, 4).Select(index => Entry(index, index switch
        {
            0 => RestorePlanEntryOutcome.Matched,
            1 => RestorePlanEntryOutcome.LaunchRequired,
            2 => RestorePlanEntryOutcome.Excluded,
            _ => RestorePlanEntryOutcome.Cancelled,
        })).ToArray();
        return new RestorePlan
        {
            WorkspaceName = "Selection",
            Mode = RestoreModeKind.Resume,
            Entries = entries,
            Actions = entries.SelectMany(entry => entry.Actions).ToArray()
        };
    }

    internal static RestorePlanEntry Entry(int index, RestorePlanEntryOutcome outcome) => new(
        index,
        "entry-" + index,
        outcome,
        "Fixture",
        new SavedWindowIdentity { ProcessName = "app-" + index },
        [],
        index == 0
            ? new RestorePlanCandidate(100, 123, true, 100, WindowMatchConfidence.Exact, [], null, false)
            : null,
        new RestoreTargetPlacement("screen", 0, RestoreMonitorMappingKind.ExactStableId,
            0, 0, 800, 600, 1, 96, 96, false),
        RestoreLaunchRequirement.None("Fixture"),
        outcome is RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Cancelled
            ? Array.Empty<RestoreAction>()
            :
            [
                new RestoreAction(index,
                    index == 0 ? RestoreActionKind.RestoreExistingWindow : RestoreActionKind.LaunchApplication,
                    index == 0 ? 100 : null, "", "", false, null, "Fixture")
            ],
        [],
        []);

    internal static RestorePlan ReferencePlan()
    {
        string chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        string edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        string explorer = @"C:\Windows\explorer.exe";

        RestorePlanEntry[] entries =
        [
            Entry(0, RestorePlanEntryOutcome.LaunchRequired, "Code", @"C:\Apps\Code.exe", "项目 A"),
            Entry(1, RestorePlanEntryOutcome.LaunchRequired, "explorer", explorer, "项目资料"),
            Entry(2, RestorePlanEntryOutcome.LaunchRequired, "WindowsTerminal",
                @"C:\Apps\WindowsTerminal.exe", "开发终端"),
            Entry(3, RestorePlanEntryOutcome.LaunchRequired, "chrome", chrome, "项目文档"),
            Entry(4, RestorePlanEntryOutcome.LaunchRequired, "chrome", chrome, "调试页面"),
            RunningEntry(5, "chrome", chrome, "常用页面"),
            RunningEntry(6, "msedge", edge, "参考文档"),
            RunningEntry(7, "Figma", @"C:\Apps\Figma.exe", "产品设计"),
            RunningEntry(8, "WeChat", @"C:\Apps\WeChat.exe", "消息"),
            RunningEntry(9, "Obsidian", @"C:\Apps\Obsidian.exe", "项目笔记"),
        ];
        return new RestorePlan
        {
            WorkspaceId = "11111111-1111-4111-8111-111111111111",
            WorkspaceName = "开发环境",
            Mode = RestoreModeKind.Resume,
            Entries = entries,
            Actions = entries.SelectMany(entry => entry.Actions).ToArray()
        };
    }

    internal static RestorePlanEntry Entry(int index, RestorePlanEntryOutcome outcome,
        string processName, string executablePath, string title,
        IReadOnlyList<RestorePlanCandidate>? candidates = null,
        RestorePlanCandidate? selected = null,
        IReadOnlyList<RestorePlanIssue>? warnings = null,
        IReadOnlyList<RestorePlanIssue>? blockingErrors = null) => new(
        index,
        "entry-" + index,
        outcome,
        "Fixture",
        new SavedWindowIdentity
        {
            EntryId = "entry-" + index,
            ProcessName = processName,
            ExecutablePath = executablePath,
            Title = title
        },
        candidates ?? (selected is null ? [] : [selected]),
        selected,
        new RestoreTargetPlacement("DISPLAY1", 0, RestoreMonitorMappingKind.ExactStableId,
            0, 0, 1280, 720, 1, 96, 96, false),
        RestoreLaunchRequirement.None("Fixture"),
        outcome is RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Cancelled
            ? Array.Empty<RestoreAction>()
            :
            [
                new RestoreAction(index, RestoreActionKind.LaunchApplication, null, executablePath,
                    "", false, null, "Fixture")
            ],
        warnings ?? [],
        blockingErrors ?? []);

    internal static RestorePlanEntry RunningEntry(int index, string processName,
        string executablePath, string title) => Entry(
        index,
        RestorePlanEntryOutcome.Matched,
        processName,
        executablePath,
        title,
        [new RestorePlanCandidate(900 + index, (uint)(4000 + index), true, 100,
            WindowMatchConfidence.Exact, [], null, false)],
        new RestorePlanCandidate(900 + index, (uint)(4000 + index), true, 100,
            WindowMatchConfidence.Exact, [], null, false));
}
