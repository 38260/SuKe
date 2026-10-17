using System.Runtime.InteropServices;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

/// <summary>
/// Live-desktop verification of the maximized-window placement fix.
/// </summary>
/// <remarks>
/// <para>
/// The reported failure was a maximized window that was reported as Rejected on every retry with no
/// write error logged. Reproducing it needs a real HWND, so this test creates and destroys its own
/// throwaway window and never touches a user window. It is skipped when the host has fewer than two
/// monitors, because the defect only appears when a maximized window has to be moved to another one.
/// </para>
/// <para>
/// Two independent claims are checked: the placement mutation actually moves the maximized window to
/// the planned monitor (the frame ends up over that monitor's work area), and the DPI-aware verifier
/// then reports Applied instead of Rejected.
/// </para>
/// </remarks>
public class MaximizedPlacementLiveTests
{
    [Fact]
    public void Already_maximized_window_moves_to_the_planned_monitor_and_verifies()
    {
        // The host process may be DPI-unaware, which would virtualize every monitor coordinate and
        // make them incomparable with the physical pixels the window manager reports. Switching the
        // *thread* context works even when the process context is already fixed by the test host, and
        // matches the per-monitor-v2 awareness the application itself declares in its manifest.
        IntPtr previousAwareness = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            VerifyMaximizedPlacement();
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousAwareness);
        }
    }

    private static void VerifyMaximizedPlacement()
    {
        var monitors = new MonitorService().GetCurrentMonitors();
        if (monitors.Count < 2)
            return; // Single-monitor hosts cannot exercise a cross-monitor maximize.

        MonitorInfo primary = monitors.First(monitor => monitor.IsPrimary);
        MonitorInfo target = monitors.First(monitor => !monitor.IsPrimary);
        IntPtr hwnd = IntPtr.Zero;
        try
        {
            hwnd = CreateWindowExW(
                0, "Static", "wa-live-placement-probe", WindowStyle,
                primary.WorkAreaLeft + 200, primary.WorkAreaTop + 200, 640, 480,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, hwnd);
            ShowWindow(hwnd, SW_SHOW);
            ShowWindow(hwnd, SW_MAXIMIZE);
            Pump(TimeSpan.FromMilliseconds(700));

            WindowPlacementObservation before = new SystemWindowPlacementProbe().Observe(hwnd);
            Assert.Equal(3, before.ShowCmd);
            Assert.True(OverWorkArea(VisibleBounds(before), primary, 4),
                $"The probe window should start maximized on the primary monitor. " +
                $"observed visible={VisibleBounds(before)} normal={NormalBounds(before)} " +
                $"normal={NormalBounds(before)} showCmd={before.ShowCmd} dpi={before.Dpi}; " +
                $"primary work={WorkArea(primary)} dpi={primary.Dpi}; " +
                $"target work={WorkArea(target)} dpi={target.Dpi}");

            // The planned rectangle is a normal (restore-down) rectangle inside the target monitor,
            // exactly like a saved maximized window: its coordinates are not the maximized frame.
            int inset = 120;
            var record = new WindowRecord
            {
                ShowCmd = 3,
                NormalLeft = target.WorkAreaLeft + inset,
                NormalTop = target.WorkAreaTop + inset,
                NormalRight = target.WorkAreaRight - inset,
                NormalBottom = target.WorkAreaBottom - inset,
                SavedDpi = target.Dpi,
                CoordinatesAreFinal = true,
                CoordinatesRepresentVisibleBounds = false
            };

            new WindowService().RestoreWindow(hwnd, record);
            Pump(TimeSpan.FromSeconds(1.2));

            WindowPlacementObservation after = new SystemWindowPlacementProbe().Observe(hwnd);
            Assert.True(after.WindowExists && after.PlacementReadable);
            Assert.Equal(3, after.ShowCmd);

            // Claim 1: the maximize followed the planned monitor instead of staying where it was.
            Assert.True(OverWorkArea(VisibleBounds(after), target, 4),
                $"The maximized frame should sit over the planned monitor. Observed {VisibleBounds(after)} " +
                $"for target work area {WorkArea(target)}.");
            Assert.False(OverWorkArea(VisibleBounds(after), primary, 4),
                "The maximized frame must no longer be over the original monitor.");

            // Claim 2: the verifier now compares the same geometry the mutation wrote, so a correctly
            // placed maximized window verifies instead of being rejected forever.
            var planned = new RestoreTargetPlacement(
                target.MonitorId,
                target.Index,
                RestoreMonitorMappingKind.ExactStableId,
                record.NormalLeft,
                record.NormalTop,
                record.NormalRight,
                record.NormalBottom,
                3,
                target.Dpi,
                target.Dpi,
                false,
                RestorePlacementStrategy.Normalized,
                WindowLayoutKind.Custom);
            WindowPlacementEvaluation evaluation = WindowPlacementVerifier.Evaluate(
                planned,
                after,
                WindowPlacementPolicy(),
                finalObservation: true,
                wasPreviouslyApplied: false);

            Assert.Equal(WindowPlacementVerificationState.Applied, evaluation.State);
            Assert.NotNull(evaluation.Comparison);
            Assert.False(evaluation.Comparison!.ComparedVisibleFrame);
            Assert.True(evaluation.Comparison.ShowStateMatches);
            Assert.True(evaluation.Comparison.WithinTolerance);
        }
        finally
        {
            if (hwnd != IntPtr.Zero)
                DestroyWindow(hwnd);
        }
    }

    private const int WindowStyle = 0x00CF0000; // WS_OVERLAPPEDWINDOW
    private const int SW_SHOW = 5;
    private const int SW_MAXIMIZE = 3;

    private static WindowPlacementVerificationPolicy WindowPlacementPolicy() => new()
    {
        InitialDelay = TimeSpan.Zero,
        RetryDelay = TimeSpan.Zero,
        MaxRetries = 2
    };

    private static bool OverWorkArea(
        (int Left, int Top, int Right, int Bottom)? bounds,
        MonitorInfo monitor,
        int tolerance) =>
        bounds is { } rect &&
        Math.Abs(rect.Left - monitor.WorkAreaLeft) <= tolerance &&
        Math.Abs(rect.Top - monitor.WorkAreaTop) <= tolerance &&
        Math.Abs(rect.Right - monitor.WorkAreaRight) <= tolerance &&
        Math.Abs(rect.Bottom - monitor.WorkAreaBottom) <= tolerance;

    private static string NormalBounds(WindowPlacementObservation observation) =>
        $"L{observation.Left} T{observation.Top} R{observation.Right} B{observation.Bottom}";

    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    private static string WorkArea(MonitorInfo monitor) =>
        $"L{monitor.WorkAreaLeft} T{monitor.WorkAreaTop} " +
        $"R{monitor.WorkAreaRight} B{monitor.WorkAreaBottom}";

    /// <summary>Pumps this thread's message queue so the window manager can finish the placement.</summary>
    private static void Pump(TimeSpan duration)
    {
        System.Windows.Threading.Dispatcher dispatcher =
            System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(25),
            System.Windows.Threading.DispatcherPriority.Normal,
            (_, _) => frame.Continue = false,
            dispatcher);
        timer.Start();
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            frame.Continue = true;
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        timer.Stop();
    }

    private static (int Left, int Top, int Right, int Bottom)? VisibleBounds(
        WindowPlacementObservation observation) =>
        observation.HasVisibleBounds
            ? (observation.VisibleLeft!.Value, observation.VisibleTop!.Value,
               observation.VisibleRight!.Value, observation.VisibleBottom!.Value)
            : null;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
}
