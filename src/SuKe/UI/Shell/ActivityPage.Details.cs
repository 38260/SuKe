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
                        issues.Children.Add(Line(issue.Description, 12, secondary: true, top: 1, left: 14));
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

        content.Children.Add(Line(DescribeEvidence(row.Entry), 12, secondary: true, top: 16));

        var close = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T("Close"), IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        content.Children.Add(close);

        var layout = new DockPanel();
        var titleBar = new TitleBar { Title = Loc.T("Activity details"), ShowMinimize = false, ShowMaximize = false };
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        layout.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var dialog = new FluentWindow
        {
            Title = Loc.T("Activity details"), Owner = owner, Content = layout,
            Width = 560, Height = 560, MinWidth = 400, MinHeight = 320,
            ExtendsContentIntoTitleBar = true, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        // 调色板由 ThemeService 在应用级维护，此对话框无需自带主题字典。
        dialog.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        dialog.SetResourceReference(Control.BackgroundProperty, "ApplicationBackgroundBrush");
        close.Click += (_, _) => dialog.Close();
        return dialog;
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

        return entry.Kind == WorkspaceActivityKind.WorkspaceSaved
            ? Loc.T("Source: the current saved workspace. This is its latest save time, not a complete save history.")
            : Loc.T("Source: a retained recovery checkpoint. It records the desktop before an operation; it does not prove that the restore completed. Older checkpoints may expire.");
    }
}

internal sealed partial class ActivityRowItem
{
    public required WorkspaceActivityEntry Entry { get; init; }
}
