using System;
using System.Collections.Generic;
using System.Globalization;
using SuKe.Localization;
using SuKe.Models;

namespace SuKe.UI.Shell;

/// <summary>
/// 把持久化的恢复结果记录渲染成界面文案。
/// </summary>
/// <remarks>
/// 这里只做格式化：所有数字都直接来自 <see cref="RestoreOutcomeRecord"/> 中按内容类型统计的计数。
/// 没有可靠数据的类型不产生任何行（界面显示“未记录”或不展示），绝不补成 0 或 100% 成功。
/// </remarks>
internal static class RestoreOutcomeText
{
    private const string Separator = "  ·  ";

    /// <summary>整体状态的简短标签。</summary>
    internal static string StatusLabel(RestoreOutcomeStatus status) => Loc.T(status switch
    {
        RestoreOutcomeStatus.Succeeded => "All targets succeeded",
        RestoreOutcomeStatus.PartiallyCompleted => "Partially completed",
        RestoreOutcomeStatus.Failed => "Failed",
        RestoreOutcomeStatus.Cancelled => "Cancelled",
        RestoreOutcomeStatus.Skipped => "All targets skipped",
        RestoreOutcomeStatus.StalePlan => "Plan became stale",
        RestoreOutcomeStatus.Blocked => "Blocked before any change",
        RestoreOutcomeStatus.Interrupted => "Interrupted / result unknown",
        _ => "Result unknown",
    });

    /// <summary>操作类型的可读标签。</summary>
    internal static string OperationLabel(WorkspaceCheckpointTrigger trigger) => Loc.T(trigger switch
    {
        WorkspaceCheckpointTrigger.Restore => "Restore",
        WorkspaceCheckpointTrigger.SelectiveRestore => "Selected-monitor restore",
        WorkspaceCheckpointTrigger.WorkspaceSwitch => "Workspace switch",
        WorkspaceCheckpointTrigger.Undo => "Undo",
        WorkspaceCheckpointTrigger.AutomaticDisplayRestore => "Automatic display restore",
        WorkspaceCheckpointTrigger.AlignAndMinimize => "Align and minimize",
        WorkspaceCheckpointTrigger.AdaptiveRestore => "Adaptive restore",
        _ => "Checkpoint capture",
    });

    /// <summary>内容类型的可读标签。</summary>
    internal static string ContentTypeLabel(string code) => Loc.T(code switch
    {
        "window" => "Application windows",
        "browser_tab" => "Browser tabs",
        "explorer_tab" => "Explorer tabs",
        "terminal_tab" => "Terminal tabs",
        "operation" => "Whole operation",
        _ => "Other items",
    });

    /// <summary>原因码的可读标签。未知原因码原样返回，便于排查。</summary>
    internal static string ReasonLabel(string code) => code switch
    {
        "already_running" => Loc.T("Already running; this item was kept unchanged and skipped."),
        "placement_verification_failed" => Loc.T("The window moved, but its final placement could not be verified"),
        "entry_failed" => Loc.T("An execution step for this window failed"),
        "entry_blocked" => Loc.T("A precondition for this window was missing"),
        "entry_cancelled" => Loc.T("This window was cancelled before it was restored"),
        "entry_stale" => Loc.T("The saved window or its resource changed after review"),
        "launch_requested_not_confirmed" => Loc.T("The application was asked to start, but its window was not confirmed"),
        "window_not_observed" => Loc.T("The application started, but no matching window appeared"),
        "result_pending" => Loc.T("The executor did not reach a final state"),
        "no_execution_record" => Loc.T("The executor recorded no result for this target"),
        "explorer_tabs_failed" => Loc.T("The saved File Explorer tabs could not be reopened"),
        "explorer_tabs_stale" => Loc.T("A saved File Explorer folder no longer exists"),
        "terminal_tabs_failed" => Loc.T("The saved Windows Terminal tabs could not be reopened"),
        "terminal_tabs_stale" => Loc.T("A saved Windows Terminal directory no longer exists"),
        "browser_session_failed" => Loc.T("The browser did not confirm the tab-restore request"),
        "browser_session_stale" => Loc.T("The browser session was no longer available"),
        "browser_session_not_requested" => Loc.T("Browser tabs were saved, but no tab-restore request ran"),
        "operation_blocked" => Loc.T("The restore was blocked before it changed the desktop"),
        "plan_stale" => Loc.T("The desktop changed after review, so no stale action was applied"),
        _ => code,
    };

    /// <summary>活动列表里的一行结果摘要，例如“部分完成 · 应用窗口 6/8”。</summary>
    internal static string Summary(RestoreOutcomeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        string status = StatusLabel(record.Status);
        string? primary = PrimaryProgress(record);
        return primary is null ? status : status + Separator + primary;
    }

    /// <summary>列表里“结果尚不可用”的说明。</summary>
    internal static string UnknownResultSummary() =>
        Loc.T("No restore result was saved for this record, so success or failure cannot be determined.");

    /// <summary>计划规模，例如“计划 8 ×应用窗口、12 ×浏览器标签页”。</summary>
    internal static string PlannedDetail(RestoreOutcomeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var parts = new List<string>(4);
        AddPlanned(parts, "window", record.Windows);
        AddPlanned(parts, "browser_tab", record.BrowserTabs);
        AddPlanned(parts, "explorer_tab", record.ExplorerTabs);
        AddPlanned(parts, "terminal_tab", record.TerminalTabs);
        return parts.Count == 0
            ? Loc.T("Nothing was planned for this operation")
            : Loc.Format("Planned {0}", string.Join(", ", parts));
    }

    /// <summary>按内容类型统计的逐行明细；没有可靠数据的类型不产生行。</summary>
    internal static IReadOnlyList<string> CountLines(RestoreOutcomeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var lines = new List<string>(4);
        AddLine(lines, "Application windows", record.Windows);
        AddLine(lines, "Browser tabs", record.BrowserTabs);
        AddLine(lines, "Explorer tabs", record.ExplorerTabs);
        AddLine(lines, "Terminal tabs", record.TerminalTabs);
        return lines;
    }

    /// <summary>耗时文本，例如 <c>0.8 s</c> 或 <c>1 m 12 s</c>。</summary>
    internal static string Duration(double milliseconds)
    {
        if (milliseconds <= 0) return "0 s";
        TimeSpan total = TimeSpan.FromMilliseconds(milliseconds);
        if (total.TotalSeconds < 60)
            return total.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        return $"{(int)total.TotalMinutes} m {total.Seconds} s";
    }

    private static string? PrimaryProgress(RestoreOutcomeRecord record)
    {
        if (record.Windows.Recorded)
            return Progress("window", record.Windows);
        if (record.BrowserTabs.Recorded)
            return Progress("browser_tab", record.BrowserTabs);
        if (record.ExplorerTabs.Recorded)
            return Progress("explorer_tab", record.ExplorerTabs);
        if (record.TerminalTabs.Recorded)
            return Progress("terminal_tab", record.TerminalTabs);
        return null;
    }

    private static string Progress(string contentType, RestoreOutcomeCounts counts) =>
        $"{ContentTypeLabel(contentType)} {counts.Succeeded}/{counts.Planned}";

    private static void AddPlanned(List<string> parts, string contentType, RestoreOutcomeCounts counts)
    {
        if (!counts.Recorded) return;
        parts.Add($"{ContentTypeLabel(contentType)} ×{counts.Planned}");
    }

    private static void AddLine(List<string> lines, string label, RestoreOutcomeCounts counts)
    {
        if (!counts.Recorded) return;
        string text = Loc.Format("{0}: planned {1}, succeeded {2}", Loc.T(label), counts.Planned, counts.Succeeded);
        if (counts.Failed > 0) text += Loc.Format(", failed {0}", counts.Failed);
        if (counts.Skipped > 0) text += Loc.Format(", skipped {0}", counts.Skipped);
        if (counts.Cancelled > 0) text += Loc.Format(", cancelled {0}", counts.Cancelled);
        if (counts.Blocked > 0) text += Loc.Format(", blocked {0}", counts.Blocked);
        if (counts.Stale > 0) text += Loc.Format(", stale {0}", counts.Stale);
        if (counts.Unconfirmed > 0) text += Loc.Format(", unconfirmed {0}", counts.Unconfirmed);
        lines.Add(text);
    }
}
