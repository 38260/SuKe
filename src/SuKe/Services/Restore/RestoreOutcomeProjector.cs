using System;
using System.Collections.Generic;
using System.Linq;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 把真实的执行计划与执行结果投影成可持久化的恢复结果记录。
/// </summary>
/// <remarks>
/// <para>
/// 这是一个纯函数：只读取 <see cref="RestorePlan"/>、<see cref="RestoreExecutionResult"/> 与两个
/// 时间戳，不执行发现、重新校验、变更或额外等待，也不放宽任何成功判定。
/// </para>
/// <para>
/// 成功一律来自执行逻辑本身：<see cref="RestoreExecutionEntryStatus.Restored"/> 且定位验证未失败
/// 才算窗口成功；<c>LaunchRequested</c>、<c>AwaitingWindow</c>、<c>Pending</c> 只记为未确认。
/// 绝不根据“保存了多少内容”或“启动命令返回成功”推测成功数。
/// </para>
/// </remarks>
internal static class RestoreOutcomeProjector
{
    /// <summary>
    /// 开始一次操作时的记录：只写计划规模，不写任何成功数量，也没有结束时间。
    /// 这样即使进程异常退出，历史记录仍能显示计划恢复了多少内容以及“中断/结果未知”。
    /// </summary>
    internal static RestoreOutcomeRecord ProjectStarted(
        string operationId,
        RestorePlan plan,
        DateTime startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(plan);

        int[] plannedWindows = plan.Entries
            .Where(entry => !plan.DisabledEntryIndexes.Contains(entry.EntryIndex))
            .Select(entry => entry.EntryIndex)
            .ToArray();

        return new RestoreOutcomeRecord
        {
            OperationId = operationId,
            WorkspaceId = plan.WorkspaceId,
            WorkspaceName = plan.WorkspaceName,
            Trigger = TriggerFromMode(plan.Mode),
            StartedAtUtc = startedAtUtc.ToUniversalTime(),
            EndedAtUtc = null,
            DurationMs = 0,
            Status = RestoreOutcomeStatus.Interrupted,
            Windows = new RestoreOutcomeCounts { Planned = plannedWindows.Length },
            BrowserTabs = new RestoreOutcomeCounts
            {
                Planned = plan.BrowserSessions.Sum(session => session.Tabs.Count),
            },
            ExplorerTabs = new RestoreOutcomeCounts
            {
                Planned = plan.Entries
                    .Where(entry => !plan.DisabledEntryIndexes.Contains(entry.EntryIndex))
                    .Sum(entry => entry.ExplorerSession?.TabPaths.Count ?? 0),
            },
            TerminalTabs = new RestoreOutcomeCounts
            {
                Planned = plan.Actions
                    .Where(action => action.TerminalDirectories is { Count: > 0 })
                    .Sum(action => action.TerminalDirectories!.Count),
            },
            Issues = Array.Empty<RestoreOutcomeIssue>(),
        };
    }

    internal static RestoreOutcomeRecord Project(
        string operationId,
        RestorePlan plan,
        RestoreExecutionResult result,
        DateTime startedAtUtc,
        DateTime endedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        var issues = new List<RestoreOutcomeIssue>(RestoreOutcomeRecord.MaximumIssues);
        RestoreOutcomeCounts windows = ProjectWindows(plan, result, issues);
        RestoreOutcomeCounts explorer = ProjectExplorerTabs(plan, result, issues);
        RestoreOutcomeCounts terminal = ProjectTerminalTabs(plan, result, issues);
        RestoreOutcomeCounts browser = ProjectBrowserTabs(plan, result, issues);

        RestoreOutcomeStatus status = DetermineStatus(result, windows, browser, explorer, terminal);
        if (status == RestoreOutcomeStatus.Blocked)
        {
            issues.Add(new RestoreOutcomeIssue
            {
                ReasonCode = "operation_blocked",
                ContentType = "operation",
                Description = "The restore was blocked before it changed the desktop.",
            });
        }
        else if (status == RestoreOutcomeStatus.StalePlan)
        {
            issues.Add(new RestoreOutcomeIssue
            {
                ReasonCode = "plan_stale",
                ContentType = "operation",
                Description = "The desktop changed after review, so no stale action was applied.",
            });
        }

        return new RestoreOutcomeRecord
        {
            OperationId = operationId,
            CheckpointId = string.IsNullOrWhiteSpace(result.Checkpoint?.CheckpointId)
                ? null
                : result.Checkpoint!.CheckpointId,
            WorkspaceId = plan.WorkspaceId,
            WorkspaceName = plan.WorkspaceName,
            Trigger = result.Checkpoint?.Trigger ?? TriggerFromMode(plan.Mode),
            StartedAtUtc = startedAtUtc.ToUniversalTime(),
            EndedAtUtc = endedAtUtc.ToUniversalTime(),
            DurationMs = Math.Max(0, (endedAtUtc - startedAtUtc).TotalMilliseconds),
            Status = status,
            Windows = windows,
            BrowserTabs = browser,
            ExplorerTabs = explorer,
            TerminalTabs = terminal,
            Issues = issues.Take(RestoreOutcomeRecord.MaximumIssues).ToArray(),
        };
    }

    private static WorkspaceCheckpointTrigger TriggerFromMode(RestoreModeKind mode) => mode switch
    {
        RestoreModeKind.AlignAndMinimize => WorkspaceCheckpointTrigger.AlignAndMinimize,
        RestoreModeKind.Selective => WorkspaceCheckpointTrigger.SelectiveRestore,
        _ => WorkspaceCheckpointTrigger.Restore,
    };

    // ── 应用窗口 ──────────────────────────────────────────────────────────────

    private static RestoreOutcomeCounts ProjectWindows(
        RestorePlan plan,
        RestoreExecutionResult result,
        List<RestoreOutcomeIssue> issues)
    {
        var counts = new RestoreOutcomeCounts();
        Dictionary<int, RestoreExecutionEntryResult> executed = IndexResults(result);
        // 与既有判定口径一致：只有定位验证处于可接受状态才算成功。
        HashSet<int> placementFailures = result.PlacementFailures
            .Select(entry => entry.EntryIndex)
            .ToHashSet();

        foreach (RestorePlanEntry entry in plan.Entries)
        {
            // 未纳入本次执行计划（执行前被取消勾选）的窗口不计入目标。
            if (plan.DisabledEntryIndexes.Contains(entry.EntryIndex)) continue;
            counts.Planned++;

            if (!executed.TryGetValue(entry.EntryIndex, out RestoreExecutionEntryResult? outcome))
            {
                counts.Unconfirmed++;
                AddIssue(issues, "no_execution_record", "window", entry, null,
                    "The entry was part of the approved plan, but the executor recorded no result.");
                continue;
            }

            switch (outcome.Status)
            {
                case RestoreExecutionEntryStatus.Restored when placementFailures.Contains(entry.EntryIndex):
                    // 窗口已就位但定位验证未通过：不能显示为完整恢复成功。
                    counts.Failed++;
                    AddIssue(issues, "placement_verification_failed", "window", entry,
                        outcome.Explanation, "The window was moved, but its final placement could not be verified.");
                    break;
                case RestoreExecutionEntryStatus.Restored:
                    counts.Succeeded++;
                    break;
                case RestoreExecutionEntryStatus.Failed:
                    counts.Failed++;
                    AddIssue(issues, "entry_failed", "window", entry, outcome.Explanation,
                        "The restore action for this window failed.");
                    break;
                case RestoreExecutionEntryStatus.Excluded:
                    counts.Skipped++;
                    break;
                case RestoreExecutionEntryStatus.Blocked:
                    counts.Blocked++;
                    AddIssue(issues, "entry_blocked", "window", entry, outcome.Explanation,
                        "This window could not be restored because a precondition was missing.");
                    break;
                case RestoreExecutionEntryStatus.Cancelled:
                    counts.Cancelled++;
                    AddIssue(issues, "entry_cancelled", "window", entry, outcome.Explanation,
                        "This window was cancelled before it was restored.");
                    break;
                case RestoreExecutionEntryStatus.Stale:
                    counts.Stale++;
                    AddIssue(issues, "entry_stale", "window", entry, outcome.Explanation,
                        "The saved window or its resource changed after review.");
                    break;
                case RestoreExecutionEntryStatus.LaunchRequested:
                    counts.Unconfirmed++;
                    AddIssue(issues, "launch_requested_not_confirmed", "window", entry, outcome.Explanation,
                        "The application was asked to start; its window was not confirmed.");
                    break;
                case RestoreExecutionEntryStatus.AwaitingWindow:
                    counts.Unconfirmed++;
                    AddIssue(issues, "window_not_observed", "window", entry, outcome.Explanation,
                        "The application was started, but no matching window appeared in time.");
                    break;
                default:
                    counts.Unconfirmed++;
                    AddIssue(issues, "result_pending", "window", entry, outcome.Explanation,
                        "The executor did not reach a final state for this window.");
                    break;
            }
        }

        return counts;
    }

    // ── 资源管理器标签页 ──────────────────────────────────────────────────────

    private static RestoreOutcomeCounts ProjectExplorerTabs(
        RestorePlan plan,
        RestoreExecutionResult result,
        List<RestoreOutcomeIssue> issues)
    {
        var counts = new RestoreOutcomeCounts();
        foreach (RestorePlanEntry entry in plan.Entries)
        {
            if (plan.DisabledEntryIndexes.Contains(entry.EntryIndex)) continue;
            if (entry.ExplorerSession is not { } session || session.TabPaths.Count == 0) continue;

            counts.Planned += session.TabPaths.Count;
            RestoreExecutionActionResult? action = result.Actions.FirstOrDefault(candidate =>
                candidate.Kind == RestoreActionKind.RestoreExplorerTabs &&
                candidate.EntryIndex == entry.EntryIndex);
            if (action is null)
            {
                counts.Unconfirmed += session.TabPaths.Count;
                continue;
            }

            switch (action.Status)
            {
                case RestoreExecutionActionStatus.Succeeded:
                    counts.Succeeded += session.TabPaths.Count;
                    break;
                case RestoreExecutionActionStatus.Failed:
                    counts.Failed += session.TabPaths.Count;
                    AddIssue(issues, "explorer_tabs_failed", "explorer_tab", entry, action.Explanation,
                        "The saved File Explorer tabs could not be reopened.");
                    break;
                case RestoreExecutionActionStatus.Stale:
                    counts.Stale += session.TabPaths.Count;
                    AddIssue(issues, "explorer_tabs_stale", "explorer_tab", entry, action.Explanation,
                        "A saved File Explorer folder no longer exists.");
                    break;
                case RestoreExecutionActionStatus.Cancelled:
                    counts.Cancelled += session.TabPaths.Count;
                    break;
                default:
                    counts.Skipped += session.TabPaths.Count;
                    break;
            }
        }

        return counts;
    }

    // ── 终端标签页 ────────────────────────────────────────────────────────────

    private static RestoreOutcomeCounts ProjectTerminalTabs(
        RestorePlan plan,
        RestoreExecutionResult result,
        List<RestoreOutcomeIssue> issues)
    {
        var counts = new RestoreOutcomeCounts();
        if (plan.Actions.Count == 0) return counts;

        var executed = new Dictionary<int, RestoreExecutionActionResult>(result.Actions.Count);
        foreach (RestoreExecutionActionResult action in result.Actions)
            executed[action.ActionIndex] = action;
        Dictionary<int, RestorePlanEntry> entries = plan.Entries.ToDictionary(entry => entry.EntryIndex);

        for (int index = 0; index < plan.Actions.Count; index++)
        {
            // 终端标签页目录只出现在动作上（结果动作不携带目录），成功与否取对应动作的执行状态。
            if (plan.Actions[index].TerminalDirectories is not { Count: > 0 } directories) continue;
            if (plan.Actions[index].EntryIndex is int disabled && plan.DisabledEntryIndexes.Contains(disabled))
                continue;

            counts.Planned += directories.Count;
            entries.TryGetValue(plan.Actions[index].EntryIndex ?? -1, out RestorePlanEntry? entry);
            if (!executed.TryGetValue(index, out RestoreExecutionActionResult? outcome))
            {
                counts.Unconfirmed += directories.Count;
                continue;
            }

            switch (outcome.Status)
            {
                case RestoreExecutionActionStatus.Succeeded:
                    counts.Succeeded += directories.Count;
                    break;
                case RestoreExecutionActionStatus.Failed:
                    counts.Failed += directories.Count;
                    AddIssue(issues, "terminal_tabs_failed", "terminal_tab", entry, outcome.Explanation,
                        "The saved Windows Terminal tabs could not be reopened.");
                    break;
                case RestoreExecutionActionStatus.Stale:
                    counts.Stale += directories.Count;
                    AddIssue(issues, "terminal_tabs_stale", "terminal_tab", entry, outcome.Explanation,
                        "A saved Windows Terminal startup directory no longer exists.");
                    break;
                case RestoreExecutionActionStatus.Cancelled:
                    counts.Cancelled += directories.Count;
                    break;
                default:
                    counts.Skipped += directories.Count;
                    break;
            }
        }

        return counts;
    }

    // ── 浏览器标签页 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 浏览器标签页通过一次性的浏览器会话恢复请求处理，因此结果只能确认到“会话请求”粒度。
    /// 没有请求记录时不写 0，而是标记为未确认。
    /// </summary>
    private static RestoreOutcomeCounts ProjectBrowserTabs(
        RestorePlan plan,
        RestoreExecutionResult result,
        List<RestoreOutcomeIssue> issues)
    {
        var counts = new RestoreOutcomeCounts();
        int planned = plan.BrowserSessions.Sum(session => session.Tabs.Count);
        if (planned == 0) return counts;

        counts.Planned = planned;
        RestoreExecutionActionResult? action = result.Actions.FirstOrDefault(candidate =>
            candidate.Kind == RestoreActionKind.RestoreBrowserSession);
        if (action is null)
        {
            counts.Unconfirmed = planned;
            AddIssue(issues, "browser_session_not_requested", "browser_tab", null, null,
                "Browser tabs were saved, but no browser-session restore request was executed.");
            return counts;
        }

        switch (action.Status)
        {
            case RestoreExecutionActionStatus.Succeeded:
                counts.Succeeded = planned;
                break;
            case RestoreExecutionActionStatus.Failed:
                counts.Failed = planned;
                AddIssue(issues, "browser_session_failed", "browser_tab", null, action.Explanation,
                    "The browser did not confirm the tab-restore request.");
                break;
            case RestoreExecutionActionStatus.Stale:
                counts.Stale = planned;
                AddIssue(issues, "browser_session_stale", "browser_tab", null, action.Explanation,
                    "The browser session was no longer available when the restore ran.");
                break;
            case RestoreExecutionActionStatus.Cancelled:
                counts.Cancelled = planned;
                break;
            default:
                counts.Skipped = planned;
                break;
        }

        return counts;
    }

    // ── 整体状态 ──────────────────────────────────────────────────────────────

    private static RestoreOutcomeStatus DetermineStatus(
        RestoreExecutionResult result,
        RestoreOutcomeCounts windows,
        RestoreOutcomeCounts browser,
        RestoreOutcomeCounts explorer,
        RestoreOutcomeCounts terminal)
    {
        if (result.WasCancelled || result.Status == RestoreExecutionStatus.Cancelled)
            return RestoreOutcomeStatus.Cancelled;
        if (result.Status == RestoreExecutionStatus.Rejected)
            return RestoreOutcomeStatus.Blocked;
        if (result.Status == RestoreExecutionStatus.StalePlan)
            return RestoreOutcomeStatus.StalePlan;

        int trouble = Trouble(windows) + Trouble(browser) + Trouble(explorer) + Trouble(terminal);
        if (trouble == 0) return RestoreOutcomeStatus.Succeeded;

        int succeeded = windows.Succeeded + browser.Succeeded + explorer.Succeeded + terminal.Succeeded;
        return succeeded == 0 && windows.Failed > 0
            ? RestoreOutcomeStatus.Failed
            : RestoreOutcomeStatus.PartiallyCompleted;
    }

    private static int Trouble(RestoreOutcomeCounts counts) =>
        counts.Failed + counts.Skipped + counts.Cancelled + counts.Blocked + counts.Stale + counts.Unconfirmed;

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private static Dictionary<int, RestoreExecutionEntryResult> IndexResults(RestoreExecutionResult result)
    {
        var indexed = new Dictionary<int, RestoreExecutionEntryResult>(result.Entries.Count);
        foreach (RestoreExecutionEntryResult entry in result.Entries)
            indexed[entry.EntryIndex] = entry;
        return indexed;
    }

    private static void AddIssue(
        List<RestoreOutcomeIssue> issues,
        string reasonCode,
        string contentType,
        RestorePlanEntry? entry,
        string? explanation,
        string fallback)
    {
        if (issues.Count >= RestoreOutcomeRecord.MaximumIssues) return;
        issues.Add(new RestoreOutcomeIssue
        {
            ReasonCode = reasonCode,
            ContentType = contentType,
            Subject = entry is null ? "" : LogRedactor.ScrubSecrets(entry.SavedIdentity.ProcessName),
            Description = Sanitize(string.IsNullOrWhiteSpace(explanation) ? fallback : explanation),
        });
    }

    /// <summary>脱敏并压平为单行，再截断到上限长度。</summary>
    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string redacted = LogRedactor.RedactUnstructured(text)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return redacted.Length <= RestoreOutcomeRecord.MaximumDescriptionLength
            ? redacted
            : redacted[..RestoreOutcomeRecord.MaximumDescriptionLength];
    }
}
