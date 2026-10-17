using System;
using System.Collections.Generic;

namespace SuKe.Models;

/// <summary>Which durable artifact produced an activity entry.</summary>
public enum WorkspaceActivityKind
{
    /// <summary>A named workspace was captured and saved.</summary>
    WorkspaceSaved,

    /// <summary>A recovery checkpoint was committed, which means an operation started.</summary>
    Checkpoint,

    /// <summary>A restore operation recorded its real execution result.</summary>
    RestoreOutcome,
}

/// <summary>Filter grouping used by the activity page.</summary>
public enum WorkspaceActivityCategory
{
    /// <summary>A restore, switch, alignment, or undo operation started.</summary>
    Restore,

    /// <summary>A workspace was saved.</summary>
    Saved,

    /// <summary>A display-driven or manual capture started.</summary>
    System,
}

/// <summary>
/// One workspace event projected from data the application already persists.
/// </summary>
/// <remarks>
/// 活动来自工作区、恢复前检查点和真实恢复结果；快照内容不能作为恢复成功的依据。
/// </remarks>
public sealed record WorkspaceActivityEntry
{
    /// <summary>Which persisted artifact produced this entry.</summary>
    public required WorkspaceActivityKind Kind { get; init; }

    /// <summary>Grouping used by the activity filters.</summary>
    public required WorkspaceActivityCategory Category { get; init; }

    /// <summary>When the artifact was written, already converted to local time for display.</summary>
    public required DateTime OccurredAtLocal { get; init; }

    /// <summary>The operation that caused a checkpoint; null for a saved workspace.</summary>
    public WorkspaceCheckpointTrigger? Trigger { get; init; }

    /// <summary>
    /// Local workspace name. This is user data shown only in the local interface; no path,
    /// window title, URL, or command line is ever carried here.
    /// </summary>
    public string? WorkspaceName { get; init; }

    /// <summary>Stable workspace identifier, when the named workspace still exists.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Number of captured windows in the artifact.</summary>
    public int WindowCount { get; init; }

    /// <summary>Number of monitors described by the artifact.</summary>
    public int MonitorCount { get; init; }

    /// <summary>
    /// The pre-operation checkpoint this activity belongs to. It links the durable “operation
    /// started” record with the recorded execution result by identity, never by workspace id and a
    /// nearby timestamp.
    /// </summary>
    public string? CheckpointId { get; init; }

    /// <summary>
    /// The recorded execution result of this operation. Null for older entries whose result was
    /// never saved, in which case the interface must say the result is unknown.
    /// </summary>
    public RestoreOutcomeRecord? Outcome { get; init; }

    /// <summary>保存内容或恢复前桌面的脱敏摘要；结果记录缺少原快照时为 null。</summary>
    public WorkspaceActivitySnapshot? Snapshot { get; init; }
}

/// <summary>只携带计数、应用名和显示器信息，不携带路径、窗口标题、URL 或命令行。</summary>
public sealed record WorkspaceActivitySnapshot
{
    public bool SavedWithFiles { get; init; }
    public int BrowserTabCount { get; init; }
    public int ExplorerTabCount { get; init; }
    public int TerminalTabCount { get; init; }
    public int VirtualDesktopCount { get; init; }
    public int UnassignedWindowCount { get; init; }
    public IReadOnlyList<WorkspaceActivityApplication> Applications { get; init; } = Array.Empty<WorkspaceActivityApplication>();
    public IReadOnlyList<WorkspaceActivityMonitor> Monitors { get; init; } = Array.Empty<WorkspaceActivityMonitor>();
}

public sealed record WorkspaceActivityApplication(string Name, int WindowCount);

public sealed record WorkspaceActivityMonitor(
    int Index, string Name, int Width, int Height, uint Dpi, bool IsPrimary, int WindowCount);
