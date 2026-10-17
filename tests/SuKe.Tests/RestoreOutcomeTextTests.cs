using SuKe.Models;
using SuKe.UI.Shell;

namespace SuKe.Tests;

/// <summary>
/// 结果记录的展示格式。断言只看结构与数字，不绑定具体译文，因此中英文都能通过。
/// </summary>
public class RestoreOutcomeTextTests
{
    private static RestoreOutcomeRecord Record(
        RestoreOutcomeStatus status = RestoreOutcomeStatus.PartiallyCompleted) => new()
    {
        OperationId = "0123456789abcdef0123456789abcdef",
        WorkspaceId = "workspace-1",
        WorkspaceName = "Desk",
        Trigger = WorkspaceCheckpointTrigger.Restore,
        StartedAtUtc = new DateTime(2026, 10, 17, 0, 30, 0, DateTimeKind.Utc),
        EndedAtUtc = new DateTime(2026, 10, 17, 0, 30, 7, 400, DateTimeKind.Utc),
        DurationMs = 7400,
        Status = status,
        Windows = new RestoreOutcomeCounts
        {
            Planned = 8, Succeeded = 6, Failed = 1, Skipped = 1,
        },
        BrowserTabs = new RestoreOutcomeCounts
        {
            Planned = 12, Succeeded = 10, Unconfirmed = 2,
        },
        ExplorerTabs = new RestoreOutcomeCounts { Planned = 3, Succeeded = 3 },
        TerminalTabs = new RestoreOutcomeCounts(),
    };

    [Fact]
    public void The_summary_line_pairs_the_status_with_the_primary_progress()
    {
        string summary = RestoreOutcomeText.Summary(Record());

        Assert.Contains("6/8", summary);
        Assert.Contains(RestoreOutcomeText.StatusLabel(RestoreOutcomeStatus.PartiallyCompleted), summary);
    }

    [Fact]
    public void Summary_labels_are_distinct_for_every_status()
    {
        var labels = Enum.GetValues<RestoreOutcomeStatus>()
            .Select(RestoreOutcomeText.StatusLabel)
            .ToArray();

        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(labels, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void Count_lines_use_the_real_numbers_and_close_once_per_content_type()
    {
        IReadOnlyList<string> lines = RestoreOutcomeText.CountLines(Record());

        // 窗口、浏览器标签页、资源管理器标签页各一行；终端标签页计划为 0，不生成行。
        Assert.Equal(3, lines.Count);
        Assert.Contains(lines, line => line.Contains('8') && line.Contains('6') && line.Contains('1'));
        Assert.Contains(lines, line => line.Contains("12") && line.Contains("10") && line.Contains('2'));
    }

    [Fact]
    public void A_content_type_with_no_planned_targets_is_not_reported_as_zero()
    {
        RestoreOutcomeRecord record = Record() with
        {
            BrowserTabs = new RestoreOutcomeCounts(),
            ExplorerTabs = new RestoreOutcomeCounts(),
            TerminalTabs = new RestoreOutcomeCounts(),
        };

        IReadOnlyList<string> lines = RestoreOutcomeText.CountLines(record);
        string planned = RestoreOutcomeText.PlannedDetail(record);

        Assert.Single(lines);
        // “未记录”的类型既不进明细行，也不进计划规模。
        Assert.DoesNotContain(RestoreOutcomeText.ContentTypeLabel("browser_tab"), planned);
        Assert.DoesNotContain(RestoreOutcomeText.ContentTypeLabel("terminal_tab"), planned);
    }

    [Fact]
    public void Planned_detail_lists_every_recorded_content_type()
    {
        string planned = RestoreOutcomeText.PlannedDetail(Record());

        Assert.Contains(RestoreOutcomeText.ContentTypeLabel("window"), planned);
        Assert.Contains(RestoreOutcomeText.ContentTypeLabel("browser_tab"), planned);
        Assert.Contains(RestoreOutcomeText.ContentTypeLabel("explorer_tab"), planned);
        Assert.Contains("8", planned);
        Assert.Contains("12", planned);
    }

    [Fact]
    public void Duration_is_readable_across_the_second_and_minute_range()
    {
        Assert.Equal("0 s", RestoreOutcomeText.Duration(0));
        Assert.Equal("7.4 s", RestoreOutcomeText.Duration(7400));
        Assert.Equal("1 m 12 s", RestoreOutcomeText.Duration(72000));
    }

    [Fact]
    public void Every_documented_reason_code_has_a_readable_label()
    {
        string[] codes =
        [
            "placement_verification_failed", "entry_failed", "entry_blocked", "entry_cancelled",
            "entry_stale", "launch_requested_not_confirmed", "window_not_observed", "result_pending",
            "no_execution_record", "explorer_tabs_failed", "explorer_tabs_stale",
            "terminal_tabs_failed", "terminal_tabs_stale", "browser_session_failed",
            "browser_session_stale", "browser_session_not_requested", "operation_blocked",
            "plan_stale",
        ];

        foreach (string code in codes)
        {
            string label = RestoreOutcomeText.ReasonLabel(code);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual(code, label);
        }

        // 未知原因码原样返回，便于排查而不是被吞掉。
        Assert.Equal("something_new", RestoreOutcomeText.ReasonLabel("something_new"));
    }
}
