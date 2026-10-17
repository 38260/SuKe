using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SuKe.Localization;

namespace SuKe.Services;

/// <summary>Re-observes assigned HWNDs and applies the existing bounded placement retry policy.</summary>
internal sealed class RestorePlacementVerificationPhase
{
    private readonly IWindowMutation _windowMutation;
    private readonly IRestoreClock _clock;
    private readonly IWindowPlacementProbe _placementProbe;
    private readonly WindowPlacementVerificationStrategyRegistry _placementStrategies;
    private readonly RestoreWindowRevalidator _revalidator;

    internal RestorePlacementVerificationPhase(
        IWindowMutation windowMutation,
        IRestoreClock clock,
        IWindowPlacementProbe placementProbe,
        WindowPlacementVerificationStrategyRegistry placementStrategies,
        RestoreWindowRevalidator revalidator)
    {
        _windowMutation = windowMutation;
        _clock = clock;
        _placementProbe = placementProbe;
        _placementStrategies = placementStrategies;
        _revalidator = revalidator;
    }

    internal async Task<bool> ExecuteAsync(
        RestoreExecutionContext context,
        CancellationToken cancellationToken,
        IProgress<RestoreProgressReport>? progress)
    {
        var pending = context.Entries.Values
            .Where(state => state.Status == RestoreExecutionEntryStatus.Restored &&
                            state.AssignedWindowHandle is not null &&
                            state.PlacementActionIndex is not null)
            .Select(state =>
            {
                (string name, WindowPlacementVerificationPolicy policy) =
                    _placementStrategies.Resolve(state.PlanEntry);
                return new PlacementVerificationSession(state, name, policy);
            })
            .ToDictionary(session => session.State.PlanEntry.EntryIndex);
        if (pending.Count == 0)
            return true;

        int totalPlacements = pending.Count;
        progress?.Report(new RestoreProgressReport(
            RestoreProgressStage.VerifyingPlacements,
            Loc.Format("Verifying {0} window placement(s)", totalPlacements),
            Loc.T("Checking final bounds and window state."),
            0,
            totalPlacements));

        // A first accepted observation completes immediately. Only windows that drift or remain
        // uncertain enter the bounded retry loop, so routine placements do not pay a delay.
        foreach (PlacementVerificationSession session in pending.Values.ToArray())
        {
            WindowPlacementEvaluation immediate = ObserveImmediateAcceptance(session);
            if (immediate.State != WindowPlacementVerificationState.Applied)
                continue;

            FinalizePlacementVerification(session, immediate, context.Results);
            pending.Remove(session.State.PlanEntry.EntryIndex);
            ReportProgress(progress, session.State, totalPlacements, pending.Count);
        }

        if (pending.Count == 0)
            return true;

        TimeSpan initialDelay = pending.Values.Max(session => session.Policy.InitialDelay);
        if (initialDelay > TimeSpan.Zero &&
            !await DelayOrCancelAsync(initialDelay, cancellationToken).ConfigureAwait(false))
        {
            CancelPendingPlacementVerification(pending.Values, context.Results);
            return false;
        }

        while (pending.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                CancelPendingPlacementVerification(pending.Values, context.Results);
                return false;
            }

            foreach (PlacementVerificationSession session in pending.Values.ToArray())
            {
                RestoreEntryExecutionState state = session.State;
                IntPtr hwnd = new(state.AssignedWindowHandle!.Value);
                WindowPlacementObservation observation = _placementProbe.Observe(hwnd);
                bool finalObservation = session.RetryCount >= session.Policy.MaxRetries;
                WindowPlacementEvaluation evaluation = WindowPlacementVerifier.Evaluate(
                    state.PlanEntry.TargetPlacement,
                    observation,
                    session.Policy,
                    finalObservation,
                    session.WasApplied);
                evaluation = NormalizeFinalEvaluation(session, evaluation);

                if (evaluation.State == WindowPlacementVerificationState.Applied ||
                    evaluation.State == WindowPlacementVerificationState.WindowGone ||
                    finalObservation)
                {
                    FinalizePlacementVerification(session, evaluation, context.Results);
                    pending.Remove(state.PlanEntry.EntryIndex);
                    ReportProgress(progress, state, totalPlacements, pending.Count);
                    continue;
                }

                uint expectedPid = state.PlanEntry.SelectedMatch?.ProcessId ?? 0;
                RestorePlanStaleReason? stale = _revalidator.Revalidate(
                    state.PlanEntry,
                    hwnd,
                    expectedPid);
                if (stale is not null)
                {
                    var staleEvaluation = new WindowPlacementEvaluation(
                        stale == RestorePlanStaleReason.WindowClosed
                            ? WindowPlacementVerificationState.WindowGone
                            : WindowPlacementVerificationState.Rejected,
                        evaluation.TolerancePixels,
                        stale == RestorePlanStaleReason.WindowClosed
                            ? "The assigned window closed before a placement retry."
                            : "The assigned HWND became stale before a placement retry.");
                    FinalizePlacementVerification(session, staleEvaluation, context.Results);
                    pending.Remove(state.PlanEntry.EntryIndex);
                    ReportProgress(progress, state, totalPlacements, pending.Count);
                    continue;
                }

                _windowMutation.RestoreSingleWindow(
                    hwnd,
                    RestoreExecutionSupport.ToWindowRecord(state.PlanEntry.TargetPlacement));
                session.RetryCount++;
                ObserveImmediateAcceptance(session);
            }

            if (pending.Count == 0)
                break;

            TimeSpan retryDelay = pending.Values.Max(session => session.Policy.RetryDelay);
            if (retryDelay > TimeSpan.Zero &&
                !await DelayOrCancelAsync(retryDelay, cancellationToken).ConfigureAwait(false))
            {
                CancelPendingPlacementVerification(pending.Values, context.Results);
                return false;
            }
        }

        return true;
    }

    private WindowPlacementEvaluation ObserveImmediateAcceptance(PlacementVerificationSession session)
    {
        IntPtr hwnd = new(session.State.AssignedWindowHandle!.Value);
        WindowPlacementEvaluation immediate = WindowPlacementVerifier.Evaluate(
            session.State.PlanEntry.TargetPlacement,
            _placementProbe.Observe(hwnd),
            session.Policy,
            finalObservation: false,
            session.WasApplied);
        session.WasApplied |= immediate.State == WindowPlacementVerificationState.Applied;
        return immediate;
    }

    private static void FinalizePlacementVerification(
        PlacementVerificationSession session,
        WindowPlacementEvaluation evaluation,
        IDictionary<int, RestoreExecutionActionResult> results)
    {
        RestoreEntryExecutionState state = session.State;
        bool succeeded = evaluation.State is
            WindowPlacementVerificationState.Applied or
            WindowPlacementVerificationState.Unavailable;
        // The reading is attached to every outcome, including success, so a later comparison never
        // has to be reconstructed from a rebuild.
        WindowPlacementComparison? comparison = evaluation.Comparison is null
            ? null
            : evaluation.Comparison with
            {
                State = evaluation.State,
                RetryCount = session.RetryCount,
                VerificationStrategy = session.Strategy
            };
        state.PlacementVerification = evaluation.State;
        state.PlacementRetryCount = session.RetryCount;
        state.PlacementVerificationStrategy = session.Strategy;
        state.PlacementTolerancePixels = evaluation.TolerancePixels;
        state.PlacementComparison = comparison;
        state.Explanation = evaluation.Explanation;
        if (!succeeded)
            state.Status = RestoreExecutionEntryStatus.Failed;

        int actionIndex = state.PlacementActionIndex!.Value;
        if (results.TryGetValue(actionIndex, out RestoreExecutionActionResult? action))
        {
            results[actionIndex] = action with
            {
                Status = succeeded
                    ? RestoreExecutionActionStatus.Succeeded
                    : RestoreExecutionActionStatus.Failed,
                Explanation = evaluation.Explanation,
                PlacementVerification = evaluation.State,
                PlacementRetryCount = session.RetryCount,
                PlacementVerificationStrategy = session.Strategy,
                PlacementTolerancePixels = evaluation.TolerancePixels,
                PlacementComparison = comparison
            };
        }

        Action<string, string, LogField[]> log = evaluation.State switch
        {
            WindowPlacementVerificationState.Applied => AppLogger.Info,
            WindowPlacementVerificationState.Unavailable => AppLogger.Warn,
            _ => AppLogger.Warn
        };
        log(
            evaluation.State switch
            {
                WindowPlacementVerificationState.Applied => "restore.entry.placement_verified",
                WindowPlacementVerificationState.Unavailable => "restore.entry.placement_unavailable",
                _ => "restore.entry.placement_verification_failed"
            },
            evaluation.State switch
            {
                WindowPlacementVerificationState.Applied => "Verified the final window placement",
                WindowPlacementVerificationState.Unavailable =>
                    "Application does not permit external window geometry control",
                _ => "Window placement did not verify within the bounded retry policy"
            },
            PlacementLogFields(state, evaluation, comparison, session.RetryCount));
    }

    /// <summary>
    /// Builds the placement log fields, including the target-versus-observed comparison so a
    /// failure can be explained from the log alone.
    /// </summary>
    /// <remarks>
    /// Bounds, show state, DPI, strategy, and retry counts are geometry and timing facts, not user
    /// content, so they are logged as public structured fields.
    /// </remarks>
    private static LogField[] PlacementLogFields(
        RestoreEntryExecutionState state,
        WindowPlacementEvaluation evaluation,
        WindowPlacementComparison? comparison,
        int retryCount)
    {
        var fields = new List<LogField>
        {
            LogField.Identifier("entryId", state.PlanEntry.EntryId),
            LogField.Public("entryIndex", state.PlanEntry.EntryIndex),
            LogField.Public("state", evaluation.State),
            LogField.Public("retryCount", retryCount),
            LogField.Public("strategy", comparison?.VerificationStrategy),
            LogField.Public("tolerancePixels", evaluation.TolerancePixels)
        };
        if (comparison is null)
            return fields.ToArray();

        fields.Add(LogField.Public("placementStrategy", comparison.PlanStrategy));
        fields.Add(LogField.Public(
            "comparedGeometry",
            comparison.ComparedVisibleFrame ? "visible_frame" : "normal_rectangle"));
        fields.Add(LogField.Public("targetBounds", comparison.TargetBounds));
        fields.Add(LogField.Public("targetShowCmd", comparison.TargetShowCmd));
        fields.Add(LogField.Public("targetDpi", comparison.TargetDpi));
        fields.Add(LogField.Public("observedBounds", comparison.ObservedBounds));
        fields.Add(LogField.Public("observedShowCmd", comparison.ObservedShowCmd));
        fields.Add(LogField.Public("observedDpi", comparison.ObservedDpi));
        fields.Add(LogField.Public("observedVisibleBounds", comparison.ObservedVisibleBounds));
        fields.Add(LogField.Public("showStateMatches", comparison.ShowStateMatches));
        fields.Add(LogField.Public("withinTolerance", comparison.WithinTolerance));
        fields.Add(LogField.Public("boundsDelta", comparison.Delta));
        return fields.ToArray();
    }

    private static WindowPlacementEvaluation NormalizeFinalEvaluation(
        PlacementVerificationSession session,
        WindowPlacementEvaluation evaluation)
    {
        if (evaluation.State != WindowPlacementVerificationState.Rejected ||
            !session.Policy.TreatRejectionAsUnavailable)
            return evaluation;

        return evaluation with
        {
            State = WindowPlacementVerificationState.Unavailable,
            Explanation =
                "The application does not permit external window geometry control; " +
                "launch and virtual-desktop restoration can still complete.",
            Comparison = evaluation.Comparison is null
                ? null
                : evaluation.Comparison with { State = WindowPlacementVerificationState.Unavailable }
        };
    }

    private static void CancelPendingPlacementVerification(
        IEnumerable<PlacementVerificationSession> pending,
        IDictionary<int, RestoreExecutionActionResult> results)
    {
        foreach (PlacementVerificationSession session in pending)
        {
            RestoreEntryExecutionState state = session.State;
            state.Status = RestoreExecutionEntryStatus.Cancelled;
            state.Explanation = "Cancellation interrupted post-restore placement verification.";
            if (state.PlacementActionIndex is int actionIndex &&
                results.TryGetValue(actionIndex, out RestoreExecutionActionResult? action))
            {
                results[actionIndex] = action with
                {
                    Status = RestoreExecutionActionStatus.Cancelled,
                    Explanation = state.Explanation,
                    PlacementRetryCount = session.RetryCount,
                    PlacementVerificationStrategy = session.Strategy
                };
            }
        }
    }

    private static void ReportProgress(
        IProgress<RestoreProgressReport>? progress,
        RestoreEntryExecutionState state,
        int totalPlacements,
        int pendingCount) =>
        progress?.Report(new RestoreProgressReport(
            RestoreProgressStage.VerifyingPlacements,
            Loc.T("Verifying window placements"),
            RestoreExecutionSupport.EntryDisplayName(state.PlanEntry),
            totalPlacements - pendingCount,
            totalPlacements));

    private async Task<bool> DelayOrCancelAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await _clock.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed class PlacementVerificationSession
    {
        internal PlacementVerificationSession(
            RestoreEntryExecutionState state,
            string strategy,
            WindowPlacementVerificationPolicy policy)
        {
            State = state;
            Strategy = strategy;
            Policy = policy;
        }

        internal RestoreEntryExecutionState State { get; }
        internal string Strategy { get; }
        internal WindowPlacementVerificationPolicy Policy { get; }
        internal int RetryCount { get; set; }
        internal bool WasApplied { get; set; }
    }
}
