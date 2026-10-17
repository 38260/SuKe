using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.UI.Shell;

/// <summary>
/// Activity page: a date-grouped timeline of the workspace events the application already persists.
/// </summary>
/// <remarks>
/// Every row is projected from a saved workspace document or a retained recovery checkpoint, so the
/// feed can only state that an operation <em>started</em>. Restore outcomes are not persisted and are
/// therefore never displayed as a status badge.
/// </remarks>
public partial class ActivityPage : UserControl
{
    private readonly WorkspaceActivityReader _reader;
    private IReadOnlyList<WorkspaceActivityEntry> _entries = Array.Empty<WorkspaceActivityEntry>();
    private ToggleButton? _activeChip;
    private bool _applyingFilter;
    private bool _pageReady;

    /// <summary>Creates the page over a read-only activity projection.</summary>
    public ActivityPage(WorkspaceActivityReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        InitializeComponent();
        _activeChip = FilterAll;

        // The initial chip is checked while the markup is still being parsed, so its handler runs
        // before the sibling chips and the list exist as fields. Only react once we are built.
        _pageReady = true;

        // The page may be built after the window's Loaded walk has already run.
        // A restore that finishes while this page is open refreshes it in place.
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            RestoreOutcomeStore.OutcomeRecorded -= OnOutcomeRecorded;
            RestoreOutcomeStore.OutcomeRecorded += OnOutcomeRecorded;
        };
        Unloaded += (_, _) => RestoreOutcomeStore.OutcomeRecorded -= OnOutcomeRecorded;
    }

    private void OnOutcomeRecorded() =>
        Dispatcher.BeginInvoke(() =>
        {
            if (IsLoaded) RefreshActivity();
        });

    /// <summary>Rereads durable events and rebuilds the timeline.</summary>
    public void RefreshActivity()
    {
        _entries = _reader.Read();
        Rebuild();
    }

    private void OnFilterChecked(object sender, RoutedEventArgs e)
    {
        if (!_pageReady || _applyingFilter || sender is not ToggleButton chip) return;
        if (ReferenceEquals(chip, _activeChip)) return;

        _activeChip = chip;
        _applyingFilter = true;
        try
        {
            foreach (ToggleButton candidate in new[] { FilterAll, FilterRestore, FilterSaved, FilterSystem })
                candidate.IsChecked = ReferenceEquals(candidate, chip);
        }
        finally
        {
            _applyingFilter = false;
        }

        Rebuild();
    }

    private bool PassesFilter(WorkspaceActivityEntry entry)
    {
        if (ReferenceEquals(_activeChip, FilterRestore)) return entry.Category == WorkspaceActivityCategory.Restore;
        if (ReferenceEquals(_activeChip, FilterSaved))   return entry.Category == WorkspaceActivityCategory.Saved;
        if (ReferenceEquals(_activeChip, FilterSystem))  return entry.Category == WorkspaceActivityCategory.System;
        return true;
    }

    private void Rebuild()
    {
        IReadOnlyList<WorkspaceActivityEntry> visible = _entries.Where(PassesFilter).ToArray();
        var groups = new List<ActivityGroupItem>();

        foreach (IGrouping<DateOnly, WorkspaceActivityEntry> grouping in
                 visible.GroupBy(entry => DateOnly.FromDateTime(entry.OccurredAtLocal))
                        .OrderByDescending(group => group.Key))
        {
            DateOnly date = grouping.Key;
            string header = DescribeDate(date);
            bool showTimeDate = date != DateOnly.FromDateTime(DateTime.Now);

            WorkspaceActivityEntry[] rows = grouping.ToArray();
            groups.Add(new ActivityGroupItem
            {
                Header = header,
                Rows = rows
                    .Select((entry, index) => BuildRow(entry, header, showTimeDate,
                        index == 0, index == rows.Length - 1))
                    .ToArray(),
            });
        }

        GroupList.ItemsSource = groups;
        EmptyCard.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // The empty copy differs between "nothing saved yet" and "this filter is empty".
        if (groups.Count == 0)
        {
            bool anyEvents = _entries.Count > 0;
            EmptyTitle.Text = anyEvents ? Loc.T("No activity in this filter") : Loc.T("No activity recorded yet");
            EmptyDetail.Text = anyEvents
                ? Loc.T("Switch back to All activity to see the events that are stored.")
                : Loc.T("Events appear here once you save a workspace or start a restore.");
        }
    }

    private static ActivityRowItem BuildRow(
        WorkspaceActivityEntry entry,
        string dateHeader,
        bool includeDateInTime,
        bool isFirst,
        bool isLast)
    {
        string time = entry.OccurredAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        string when = includeDateInTime ? $"{dateHeader}, {time}" : time;
        string summary = DescribeSummary(entry);

        return new ActivityRowItem
        {
            Entry = entry,
            Icon = entry.Kind == WorkspaceActivityKind.WorkspaceSaved
                ? SymbolRegular.Save24
                : entry.Category == WorkspaceActivityCategory.Restore
                    ? SymbolRegular.Play24
                    : SymbolRegular.Settings24,
            Category = entry.Category,
            Title = DescribeTitle(entry),
            Meta = DescribeMeta(entry, when),
            Summary = summary,
            SummaryVisibility = summary.Length == 0 ? Visibility.Collapsed : Visibility.Visible,
            CategoryLabel = entry.Category switch
            {
                WorkspaceActivityCategory.Restore => Loc.T("Restore"),
                WorkspaceActivityCategory.Saved => Loc.T("Saved"),
                _ => Loc.T("System"),
            },
            ConnectorAboveVisibility = isFirst ? Visibility.Collapsed : Visibility.Visible,
            ConnectorBelowVisibility = isLast ? Visibility.Collapsed : Visibility.Visible,
        };
    }

    /// <summary>
    /// 列表里的一行结果。已完成的操作显示真实统计；类别属于恢复但没有结果记录的旧条目明确说明
    /// 结果未知，绝不推断成功。
    /// </summary>
    private static string DescribeSummary(WorkspaceActivityEntry entry)
    {
        if (entry.Outcome is not null)
            return RestoreOutcomeText.Summary(entry.Outcome);
        if (entry.Category == WorkspaceActivityCategory.Restore)
            return RestoreOutcomeText.UnknownResultSummary();
        return "";
    }

    private static string DescribeTitle(WorkspaceActivityEntry entry)
    {
        string? name = entry.WorkspaceName;

        // 有真实结果时标题改为“结果”，因为“已启动”对一条已经结束的记录不再准确。
        if (entry.Outcome is not null)
        {
            string operation = RestoreOutcomeText.OperationLabel(entry.Outcome.Trigger);
            return name is null
                ? Loc.Format("{0} result", operation)
                : Loc.Format("{0} result: {1}", operation, name);
        }

        if (entry.Kind == WorkspaceActivityKind.WorkspaceSaved)
            return name is null
                ? Loc.T("Workspace saved")
                : Loc.Format("Workspace saved: {0}", name);

        return entry.Trigger switch
        {
            WorkspaceCheckpointTrigger.Restore => name is null
                ? Loc.T("Restore started")
                : Loc.Format("Restore started: {0}", name),
            WorkspaceCheckpointTrigger.SelectiveRestore => name is null
                ? Loc.T("Selected-monitor restore started")
                : Loc.Format("Selected-monitor restore started: {0}", name),
            WorkspaceCheckpointTrigger.WorkspaceSwitch => name is null
                ? Loc.T("Workspace switch started")
                : Loc.Format("Workspace switch started: {0}", name),
            WorkspaceCheckpointTrigger.Undo => Loc.T("Undo started"),
            WorkspaceCheckpointTrigger.AutomaticDisplayRestore => Loc.T("Automatic display restore started"),
            WorkspaceCheckpointTrigger.AlignAndMinimize => Loc.T("Align and minimize started"),
            WorkspaceCheckpointTrigger.AdaptiveRestore => Loc.T("Adaptive restore started"),
            _ => Loc.T("Checkpoint captured"),
        };
    }

    private static string DescribeMeta(WorkspaceActivityEntry entry, string when)
    {
        // 有结果记录时，说明本次计划实际要恢复多少内容（而不是检查点里顺带采集了多少）。
        if (entry.Outcome is not null)
            return $"{when}  ·  {RestoreOutcomeText.PlannedDetail(entry.Outcome)}";

        string detail = entry.Kind == WorkspaceActivityKind.WorkspaceSaved
            ? Loc.Format(
                "{0} window(s) across {1} monitor(s)",
                entry.WindowCount,
                entry.MonitorCount)
            : Loc.Format(
                "{0} window(s) captured as a recovery point",
                entry.WindowCount);

        return $"{when}  ·  {detail}";
    }

    private static string DescribeDate(DateOnly date)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        if (date == today) return Loc.T("Today");
        if (date == today.AddDays(-1)) return Loc.T("Yesterday");
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

/// <summary>One date heading and the events recorded under it.</summary>
internal sealed class ActivityGroupItem
{
    public string Header { get; init; } = "";
    public IReadOnlyList<ActivityRowItem> Rows { get; init; } = Array.Empty<ActivityRowItem>();
}

/// <summary>View model for a single timeline row.</summary>
internal sealed partial class ActivityRowItem
{
    public SymbolRegular Icon { get; init; }
    public WorkspaceActivityCategory Category { get; init; }
    public string Title { get; init; } = "";
    public string Meta { get; init; } = "";
    public string CategoryLabel { get; init; } = "";

    /// <summary>一行结果摘要，例如“部分完成 · 应用窗口 6/8”；空串表示不显示。</summary>
    public string Summary { get; init; } = "";

    public Visibility SummaryVisibility { get; init; } = Visibility.Collapsed;

    public Visibility ConnectorAboveVisibility { get; init; } = Visibility.Visible;
    public Visibility ConnectorBelowVisibility { get; init; } = Visibility.Visible;
}
