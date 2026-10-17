using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>User-facing classification of one immutable restore-plan outcome.</summary>
public enum RestorePreviewOutcomeKind
{
    Exact,
    Adapted,
    Ambiguous,
    Missing,
    Ready,
    Skipped,
    Cancelled
}

/// <summary>User-facing category for a plan action. Close is reserved for planned switch actions.</summary>
public enum RestorePreviewActionKind
{
    Move,
    Launch,
    Browser,
    Wait,
    Minimize,
    Close,
    Skip,
    NoChange
}

/// <summary>Display projection of one action from the immutable plan.</summary>
public sealed record RestorePlanPreviewAction(
    RestorePreviewActionKind Kind,
    string Label,
    string Explanation,
    bool IsDestructive,
    bool IsBlocking,
    int? EntryIndex);

/// <summary>Distinguishing, explained metadata for one selectable live-window candidate.</summary>
public sealed record RestorePlanPreviewCandidate(
    long WindowHandle,
    string DisplayTitle,
    string IdentityLabel,
    string ConfidenceLabel,
    string ScoreLabel,
    IReadOnlyList<string> Reasons,
    bool IsLearnedHintMatch,
    bool CanRememberChoice,
    bool IsSelected);

/// <summary>Display projection of one saved entry and all of its approved actions.</summary>
public sealed record RestorePlanPreviewEntry(
    int EntryIndex,
    string EntryId,
    string DisplayName,
    RestorePreviewOutcomeKind Outcome,
    string OutcomeLabel,
    string TargetLabel,
    string Explanation,
    bool IsInitiallyEnabled,
    bool IsBlocking,
    bool IsDestructive,
    string AccessibilityLabel,
    IReadOnlyList<RestorePlanPreviewAction> Actions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> BlockingErrors,
    IReadOnlyList<RestorePlanPreviewCandidate> Candidates)
{
    public string PolicyLabel { get; init; } = "Workspace mode default";
    public bool KeepRunning { get; init; }

    /// <summary>
    /// Executable that owns the saved window, used to draw the application badge. Empty for
    /// packaged or web apps that have no file path, in which case the badge falls back to a letter.
    /// </summary>
    public string ExecutablePath { get; init; } = "";

    /// <summary>Short application name for the badge tooltip and its letter fallback.</summary>
    public string AppLabel { get; init; } = "";

    /// <summary>
    /// Stable display-grouping key derived from the saved application identity (web-app identity,
    /// executable path, packaged app id, then process name). Grouping is presentation only: every
    /// entry keeps its own <see cref="EntryIndex"/>, <see cref="EntryId"/>, candidates and placement.
    /// </summary>
    public string AppKey { get; init; } = "";

    /// <summary>Window title without the saved application suffix, used as the row's second line.</summary>
    public string WindowTitleLabel { get; init; } = "";
}

/// <summary>
/// Presentation-only grouping of the saved windows that belong to one application. The group has
/// no authority over restore behaviour: it only decides how the preview lists and counts windows.
/// </summary>
public sealed record RestorePlanPreviewAppGroup(
    string AppKey,
    string AppLabel,
    string ExecutablePath,
    IReadOnlyList<RestorePlanPreviewEntry> Entries)
{
    /// <summary>Number of saved windows, never the number of browser tabs inside them.</summary>
    public int WindowCount => Entries.Count;
}

/// <summary>
/// Read-only UI projection of the exact plan that will be approved. It never observes windows or
/// resources and cannot recompute a match.
/// </summary>
public sealed record RestorePlanPreview
{
    public string WorkspaceName { get; init; } = "";
    public RestoreModeKind Mode { get; init; }
    public IReadOnlyList<RestorePlanPreviewEntry> Entries { get; init; } =
        Array.Empty<RestorePlanPreviewEntry>();
    public IReadOnlyList<RestorePlanPreviewAction> GlobalActions { get; init; } =
        Array.Empty<RestorePlanPreviewAction>();
    public int EnabledEntryCount { get; init; }
    public int BlockingErrorCount { get; init; }
    public int DestructiveActionCount { get; init; }
    public string DestructiveSummary { get; init; } = "";

    /// <summary>Windows grouped by saved application identity, in saved order.</summary>
    public IReadOnlyList<RestorePlanPreviewAppGroup> Groups { get; init; } =
        Array.Empty<RestorePlanPreviewAppGroup>();

    /// <summary>Number of distinct applications in the plan.</summary>
    public int AppCount => Groups.Count;

    /// <summary>Number of saved windows in the plan.</summary>
    public int WindowCount => Entries.Count;
}

/// <summary>Creates presentation-only projections from immutable restore plans.</summary>
public static class RestorePlanPreviewBuilder
{
    public static RestorePlanPreview Build(RestorePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RestorePlanPreviewEntry[] entries = plan.Entries.Select(BuildEntry).ToArray();
        RestorePlanPreviewAction[] globals = plan.Actions
            .Where(action => action.EntryIndex is null)
            .Select(ToPreviewAction)
            .ToArray();
        int destructiveCount = entries.Sum(entry => entry.Actions.Count(action => action.IsDestructive)) +
            globals.Count(action => action.IsDestructive);
        bool minimizes = globals.Any(action => action.Kind == RestorePreviewActionKind.Minimize);
        RestorePlanPreviewAppGroup[] groups = entries
            .GroupBy(entry => entry.AppKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RestorePlanPreviewAppGroup(
                group.Key,
                group.First().AppLabel,
                group.First().ExecutablePath,
                group.ToArray()))
            .ToArray();

        return new RestorePlanPreview
        {
            WorkspaceName = plan.WorkspaceName,
            Mode = plan.Mode,
            Entries = entries,
            Groups = groups,
            GlobalActions = globals,
            EnabledEntryCount = entries.Count(entry => entry.IsInitiallyEnabled),
            BlockingErrorCount = entries.Sum(entry => entry.BlockingErrors.Count),
            DestructiveActionCount = destructiveCount,
            DestructiveSummary = minimizes
                ? "Other open windows will be minimized. No windows will be closed."
                : "No windows will be closed or minimized."
        };
    }

    private static RestorePlanPreviewEntry BuildEntry(RestorePlanEntry entry)
    {
        RestorePreviewOutcomeKind outcome = Classify(entry);
        RestorePlanPreviewAction[] actions = entry.Actions.Select(ToPreviewAction).ToArray();
        if (actions.Length == 0)
        {
            actions =
            [
                new RestorePlanPreviewAction(
                    entry.Outcome is RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Blocked
                        ? RestorePreviewActionKind.Skip
                        : RestorePreviewActionKind.NoChange,
                    entry.Outcome is RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Blocked
                        ? "No safe action"
                        : "No additional action",
                    entry.Explanation,
                    IsDestructive: false,
                    IsBlocking: entry.BlockingErrors.Count > 0,
                    entry.EntryIndex)
            ];
        }

        string displayName = FirstNonEmpty(
            entry.SavedIdentity.Title,
            Path.GetFileName(entry.SavedIdentity.DocumentPath),
            Path.GetFileName(entry.SavedIdentity.LaunchTargetPath),
            entry.SavedIdentity.ProcessName,
            $"Workspace entry {entry.EntryIndex + 1}");
        string target = $"Monitor {entry.TargetPlacement.TargetMonitorIndex + 1}";
        if (!string.IsNullOrWhiteSpace(entry.TargetPlacement.TargetMonitorId))
            target += $" · {entry.TargetPlacement.TargetMonitorId}";
        if (entry.TargetPlacement.MonitorMapping != RestoreMonitorMappingKind.ExactStableId)
            target += $" · {MappingLabel(entry.TargetPlacement.MonitorMapping)}";
        if (entry.TargetPlacement.Strategy != RestorePlacementStrategy.ExactPixels)
            target += $" · {PlacementLabel(entry.TargetPlacement)}";
        if (entry.TargetPlacement.WasDpiScaled)
            target += $" · DPI {entry.TargetPlacement.SavedDpi}→{entry.TargetPlacement.TargetDpi}";

        bool enabled = entry.KeepRunning || entry.Outcome is not (RestorePlanEntryOutcome.Excluded or
            RestorePlanEntryOutcome.Cancelled);
        string outcomeLabel = entry.KeepRunning ? "Already running · skipped" : OutcomeLabel(outcome);
        RestorePlanPreviewCandidate[] candidates = entry.Candidates
            .Where(candidate => candidate.IsEligible &&
                (outcome == RestorePreviewOutcomeKind.Ambiguous
                    ? candidate.IsWithinAmbiguityMargin
                    : entry.SelectedMatch?.WindowHandle == candidate.WindowHandle))
            .Select(candidate => new RestorePlanPreviewCandidate(
                candidate.WindowHandle,
                FirstNonEmpty(candidate.Title, candidate.ProcessName, "Untitled window"),
                CandidateIdentityLabel(candidate),
                ConfidenceLabel(candidate.Confidence),
                $"Score {candidate.Score:0}",
                candidate.Evidence
                    .Where(evidence => evidence.Matched)
                    .OrderByDescending(evidence => evidence.ScoreContribution)
                    .Select(evidence => evidence.Explanation)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                candidate.IsLearnedHintMatch,
                candidate.CanRememberChoice,
                entry.SelectedMatch?.WindowHandle == candidate.WindowHandle))
            .ToArray();
        return new RestorePlanPreviewEntry(
            entry.EntryIndex,
            entry.EntryId,
            displayName,
            outcome,
            outcomeLabel,
            target,
            entry.Explanation,
            enabled,
            entry.BlockingErrors.Count > 0,
            actions.Any(action => action.IsDestructive),
            $"{displayName}. {outcomeLabel}. {target}. {entry.Explanation}",
            actions,
            entry.Warnings.Select(issue => issue.Explanation).ToArray(),
            entry.BlockingErrors.Select(issue => issue.Explanation).ToArray(),
            candidates)
        {
            PolicyLabel = RestorePolicyResolver.Label(entry.RestorePolicy),
            KeepRunning = entry.KeepRunning,
            ExecutablePath = entry.SavedIdentity.ExecutablePath,
            AppLabel = ApplicationLabel(entry.SavedIdentity),
            AppKey = AppIdentityKey(entry.SavedIdentity, entry.EntryId),
            WindowTitleLabel = WindowTitleLabel(entry.SavedIdentity)
        };
    }

    /// <summary>
    /// Grouping must survive two windows of one app sharing a title and must keep two different
    /// apps that share an executable (installed web apps) apart, so identity is read from the
    /// strongest available evidence instead of the display name.
    /// </summary>
    private static string AppIdentityKey(SavedWindowIdentity identity, string entryId)
    {
        if (identity.IsWebApp && !string.IsNullOrWhiteSpace(identity.PwaIdentity))
            return "pwa:" + identity.PwaIdentity.Trim();

        string path = WindowIdentityExtractor.NormalizePath(identity.ExecutablePath);
        if (path.Length > 0) return "exe:" + path;

        if (!string.IsNullOrWhiteSpace(identity.AppUserModelId))
            return "aumid:" + identity.AppUserModelId.Trim();

        if (!string.IsNullOrWhiteSpace(identity.ProcessName))
            return "proc:" + ProcessIdentityNormalizer.Normalize(identity.ProcessName);

        return "entry:" + entryId;
    }

    /// <summary>
    /// Window title with a trailing " - &lt;app&gt;" suffix removed when that suffix repeats the
    /// application identity; anything else is left untouched because titles are user data.
    /// </summary>
    private static string WindowTitleLabel(SavedWindowIdentity identity)
    {
        string title = identity.Title.Trim();
        int cut = title.LastIndexOf(" - ", StringComparison.Ordinal);
        if (cut <= 0) return title;

        string tail = title[(cut + 3)..].Trim();
        string processName = identity.ProcessName.Trim();
        string fileName = Path.GetFileNameWithoutExtension(identity.ExecutablePath);
        bool repeatsApp =
            (processName.Length > 0 && tail.Equals(processName, StringComparison.OrdinalIgnoreCase)) ||
            (fileName.Length > 0 && tail.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return repeatsApp ? title[..cut].Trim() : title;
    }

    /// <summary>
    /// Short application name shown on the badge. Deliberately never the window title: the badge
    /// identifies the app, so two windows of the same app always share one letter fallback.
    /// </summary>
    private static string ApplicationLabel(SavedWindowIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.ProcessName)) return identity.ProcessName;
        string fileName = Path.GetFileNameWithoutExtension(identity.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
        if (!string.IsNullOrWhiteSpace(identity.AppUserModelId))
            return identity.AppUserModelId.Split('!', 2)[0].Split('_')[0];
        return "Application";
    }

    private static RestorePreviewOutcomeKind Classify(RestorePlanEntry entry)
    {
        if (entry.Outcome == RestorePlanEntryOutcome.Cancelled)
            return RestorePreviewOutcomeKind.Cancelled;
        if (entry.Outcome == RestorePlanEntryOutcome.Excluded)
            return RestorePreviewOutcomeKind.Skipped;
        if (entry.BlockingErrors.Any(issue => issue.Code is
                RestorePlanIssueCode.MissingResource or
                RestorePlanIssueCode.MissingExecutable or
                RestorePlanIssueCode.MissingWebAppLaunchTarget or
                RestorePlanIssueCode.MissingBrowserUrl))
            return RestorePreviewOutcomeKind.Missing;
        if (entry.Warnings.Any(issue => issue.Code == RestorePlanIssueCode.AmbiguousMatch))
            return RestorePreviewOutcomeKind.Ambiguous;
        if (entry.TargetPlacement.MonitorMapping != RestoreMonitorMappingKind.ExactStableId ||
            entry.TargetPlacement.WasDpiScaled ||
            entry.TargetPlacement.Strategy != RestorePlacementStrategy.ExactPixels)
            return RestorePreviewOutcomeKind.Adapted;
        if (entry.SelectedMatch?.Confidence == WindowMatchConfidence.Exact)
            return RestorePreviewOutcomeKind.Exact;
        return RestorePreviewOutcomeKind.Ready;
    }

    private static RestorePlanPreviewAction ToPreviewAction(RestoreAction action)
    {
        (RestorePreviewActionKind kind, string label, bool destructive) = action.Kind switch
        {
            RestoreActionKind.RestoreExistingWindow =>
                (RestorePreviewActionKind.Move, "Move and resize existing window", false),
            RestoreActionKind.LaunchApplication =>
                (RestorePreviewActionKind.Launch, "Launch application", false),
            RestoreActionKind.OpenResource =>
                (RestorePreviewActionKind.Launch, "Open saved resource", false),
            RestoreActionKind.LaunchDedicatedBrowser =>
                (RestorePreviewActionKind.Launch, "Open dedicated browser window", false),
            RestoreActionKind.LaunchWebApp =>
                (RestorePreviewActionKind.Launch, "Launch installed web app", false),
            RestoreActionKind.ActivatePackagedApplication =>
                (RestorePreviewActionKind.Launch, "Activate packaged application", false),
            RestoreActionKind.RestoreBrowserSession =>
                (RestorePreviewActionKind.Browser, "Restore browser session", false),
            RestoreActionKind.RestoreExplorerTabs =>
                (RestorePreviewActionKind.Launch, "Restore File Explorer tabs", false),
            RestoreActionKind.AwaitWindowAppearance =>
                (RestorePreviewActionKind.Wait, "Wait for window and position it", false),
            RestoreActionKind.MinimizeOtherWindows =>
                (RestorePreviewActionKind.Minimize, "Minimize other open windows", true),
            _ => (RestorePreviewActionKind.NoChange, action.Kind.ToString(), false)
        };
        if (action.Condition == RestoreActionCondition.BrowserSessionUnavailable)
            label += " if browser-session restore is unavailable";
        return new RestorePlanPreviewAction(
            kind,
            label,
            action.Explanation,
            destructive,
            IsBlocking: false,
            action.EntryIndex);
    }

    private static string OutcomeLabel(RestorePreviewOutcomeKind outcome) => outcome switch
    {
        RestorePreviewOutcomeKind.Exact => "Exact match",
        RestorePreviewOutcomeKind.Adapted => "Adapted placement",
        RestorePreviewOutcomeKind.Ambiguous => "Ambiguous match",
        RestorePreviewOutcomeKind.Missing => "Missing resource",
        RestorePreviewOutcomeKind.Ready => "Ready",
        RestorePreviewOutcomeKind.Skipped => "Skipped",
        RestorePreviewOutcomeKind.Cancelled => "Cancelled",
        _ => outcome.ToString()
    };

    private static string ConfidenceLabel(WindowMatchConfidence confidence) => confidence switch
    {
        WindowMatchConfidence.Exact => "Exact evidence",
        WindowMatchConfidence.Strong => "Strong evidence",
        WindowMatchConfidence.Probable => "Probable evidence",
        WindowMatchConfidence.Ambiguous => "Ambiguous",
        WindowMatchConfidence.Missing => "Missing",
        _ => "Ineligible"
    };

    private static string CandidateIdentityLabel(RestorePlanCandidate candidate)
    {
        string process = FirstNonEmpty(candidate.ProcessName, "Unknown application");
        string className = string.IsNullOrWhiteSpace(candidate.WindowClassName)
            ? "unknown class"
            : candidate.WindowClassName;
        string monitor = string.IsNullOrWhiteSpace(candidate.MonitorId)
            ? "unknown monitor"
            : candidate.MonitorId;
        string bounds = candidate.Bounds.IsValid
            ? $"{candidate.Bounds.Width}×{candidate.Bounds.Height} at {candidate.Bounds.Left},{candidate.Bounds.Top}"
            : "bounds unavailable";
        return $"{process} · {className} · {monitor} · {bounds}";
    }

    private static string MappingLabel(RestoreMonitorMappingKind mapping) => mapping switch
    {
        RestoreMonitorMappingKind.SavedIndexFallback => "mapped by saved monitor number",
        RestoreMonitorMappingKind.PrimaryFallback => "mapped to primary monitor",
        RestoreMonitorMappingKind.Unavailable => "monitor unavailable",
        _ => "exact monitor"
    };

    private static string PlacementLabel(RestoreTargetPlacement placement) => placement.Strategy switch
    {
        RestorePlacementStrategy.Semantic => $"{placement.SemanticKind} layout",
        RestorePlacementStrategy.Normalized => "normalized layout",
        RestorePlacementStrategy.LegacyDpiScaledAndClamped => "legacy layout kept visible",
        RestorePlacementStrategy.Unavailable => "saved coordinates retained",
        _ => "exact pixels"
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Workspace entry";
}
