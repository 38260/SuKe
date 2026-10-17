using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SuKe.Localization;
using SuKe.Models;
using Wpf.Ui.Controls;

namespace SuKe.UI.Shell;

public partial class ActivityPage
{
    private void OnFilterUnchecked(object sender, RoutedEventArgs e)
    {
        // 单选筛选项始终保留一个选中项，避免界面与实际过滤条件不一致。
        if (_pageReady && !_applyingFilter && ReferenceEquals(sender, _activeChip))
            _activeChip!.IsChecked = true;
    }

    private void OnActivityDetails(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ActivityRowItem row }) return;
        CreateDetailsWindow(row, Window.GetWindow(this)).ShowDialog();
    }

    internal static FluentWindow CreateDetailsWindow(ActivityRowItem row, Window? owner)
    {
        var content = new StackPanel { Margin = new Thickness(24, 12, 24, 24) };

        var title = new System.Windows.Controls.TextBlock
        {
            Text = row.Title, FontSize = 18, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        Loc.SetSkip(title, true);
        content.Children.Add(title);
        content.Children.Add(Line(Loc.Format("Workspace: {0}",
            row.Entry.WorkspaceName ?? Loc.T("Not recorded")), 13, top: 8));
        content.Children.Add(Line(Loc.Format("Operation: {0}", row.Entry.Trigger is { } trigger
            ? RestoreOutcomeText.OperationLabel(trigger)
            : row.Entry.Kind == WorkspaceActivityKind.WorkspaceDeleted
                ? Loc.T("Workspace deleted")
                : Loc.T("Workspace saved")), 13, secondary: true, top: 3));

        // 时间范围与耗时：只在真正记录了结果时展示，旧记录不显示 0 秒这种假数据。
        if (row.Entry.Outcome is RestoreOutcomeRecord timed)
        {
            content.Children.Add(Line(
                Loc.Format("Started {0}", timed.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                13, secondary: true, top: 6));
            content.Children.Add(timed.EndedAtUtc is { } endedUtc
                ? Line(
                    Loc.Format("Ended {0}", endedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    13, secondary: true, top: 2)
                : Line(Loc.T("Still in progress"), 13, secondary: true, top: 2));
            if (timed.EndedAtUtc is not null)
            {
                content.Children.Add(Line(
                    Loc.Format("Duration {0}", RestoreOutcomeText.Duration(timed.DurationMs)),
                    13, secondary: true, top: 2));
            }
        }
        else
        {
            content.Children.Add(Line(
                row.Entry.OccurredAtLocal.ToString("yyyy-MM-dd HH:mm:ss"), 13, secondary: true, top: 6));
        }

        if (row.Entry.Outcome is RestoreOutcomeRecord outcome)
        {
            content.Children.Add(Line(
                Loc.Format("Overall result: {0}", RestoreOutcomeText.StatusLabel(outcome.Status)),
                15, top: 10));

            IReadOnlyList<string> counts = RestoreOutcomeText.CountLines(outcome);
            if (counts.Count == 0)
            {
                content.Children.Add(Line(
                    Loc.T("Nothing was recorded for this operation."), 13, secondary: true, top: 4));
            }
            else
            {
                foreach (string line in counts)
                    content.Children.Add(Line("•  " + line, 13, top: 3));
            }

            content.Children.Add(Line(
                Loc.T("“Succeeded” only counts targets the executor confirmed finished. Requested launches that never appeared stay unconfirmed."),
                12, secondary: true, top: 8));

            // 失败与未完成项：可展开，长原因换行。
            if (outcome.Issues.Count > 0)
            {
                var issues = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
                foreach (RestoreOutcomeIssue issue in outcome.Issues)
                {
                    issues.Children.Add(Line(
                        "•  " + DescribeIssueHeadline(issue), 13, top: 6));
                    if (issue.Description.Length > 0)
                        issues.Children.Add(Line(Loc.T(issue.Description), 12, secondary: true, top: 1, left: 14));
                }

                content.Children.Add(new Expander
                {
                    Header = new System.Windows.Controls.TextBlock
                    {
                        Text = Loc.Format("Failed and incomplete items ({0})", outcome.Issues.Count),
                        FontSize = 14, FontWeight = FontWeights.SemiBold,
                    },
                    IsExpanded = true,
                    Margin = new Thickness(0, 12, 0, 0),
                    Content = issues,
                });
            }
            else
            {
                content.Children.Add(Line(
                    Loc.T("No failures or incomplete items were recorded."), 13, secondary: true, top: 8));
            }
        }
        else if (row.Entry.Category == WorkspaceActivityCategory.Restore)
        {
            // 这是一次恢复类操作，但没有落盘的结果记录（旧记录、已过期，或进程中断）：
            // 明确说明结果未知，绝不推断成功。
            content.Children.Add(Line(RestoreOutcomeText.UnknownResultSummary(), 14, top: 10));
            content.Children.Add(Line(
                Loc.T("Older records did not store a restore result, so no success or failure can be shown."),
                12, secondary: true, top: 4));
        }
        else if (row.Entry.Kind == WorkspaceActivityKind.WorkspaceDeleted)
        {
            // 删除记录没有“恢复结果”可言；它只说明这个工作区曾经存在、当时存了什么。
            content.Children.Add(Line(Loc.T("This workspace was deleted. The summary below is its last saved content."),
                14, top: 10));
            if (row.Entry.SavedAtLocal is { } savedAt)
            {
                content.Children.Add(Line(
                    Loc.Format("Last saved {0}", savedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                    12, secondary: true, top: 4));
            }
            content.Children.Add(Line(
                Loc.T("Deleted workspaces cannot be restored from this record; it is kept only as history."),
                12, secondary: true, top: 4));
        }
        else
        {
            // 保存事件本身没有“恢复结果”，这里只陈述这个工作区保存了什么。
            content.Children.Add(Line(
                Loc.Format(
                    "{0} window(s) across {1} monitor(s)",
                    row.Entry.WindowCount,
                    row.Entry.MonitorCount),
                14, top: 10));
            content.Children.Add(Line(
                Loc.T("This is a saved workspace. Open a restore entry to see window and tab results."),
                12, secondary: true, top: 4));
        }

        AddSnapshotDetails(content, row.Entry);
        content.Children.Add(Line(DescribeEvidence(row.Entry), 12, secondary: true, top: 16));
        var identifiers = new StackPanel();
        if (row.Entry.WorkspaceId is { Length: > 0 } workspaceId)
            identifiers.Children.Add(Line(Loc.Format("Workspace ID: {0}", workspaceId), 12, top: 4));
        if (row.Entry.CheckpointId is { Length: > 0 } checkpointId)
            identifiers.Children.Add(Line(Loc.Format("Checkpoint ID: {0}", checkpointId), 12, top: 4));
        if (row.Entry.Outcome is { } recorded)
            identifiers.Children.Add(Line(Loc.Format("Operation ID: {0}", recorded.OperationId), 12, top: 4));
        if (identifiers.Children.Count > 0)
            content.Children.Add(new Expander
            {
                Header = Loc.T("Record identifiers"), Content = identifiers, Margin = new Thickness(0, 12, 0, 0),
            });

        var close = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T("Close"), IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(24, 8, 24, 20),
        };

        var layout = new DockPanel();
        var titleBar = new TitleBar { Title = Loc.T("Activity details"), ShowMinimize = false, ShowMaximize = false };
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        DockPanel.SetDock(close, Dock.Bottom);
        layout.Children.Add(close);
        layout.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var dialog = new FluentWindow
        {
            Title = Loc.T("Activity details"), Owner = owner, Content = layout,
            Width = 660, Height = 660, MinWidth = 440, MinHeight = 360,
            ExtendsContentIntoTitleBar = true, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        // 调色板由 ThemeService 在应用级维护，此对话框无需自带主题字典。
        dialog.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        dialog.SetResourceReference(Control.BackgroundProperty, "ApplicationBackgroundBrush");
        close.Click += (_, _) => dialog.Close();
        return dialog;
    }

    private static void AddSnapshotDetails(StackPanel content, WorkspaceActivityEntry entry)
    {
        content.Children.Add(Line(Loc.T(entry.Kind switch
        {
            WorkspaceActivityKind.WorkspaceSaved => "Saved content",
            WorkspaceActivityKind.WorkspaceDeleted => "Content of the deleted workspace",
            _ => "Desktop captured before this operation",
        }), 15, top: 16));
        if (entry.Snapshot is not { } snapshot)
        {
            content.Children.Add(Line(Loc.T("The original snapshot is no longer available. Only recorded execution results are shown."),
                12, secondary: true, top: 4));
            return;
        }
        content.Children.Add(Line(Loc.Format("{0} window(s) across {1} monitor(s)", entry.WindowCount, entry.MonitorCount), 13, top: 6));
        content.Children.Add(Line(Loc.Format("Browser tabs: {0} · Explorer tabs: {1} · Terminal tabs: {2}",
            snapshot.BrowserTabCount, snapshot.ExplorerTabCount, snapshot.TerminalTabCount), 13, top: 4));
        content.Children.Add(Line(Loc.Format("Virtual desktops: {0} · File/folder capture: {1}", snapshot.VirtualDesktopCount,
            Loc.T(snapshot.SavedWithFiles ? "Enabled" : "Disabled")), 13, top: 4));
        content.Children.Add(Line(Loc.T("Applications in snapshot"), 14, top: 12));
        foreach (WorkspaceActivityApplication app in snapshot.Applications)
            content.Children.Add(Line(Loc.Format("{0}: {1} window(s)", app.Name == "Application" ? Loc.T(app.Name) : app.Name, app.WindowCount), 13, top: 3));
        if (snapshot.Applications.Count == 0)
            content.Children.Add(Line(Loc.T("No windows in this snapshot."), 12, secondary: true, top: 3));
        content.Children.Add(Line(Loc.T("Monitor configuration"), 14, top: 12));
        foreach (WorkspaceActivityMonitor monitor in snapshot.Monitors)
            content.Children.Add(Line(Loc.Format("Monitor {0}: {1} · {2} × {3} · {4}% · {5} window(s){6}",
                monitor.Index + 1, monitor.Name, monitor.Width, monitor.Height,
                Math.Round(monitor.Dpi / 96.0 * 100), monitor.WindowCount,
                monitor.IsPrimary ? Loc.T(" · Primary") : ""), 13, top: 4));
        if (snapshot.Monitors.Count == 0)
            content.Children.Add(Line(Loc.T("Monitor configuration was not recorded."), 12, secondary: true, top: 3));
        if (snapshot.UnassignedWindowCount > 0)
            content.Children.Add(Line(Loc.Format("Unassigned monitor: {0} window(s)", snapshot.UnassignedWindowCount), 12, secondary: true, top: 4));
    }

    /// <summary>一条明细行；长文本换行，不使用巨大标题堆叠数字。</summary>
    private static System.Windows.Controls.TextBlock Line(
        string text,
        double fontSize,
        bool secondary = false,
        double top = 0,
        double left = 0)
    {
        var block = new System.Windows.Controls.TextBlock
        {
            Text = text,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(left, top, 0, 0),
        };
        Loc.SetSkip(block, true);
        block.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty,
            secondary ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush");
        return block;
    }

    /// <summary>“内容类型 · 应用 · 原因”，并保留原因码便于排查。</summary>
    private static string DescribeIssueHeadline(RestoreOutcomeIssue issue)
    {
        var parts = new List<string>(3);
        if (issue.ContentType.Length > 0)
            parts.Add(RestoreOutcomeText.ContentTypeLabel(issue.ContentType));
        if (issue.Subject.Length > 0)
            parts.Add(issue.Subject);
        string reason = RestoreOutcomeText.ReasonLabel(issue.ReasonCode);
        if (reason.Length > 0)
            parts.Add(reason + " (" + issue.ReasonCode + ")");
        return string.Join("  ·  ", parts);
    }

    internal static string DescribeEvidence(WorkspaceActivityEntry entry)
    {
        if (entry.Outcome is not null)
            return Loc.T("Source: the result recorded by the restore itself. Every number comes from the real execution result, not from the saved content.");

        return entry.Kind switch
        {
            WorkspaceActivityKind.WorkspaceSaved =>
                Loc.T("Source: the current saved workspace. This is its latest save time, not a complete save history."),
            WorkspaceActivityKind.WorkspaceDeleted =>
                Loc.T("Source: the deletion record written when the workspace was removed. It carries the last saved summary only, and contains no restore result."),
            _ =>
                Loc.T("Source: a retained recovery checkpoint. It records the desktop before an operation; it does not prove that the restore completed. Older checkpoints may expire."),
        };
    }
}

internal sealed partial class ActivityRowItem
{
    public required WorkspaceActivityEntry Entry { get; init; }
}
