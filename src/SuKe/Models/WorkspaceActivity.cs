using System;

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
/// The activity page is deliberately limited to these two durable sources. A restore's
/// <em>outcome</em> is never persisted, so an entry can only state that an operation started.
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
}
