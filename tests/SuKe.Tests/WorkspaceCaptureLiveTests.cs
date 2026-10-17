using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SuKe.Models;
using SuKe.Native;
using SuKe.Services;
using SuKe.UI;
using Xunit.Abstractions;

namespace SuKe.Tests;

internal sealed class LiveCaptureFactAttribute : FactAttribute
{
    public LiveCaptureFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SUKE_LIVE_CAPTURE_SMOKE") != "1")
            Skip = "Set SUKE_LIVE_CAPTURE_SMOKE=1 in an interactive Windows session.";
    }
}

public class WorkspaceCaptureLiveTests(ITestOutputHelper output)
{
    [LiveCaptureFact]
    public async Task Controlled_windows_survive_occlusion_minimization_selection_and_virtual_desktop_capture()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        Window? first = null;
        Window? second = null;
        Exception? windowError = null;
        IntPtr firstHandle = IntPtr.Zero, secondHandle = IntPtr.Zero;
        var thread = new Thread(() =>
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                first = new Window { Title = "SuKe capture test", Left = 100, Top = 100,
                    Width = 480, Height = 320, ShowInTaskbar = true, ShowActivated = false };
                second = new Window { Title = "", Left = 100, Top = 100,
                    Width = 600, Height = 440, ShowInTaskbar = true, ShowActivated = false };
                first.Show();
                second.Show();
                firstHandle = new WindowInteropHelper(first).Handle;
                secondHandle = new WindowInteropHelper(second).Handle;
                ready.Set();
                Dispatcher.Run();
            }
            catch (Exception ex) { windowError = ex; ready.Set(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "Controlled windows did not start.");
            if (windowError is not null) ExceptionDispatchInfo.Capture(windowError).Throw();
            var raw = new OwnedInventory([firstHandle, secondHandle]);
            var native = new WindowService(raw, explorerTabs: new NoExplorer());
            var monitors = new MonitorService();
            var observed = raw.EnumerateWindows();
            Assert.Equal(2, observed.Count);
            Assert.Equal(observed[0].ProcessId, observed[1].ProcessId);
            var a = Assert.Single(observed, window => window.Hwnd == firstHandle);
            var b = Assert.Single(observed, window => window.Hwnd == secondHandle);
            Assert.Empty(b.Title);
            Assert.True(b.Bounds!.Value.Left <= a.Bounds!.Value.Left && b.Bounds.Value.Right >= a.Bounds.Value.Right);
            Assert.True(b.Bounds.Value.Top <= a.Bounds.Value.Top && b.Bounds.Value.Bottom >= a.Bounds.Value.Bottom);
            Assert.True(observed.ToList().FindIndex(window => window.Hwnd == secondHandle) <
                        observed.ToList().FindIndex(window => window.Hwnd == firstHandle));
            Assert.Equal(2, native.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate, monitors.GetCurrentMonitors()).Count);
            output.WriteLine("Real same-process windows: obscured task and untitled independent task both captured.");

            dispatcher!.Invoke(() => first!.WindowState = WindowState.Minimized);
            a = Assert.Single(raw.EnumerateWindows(), window => window.Hwnd == firstHandle);
            Assert.True(a.IsMinimized);
            Assert.True(a.Bounds!.Value.Height < 100);
            Assert.True(a.NormalBounds!.Value.Height >= 100);
            var captured = native.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate, monitors.GetCurrentMonitors());
            Assert.Equal(2, captured.Count);
            Assert.Equal(2, Assert.Single(captured, record => record.SourceWindowHandle == firstHandle).ShowCmd);
            Assert.True(NativeMethodsWindow.IsIconic(firstHandle));
            Assert.All(captured, record => Assert.True(record.CanCapture));
            var matches = native.GetWindowsWithPids(WindowCandidatePolicy.RestoreMatchCandidate);
            Assert.Contains(firstHandle, matches.Keys);
            Assert.Contains(secondHandle, matches.Keys);
            output.WriteLine($"Real minimized bounds={a.Bounds.Value.Width}x{a.Bounds.Value.Height}; normal={a.NormalBounds.Value.Width}x{a.NormalBounds.Value.Height}; showCmd=2; remains minimized after capture.");

            using var directory = new TestDirectory();
            var storage = new StorageService(directory.Path);
            await using var workspace = new WorkspaceService(storage, native, monitors, new JumpListService());
            var preview = workspace.GetWindowPreviewForDialog();
            var groups = SaveWorkspaceDialog.BuildGroups(preview, desktops: VirtualDesktopCatalog.Read());
            var selected = groups.SelectMany(group => group.Windows).Where(item => item.IsSelected).Select(item => item.Record).ToList();
            Assert.Equal(2, selected.Count);
            var capture = await workspace.CaptureWorkspaceAsync("Controlled capture acceptance", saveFiles: false,
                selectedWindows: selected, captureBrowserSessions: false);
            workspace.PersistCapture(capture, WorkspaceArtifactKind.NamedWorkspace, IncompleteBrowserCapturePolicy.SavePartialWorkspace);
            Assert.Equal(2, Assert.Single(storage.LoadAllWorkspaces()).Entries.Count);
            Assert.True(NativeMethodsWindow.IsIconic(firstHandle));
            output.WriteLine("Real preview selection -> capture -> isolated persistence: 2 selected, 2 saved, minimized state preserved.");

            dispatcher.Invoke(() => second!.Title = "SuKe virtual capture test");
            var desktops = new VirtualDesktopAssociationService();
            bool inspected = false;
            bool moved = desktops.TryMoveWindowRoundTripForSmokeTest(secondHandle, out Guid created, out string diagnostic,
                inactiveId =>
                {
                    var inactive = Assert.Single(raw.EnumerateWindows(), window => window.Hwnd == secondHandle);
                    Assert.True(inactive.IsCloaked);
                    var page = Assert.Single(native.SnapshotWindows(WindowCandidatePolicy.CaptureCandidate, monitors.GetCurrentMonitors()),
                        record => record.SourceWindowHandle == secondHandle);
                    Assert.False(page.IsOnCurrentVirtualDesktop);
                    Assert.Equal(inactiveId.ToString("D"), page.VirtualDesktopId);
                    Assert.Contains(secondHandle, native.GetWindowsWithPids(WindowCandidatePolicy.RestoreMatchCandidate).Keys);
                    inspected = true;
                });
            output.WriteLine($"Real temporary desktop round trip: moved={moved}, inspected={inspected}; {diagnostic}");
            Assert.True(moved, diagnostic);
            Assert.True(inspected);
            Assert.DoesNotContain(VirtualDesktopCatalog.Read(), desktop => desktop.Id == created);
        }
        finally
        {
            if (thread.IsAlive && dispatcher is { HasShutdownStarted: false })
                _ = dispatcher.BeginInvoke(() => { first?.Close(); second?.Close(); dispatcher.InvokeShutdown(); });
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Controlled window thread did not exit.");
        }
    }

    private sealed class OwnedInventory(IReadOnlyList<IntPtr> handles) : IRawWindowInventory
    {
        private readonly WindowInventory _native = new();
        public IReadOnlyList<ObservedWindow> EnumerateWindows() => _native.EnumerateWindows().Where(window => handles.Contains(window.Hwnd)).ToArray();
        public bool IsWindowAlive(IntPtr hwnd) => _native.IsWindowAlive(hwnd);
    }

    private sealed class NoExplorer : IExplorerTabSessionCapture
    {
        public IReadOnlyDictionary<IntPtr, ExplorerWindowSession> CaptureOpenWindows() => new Dictionary<IntPtr, ExplorerWindowSession>();
    }
}
