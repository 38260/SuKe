using System;
using System.Collections.Generic;
using System.Linq;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 把工作区快照投影成只含计数、应用名与显示器信息的脱敏摘要。
/// </summary>
/// <remarks>
/// 活动流（保存/检查点/删除记录）共用这一份实现，避免几处各写一套后逐渐漂移。
/// 摘要里永不出现路径、窗口标题、URL 或命令行。
/// </remarks>
internal static class WorkspaceActivitySnapshotBuilder
{
    internal static WorkspaceActivitySnapshot Describe(WorkspaceSnapshot snapshot) => new()
    {
        SavedWithFiles = snapshot.SavedWithFiles,
        BrowserTabCount = snapshot.BrowserSessions.Sum(session => session.Tabs.Count),
        ExplorerTabCount = snapshot.Entries.Sum(entry => entry.ExplorerTabPaths.Count),
        TerminalTabCount = snapshot.Entries.Sum(entry => entry.TerminalTabs.Count),
        VirtualDesktopCount = snapshot.VirtualDesktops.Count,
        UnassignedWindowCount = snapshot.Entries.Count(entry => string.IsNullOrEmpty(entry.MonitorId) ||
            !snapshot.Monitors.Any(monitor => string.Equals(monitor.MonitorId, entry.MonitorId, StringComparison.OrdinalIgnoreCase))),
        Applications = snapshot.Entries
            .GroupBy(entry => ApplicationName(entry.ProcessName), StringComparer.OrdinalIgnoreCase)
            .Select(group => new WorkspaceActivityApplication(group.Key, group.Count()))
            .OrderByDescending(app => app.WindowCount)
            .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        Monitors = snapshot.Monitors.OrderBy(monitor => monitor.Index)
            .Select(monitor => new WorkspaceActivityMonitor(
                monitor.Index, monitor.FriendlyName, monitor.WidthPixels, monitor.HeightPixels,
                monitor.Dpi, monitor.IsPrimary,
                snapshot.Entries.Count(entry => !string.IsNullOrEmpty(monitor.MonitorId) &&
                    string.Equals(entry.MonitorId, monitor.MonitorId, StringComparison.OrdinalIgnoreCase))))
            .ToArray(),
    };

    /// <summary>进程名只作为展示文本；含不可见字符或奇怪符号时统一回落到 “Application”。</summary>
    internal static string ApplicationName(string processName) =>
        !string.IsNullOrWhiteSpace(processName) && processName.All(character =>
            char.IsLetterOrDigit(character) || character is ' ' or '.' or '_' or '-')
            ? processName
            : "Application";
}
