using System.Text.Json;
using SuKe.Services;
using SuKe.UI;

namespace SuKe.Tests;

public class WindowCaptureDiagnosticsTests
{
    [Fact]
    public void Failed_candidate_remains_visible_but_unsavable_and_diagnostics_are_redacted()
    {
        var raw = new RawInventory
        {
            Windows = [CaptureCompletenessTests.TaskWindow(new IntPtr(0x7fffffff)) with
                { Title = "PRIVATE_TITLE", ExecutablePath = @"C:\PRIVATE_PATH\editor.exe" },
                CaptureCompletenessTests.TaskWindow(new IntPtr(1)) with { ClassName = "Shell_TrayWnd" }]
        };
        var service = new WindowService(raw, explorerTabs: new NoExplorer(), virtualDesktops: new NoDesktops());
        var record = Assert.Single(service.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate, []));
        Assert.False(record.CanCapture);
        Assert.NotEmpty(record.CaptureLimitation);
        var diagnostics = Assert.IsType<WindowCaptureDiagnostics>(service.LastCaptureDiagnostics);
        Assert.Equal(2, diagnostics.Discovered);
        Assert.Equal(0, diagnostics.Savable);
        Assert.Equal(1, diagnostics.Filtered);
        Assert.Equal(1, diagnostics.Failed);
        Assert.Equal(1, diagnostics.Limited);
        Assert.Equal(1, diagnostics.Reasons[WindowCaptureReason.ReadFailed]);
        Assert.Equal(1, diagnostics.Reasons[WindowCaptureReason.SystemWindow]);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(diagnostics));
        var group = Assert.Single(SaveWorkspaceDialog.BuildGroups([(null, [record])]));
        var item = Assert.Single(group.Windows);
        Assert.False(item.IsSelected);
        item.IsSelected = true;
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void Enumeration_failure_is_distinct_from_no_windows_found()
    {
        var service = new WindowService(new RawInventory { Throw = true }, explorerTabs: new NoExplorer());
        Assert.Throws<InvalidOperationException>(() => service.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate));
        Assert.True(service.LastCaptureDiagnostics!.EnumerationFailed);
        Assert.NotEqual(WindowCaptureText.Summary(service.LastCaptureDiagnostics, 0),
            WindowCaptureText.Summary(new WindowCaptureDiagnostics(), 0));
    }

    [Fact]
    public void Observation_failures_are_counted_without_a_fabricated_window_record()
    {
        var service = new WindowService(new RawInventory
        {
            Windows = [CaptureCompletenessTests.TaskWindow() with { IsVisible = false, ObservationFailed = true }]
        }, explorerTabs: new NoExplorer());
        Assert.Empty(service.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate));
        Assert.Equal(1, service.LastCaptureDiagnostics!.Failed);
        Assert.Equal(1, service.LastCaptureDiagnostics.Discovered);
    }

    private sealed class RawInventory : IRawWindowInventory
    {
        internal IReadOnlyList<ObservedWindow> Windows { get; init; } = [];
        internal bool Throw { get; init; }
        public IReadOnlyList<ObservedWindow> EnumerateWindows() => Throw ? throw new InvalidOperationException("Injected enumeration failure") : Windows;
        public bool IsWindowAlive(IntPtr hwnd) => true;
    }

    private sealed class NoExplorer : IExplorerTabSessionCapture
    {
        public IReadOnlyDictionary<IntPtr, ExplorerWindowSession> CaptureOpenWindows() => new Dictionary<IntPtr, ExplorerWindowSession>();
    }

    private sealed class NoDesktops : IVirtualDesktopAssociation
    {
        public VirtualDesktopCaptureResult TryGetWindowDesktopId(IntPtr hwnd) => new(VirtualDesktopAssociationStatus.Unavailable);
        public VirtualDesktopMoveResult TryMoveWindowToDesktop(IntPtr hwnd, Guid id) => throw new NotSupportedException();
        public VirtualDesktopTopologyResult EnsureTopology(IReadOnlyList<SuKe.Models.SavedVirtualDesktop> desktops) => throw new NotSupportedException();
    }
}
