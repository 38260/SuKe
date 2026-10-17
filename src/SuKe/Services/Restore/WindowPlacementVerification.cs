using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SuKe.Models;
using SuKe.Native;

namespace SuKe.Services;

/// <summary>Observed outcome of applying a planned placement to one assigned HWND.</summary>
public enum WindowPlacementVerificationState
{
    Applied,
    Settling,
    Unavailable,
    Rejected,
    MovedByApp,
    WindowGone
}

/// <summary>One read-only placement observation made after a restore mutation.</summary>
public sealed record WindowPlacementObservation(
    bool WindowExists,
    bool PlacementReadable,
    int Left,
    int Top,
    int Right,
    int Bottom,
    int ShowCmd,
    uint Dpi)
{
    public int? VisibleLeft { get; init; }
    public int? VisibleTop { get; init; }
    public int? VisibleRight { get; init; }
    public int? VisibleBottom { get; init; }

    public bool HasVisibleBounds =>
        VisibleLeft.HasValue && VisibleTop.HasValue &&
        VisibleRight.HasValue && VisibleBottom.HasValue &&
        VisibleRight.Value > VisibleLeft.Value && VisibleBottom.Value > VisibleTop.Value;

    public static WindowPlacementObservation Gone { get; } =
        new(false, false, 0, 0, 0, 0, 0, 96);

    public static WindowPlacementObservation Unreadable { get; } =
        new(true, false, 0, 0, 0, 0, 0, 96);
}

/// <summary>Bounded, DPI-aware verification policy for one application family.</summary>
public sealed record WindowPlacementVerificationPolicy
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(350);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(350);
    public int MaxRetries { get; init; } = 2;
    public int BaseTolerancePixels { get; init; } = 8;
    public bool TreatRejectionAsUnavailable { get; init; }

    public static WindowPlacementVerificationPolicy Default { get; } = new();

    internal void Validate()
    {
        if (InitialDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(InitialDelay));
        if (RetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryDelay));
        if (MaxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetries));
        if (BaseTolerancePixels < 0)
            throw new ArgumentOutOfRangeException(nameof(BaseTolerancePixels));
    }
}

/// <summary>Application-specific override for placement tolerance and bounded retry timing.</summary>
public interface IWindowPlacementVerificationStrategy
{
    string Name { get; }
    bool CanHandle(SavedWindowIdentity identity);
    WindowPlacementVerificationPolicy GetPolicy(RestorePlanEntry entry);
}

/// <summary>Read-only boundary used to inspect a specific assigned HWND.</summary>
public interface IWindowPlacementProbe
{
    WindowPlacementObservation Observe(IntPtr hwnd);
}

/// <summary>A window rectangle expressed the way both the plan and a live window report it.</summary>
public readonly record struct WindowPlacementBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    /// <summary>Per-edge signed difference between an observed rectangle and the planned one.</summary>
    public static WindowPlacementBounds Delta(WindowPlacementBounds observed, WindowPlacementBounds target) =>
        new(observed.Left - target.Left, observed.Top - target.Top,
            observed.Right - target.Right, observed.Bottom - target.Bottom);

    public bool IsWithin(int tolerance) =>
        Math.Abs(Left) <= tolerance && Math.Abs(Top) <= tolerance &&
        Math.Abs(Right) <= tolerance && Math.Abs(Bottom) <= tolerance;

    public override string ToString() =>
        $"L{Left} T{Top} R{Right} B{Bottom} ({Width}x{Height})";
}

/// <summary>
/// Read-only target-versus-observed evidence for exactly one placement verification pass.
/// </summary>
/// <remarks>
/// This type exists so a failed verification can be explained without re-instrumenting a build:
/// it records which geometry was compared, both sides of the comparison, the show state and DPI of
/// each side, the bounded retry counter, and the tolerance that was applied. It never influences
/// the verification state.
/// </remarks>
public sealed record WindowPlacementComparison(
    WindowPlacementVerificationState State,
    RestorePlacementStrategy PlanStrategy,
    int TargetShowCmd,
    uint TargetDpi,
    WindowPlacementBounds TargetBounds,
    bool ObservedExists,
    bool ObservedPlacementReadable,
    int ObservedShowCmd,
    uint ObservedDpi,
    WindowPlacementBounds ObservedBounds,
    WindowPlacementBounds? ObservedVisibleBounds,
    bool ComparedVisibleFrame,
    bool ShowStateMatches,
    WindowPlacementBounds Delta,
    int TolerancePixels)
{
    /// <summary>Bounded retry counter reported by the verification phase. Filled in by the phase.</summary>
    public int RetryCount { get; init; }

    /// <summary>Application-family verification policy name. Filled in by the phase.</summary>
    public string? VerificationStrategy { get; init; }

    /// <summary>True when every compared edge stayed inside the DPI-scaled tolerance.</summary>
    public bool WithinTolerance => Delta.IsWithin(TolerancePixels);

    /// <summary>Evidence for a window that no longer exists, so no geometry could be read.</summary>
    public static WindowPlacementComparison Missing(
        RestoreTargetPlacement target,
        int tolerance) => new(
        WindowPlacementVerificationState.WindowGone,
        target.Strategy,
        target.ShowCmd,
        target.TargetDpi,
        new WindowPlacementBounds(target.Left, target.Top, target.Right, target.Bottom),
        ObservedExists: false,
        ObservedPlacementReadable: false,
        ObservedShowCmd: 0,
        ObservedDpi: 0,
        new WindowPlacementBounds(0, 0, 0, 0),
        ObservedVisibleBounds: null,
        ComparedVisibleFrame: false,
        ShowStateMatches: false,
        new WindowPlacementBounds(0, 0, 0, 0),
        tolerance);
}

/// <summary>Final or intermediate comparison result produced without side effects.</summary>
public sealed record WindowPlacementEvaluation(
    WindowPlacementVerificationState State,
    int TolerancePixels,
    string Explanation,
    WindowPlacementComparison? Comparison = null);

/// <summary>Pure DPI-aware comparison between planned and observed normal placement.</summary>
public static class WindowPlacementVerifier
{
    public static WindowPlacementEvaluation Evaluate(
        RestoreTargetPlacement target,
        WindowPlacementObservation observation,
        WindowPlacementVerificationPolicy policy,
        bool finalObservation,
        bool wasPreviouslyApplied)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        int tolerance = (int)Math.Ceiling(
            policy.BaseTolerancePixels * Math.Max(target.TargetDpi, 96u) / 96d);
        if (!observation.WindowExists)
        {
            return new WindowPlacementEvaluation(
                WindowPlacementVerificationState.WindowGone,
                tolerance,
                "The assigned window closed before placement verification completed.",
                WindowPlacementComparison.Missing(target, tolerance));
        }

        // The mutation compensates a Semantic/Normalized plan against the DWM visible frame only
        // while it places a *normal* window (RestoreExecutionSupport.ToWindowRecord). For a
        // maximized or minimized target the plan rectangle is the window's rcNormalPosition
        // instead, while the DWM visible frame of such a window is the monitor work area.
        // Comparing those two different quantities can never match, so a maximized window was
        // rejected on every retry. The comparison below therefore follows the same rule as the
        // write it verifies — and it strictly narrows what is compared, never widens it.
        bool useVisibleBounds = target.ShowCmd == 1 &&
            target.Strategy is RestorePlacementStrategy.Semantic or RestorePlacementStrategy.Normalized &&
            observation.HasVisibleBounds;
        var targetBounds = new WindowPlacementBounds(
            target.Left, target.Top, target.Right, target.Bottom);
        var observedBounds = useVisibleBounds
            ? new WindowPlacementBounds(
                observation.VisibleLeft!.Value,
                observation.VisibleTop!.Value,
                observation.VisibleRight!.Value,
                observation.VisibleBottom!.Value)
            : new WindowPlacementBounds(
                observation.Left, observation.Top, observation.Right, observation.Bottom);
        WindowPlacementBounds? visibleBounds = observation.HasVisibleBounds
            ? new WindowPlacementBounds(
                observation.VisibleLeft!.Value,
                observation.VisibleTop!.Value,
                observation.VisibleRight!.Value,
                observation.VisibleBottom!.Value)
            : null;
        bool showStateMatches = EquivalentShowState(observation.ShowCmd, target.ShowCmd);
        var delta = WindowPlacementBounds.Delta(observedBounds, targetBounds);
        bool applied = observation.PlacementReadable &&
            delta.IsWithin(tolerance) &&
            showStateMatches;

        var comparison = new WindowPlacementComparison(
            WindowPlacementVerificationState.Settling,
            target.Strategy,
            target.ShowCmd,
            target.TargetDpi,
            targetBounds,
            ObservedExists: true,
            observation.PlacementReadable,
            observation.ShowCmd,
            observation.Dpi,
            observedBounds,
            visibleBounds,
            useVisibleBounds,
            showStateMatches,
            delta,
            tolerance);
        if (applied)
        {
            return new WindowPlacementEvaluation(
                WindowPlacementVerificationState.Applied,
                tolerance,
                "The assigned window matches the planned normal bounds and show state.",
                comparison with { State = WindowPlacementVerificationState.Applied });
        }

        if (!finalObservation)
        {
            return new WindowPlacementEvaluation(
                WindowPlacementVerificationState.Settling,
                tolerance,
                observation.PlacementReadable
                    ? "The assigned window has not settled at the planned placement yet."
                    : "The assigned window placement is temporarily unreadable while it settles.",
                comparison with { State = WindowPlacementVerificationState.Settling });
        }

        WindowPlacementVerificationState finalState = wasPreviouslyApplied
            ? WindowPlacementVerificationState.MovedByApp
            : WindowPlacementVerificationState.Rejected;
        return new WindowPlacementEvaluation(
            finalState,
            tolerance,
            wasPreviouslyApplied
                ? "The app moved its window after the planned placement was observed."
                : "The window did not accept the planned placement within the bounded retry policy.",
            comparison with { State = finalState });
    }

    private static bool EquivalentShowState(int actual, int expected)
    {
        static int Category(int showCmd) => showCmd switch
        {
            3 => 3,
            2 or 6 or 7 or 11 => 2,
            _ => 1
        };
        return Category(actual) == Category(expected);
    }
}

/// <summary>Native placement probe used by the production restore service.</summary>
public sealed class SystemWindowPlacementProbe : IWindowPlacementProbe
{
    public WindowPlacementObservation Observe(IntPtr hwnd)
    {
        if (!NativeMethodsWindow.IsWindow(hwnd))
            return WindowPlacementObservation.Gone;

        var placement = new NativeMethodsWindow.WindowPlacement
        {
            Length = Marshal.SizeOf<NativeMethodsWindow.WindowPlacement>()
        };
        if (!NativeMethodsWindow.GetWindowPlacement(hwnd, ref placement))
            return WindowPlacementObservation.Unreadable;

        var observation = new WindowPlacementObservation(
            true,
            true,
            placement.RcNormalPosition.Left,
            placement.RcNormalPosition.Top,
            placement.RcNormalPosition.Right,
            placement.RcNormalPosition.Bottom,
            placement.ShowCmd,
            NativeMethodsWindow.GetDpiForWindow(hwnd));
        if (NativeMethodsWindow.DwmGetWindowAttribute(
                hwnd,
                NativeMethodsWindow.DWMWA_EXTENDED_FRAME_BOUNDS,
                out NativeMethodsWindow.Rect visible,
                Marshal.SizeOf<NativeMethodsWindow.Rect>()) == 0 &&
            visible.Right > visible.Left && visible.Bottom > visible.Top)
        {
            observation = observation with
            {
                VisibleLeft = visible.Left,
                VisibleTop = visible.Top,
                VisibleRight = visible.Right,
                VisibleBottom = visible.Bottom
            };
        }
        return observation;
    }
}

/// <summary>Inventory-backed fallback that keeps service tests independent of native HWNDs.</summary>
public sealed class InventoryWindowPlacementProbe : IWindowPlacementProbe
{
    private readonly IWindowInventory _inventory;

    public InventoryWindowPlacementProbe(IWindowInventory inventory) =>
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

    public WindowPlacementObservation Observe(IntPtr hwnd)
    {
        Dictionary<IntPtr, (uint Pid, WindowRecord Record)> windows =
            _inventory.GetWindowsWithPids(WindowCandidatePolicy.RestoreMatchCandidate);
        if (windows.TryGetValue(hwnd, out (uint Pid, WindowRecord Record) observed))
        {
            WindowRecord record = observed.Record;
            return new WindowPlacementObservation(
                true,
                true,
                record.NormalLeft,
                record.NormalTop,
                record.NormalRight,
                record.NormalBottom,
                record.ShowCmd,
                record.SavedDpi > 0 ? record.SavedDpi : 96);
        }
        return _inventory.IsWindowAlive(hwnd)
            ? WindowPlacementObservation.Unreadable
            : WindowPlacementObservation.Gone;
    }
}

/// <summary>Resolves per-app verification policy, falling back to the generic bounded policy.</summary>
public sealed class WindowPlacementVerificationStrategyRegistry
{
    private readonly IReadOnlyList<IWindowPlacementVerificationStrategy> _strategies;
    private readonly WindowPlacementVerificationPolicy _fallback;

    public WindowPlacementVerificationStrategyRegistry(
        WindowPlacementVerificationPolicy? fallback = null,
        IEnumerable<IWindowPlacementVerificationStrategy>? strategies = null)
    {
        _fallback = fallback ?? WindowPlacementVerificationPolicy.Default;
        _fallback.Validate();
        _strategies = (strategies ?? Array.Empty<IWindowPlacementVerificationStrategy>()).ToArray();
    }

    public (string Name, WindowPlacementVerificationPolicy Policy) Resolve(RestorePlanEntry entry)
    {
        IWindowPlacementVerificationStrategy? strategy = _strategies.FirstOrDefault(candidate =>
            candidate.CanHandle(entry.SavedIdentity));
        WindowPlacementVerificationPolicy policy = strategy?.GetPolicy(entry) ?? _fallback;
        policy.Validate();
        return (strategy?.Name ?? "generic", policy);
    }
}
