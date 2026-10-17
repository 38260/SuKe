using System;
using System.Collections.Generic;
using System.Linq;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// Projects the durable workspace artifacts into a newest-first list of activity events.
/// </summary>
/// <remarks>
/// <para>
/// Three durable sources feed this feed. Named workspace captures under <c>workspaces/</c> and
/// recovery checkpoints under <c>checkpoints/</c> describe what was <em>saved</em> or what an
/// operation was about to change: a checkpoint is written as the durability boundary before any
/// mutation, so on its own it only proves that an operation started. The recorded execution result
/// under <c>restore-results/</c> is the third source, and it is the only one that can state success
/// or failure — it is written from the real <c>RestoreExecutionResult</c>, never from the size of
/// the saved content.
/// </para>
/// <para>
/// A result is attached to its checkpoint by the stable checkpoint identifier, so two consecutive
/// restores can never be mixed up by workspace id and a nearby timestamp. Checkpoints with no
/// result (older records, expired results, or an interrupted process) keep a null outcome and are
/// shown as “result unknown”.
/// </para>
/// <para>
/// Checkpoint history is bounded by <see cref="CheckpointRetentionPolicy"/> (count and age) and the
/// result history by <see cref="RestoreOutcomeStore"/> (count and age). Nothing here writes to disk.
/// </para>
/// </remarks>
public sealed class WorkspaceActivityReader
{
    private readonly StorageService _storage;

    /// <summary>Creates a reader over one storage root.</summary>
    public WorkspaceActivityReader(StorageService storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    /// <summary>Returns every durable event, newest first.</summary>
    public IReadOnlyList<WorkspaceActivityEntry> Read()
    {
        List<WorkspaceSnapshot> workspaces = _storage.LoadAllWorkspaces();
        var entries = new List<WorkspaceActivityEntry>(workspaces.Count);
        var namesByWorkspaceId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (WorkspaceSnapshot workspace in workspaces)
        {
            if (!string.IsNullOrWhiteSpace(workspace.WorkspaceId))
                namesByWorkspaceId[workspace.WorkspaceId] = workspace.Name;

            entries.Add(new WorkspaceActivityEntry
            {
                Kind             = WorkspaceActivityKind.WorkspaceSaved,
                Category         = WorkspaceActivityCategory.Saved,
                OccurredAtLocal  = workspace.SavedAt.ToLocalTime(),
                WorkspaceName    = workspace.Name,
                WorkspaceId      = workspace.WorkspaceId,
                WindowCount      = workspace.Entries.Count,
                MonitorCount     = workspace.Monitors.Count,
            });
        }

        var checkpointIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorkspaceSnapshot checkpoint in _storage.Checkpoints.GetRetainedHistory())
        {
            WorkspaceCheckpointMetadata? metadata = checkpoint.Checkpoint;
            DateTime occurred = (metadata?.CreatedAtUtc ?? checkpoint.SavedAt).ToLocalTime();
            string targetId = metadata?.TargetWorkspaceId ?? "";
            string? targetName = targetId.Length > 0 && namesByWorkspaceId.TryGetValue(targetId, out string? name)
                ? name
                : null;
            WorkspaceCheckpointTrigger trigger = metadata?.Trigger ?? WorkspaceCheckpointTrigger.ManualCapture;
            string checkpointId = metadata?.CheckpointId ?? "";

            entries.Add(new WorkspaceActivityEntry
            {
                Kind            = WorkspaceActivityKind.Checkpoint,
                Category        = Classify(trigger),
                OccurredAtLocal = occurred,
                Trigger         = trigger,
                // The checkpoint document itself is a pre-operation capture of whatever was on the
                // desktop, so its own name is not meaningful to the user. Only the workspace the
                // operation targeted is named, and only while that workspace still exists.
                WorkspaceName   = targetName,
                WorkspaceId     = targetId.Length > 0 ? targetId : null,
                WindowCount     = checkpoint.Entries.Count,
                MonitorCount    = checkpoint.Monitors.Count,
                CheckpointId    = checkpointId.Length > 0 ? checkpointId : null,
            });
            if (checkpointId.Length > 0)
                checkpointIndexById.TryAdd(checkpointId, entries.Count - 1);
        }

        // Results are matched to their checkpoint by identifier. A result whose checkpoint is gone
        // (expired, or captured while checkpoints were disabled) still appears as its own row.
        foreach (RestoreOutcomeRecord outcome in _storage.RestoreOutcomes.Read())
        {
            if (outcome.CheckpointId is { Length: > 0 } checkpointId &&
                checkpointIndexById.TryGetValue(checkpointId, out int index))
            {
                entries[index] = entries[index] with { Outcome = outcome };
                continue;
            }

            entries.Add(new WorkspaceActivityEntry
            {
                Kind            = WorkspaceActivityKind.RestoreOutcome,
                Category        = Classify(outcome.Trigger),
                OccurredAtLocal = outcome.StartedAtUtc.ToLocalTime(),
                Trigger         = outcome.Trigger,
                WorkspaceName   = string.IsNullOrWhiteSpace(outcome.WorkspaceName) ? null : outcome.WorkspaceName,
                WorkspaceId     = string.IsNullOrWhiteSpace(outcome.WorkspaceId) ? null : outcome.WorkspaceId,
                WindowCount     = outcome.Windows.Planned,
                MonitorCount    = 0,
                CheckpointId    = outcome.CheckpointId,
                Outcome         = outcome,
            });
        }

        return entries
            .OrderByDescending(entry => entry.OccurredAtLocal)
            .ThenBy(entry => entry.WorkspaceName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>Maps a checkpoint trigger onto the activity filters.</summary>
    public static WorkspaceActivityCategory Classify(WorkspaceCheckpointTrigger trigger) => trigger switch
    {
        WorkspaceCheckpointTrigger.Restore
            or WorkspaceCheckpointTrigger.SelectiveRestore
            or WorkspaceCheckpointTrigger.AdaptiveRestore
            or WorkspaceCheckpointTrigger.AlignAndMinimize
            or WorkspaceCheckpointTrigger.WorkspaceSwitch
            or WorkspaceCheckpointTrigger.Undo
            => WorkspaceActivityCategory.Restore,
        _ => WorkspaceActivityCategory.System,
    };
}
