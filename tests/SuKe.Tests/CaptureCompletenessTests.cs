using SuKe.Native;
using SuKe.Services;

namespace SuKe.Tests;

public class CaptureCompletenessTests
{
    [Theory]
    [InlineData(VirtualDesktopAssociationStatus.Unavailable)]
    [InlineData(VirtualDesktopAssociationStatus.Unsupported)]
    [InlineData(VirtualDesktopAssociationStatus.Available)]
    public void Shell_task_with_unknown_desktop_is_retained_without_admitting_app_cloaking(VirtualDesktopAssociationStatus status)
    {
        var window = TaskWindow() with { IsCloaked = true, CloakState = 2 };
        var unknown = new VirtualDesktopCaptureResult(status);
        foreach (var policy in new[] { WindowCandidatePolicy.CaptureCandidate, WindowCandidatePolicy.RestoreMatchCandidate })
        {
            Assert.True(WindowCapturePolicy.IncludesShellCloakedTask(window, policy, unknown));
            Assert.False(WindowCapturePolicy.IncludesShellCloakedTask(window with { CloakState = 1 }, policy, unknown));
            Assert.False(WindowCapturePolicy.IncludesShellCloakedTask(window, policy,
                new(VirtualDesktopAssociationStatus.Available, Guid.NewGuid(), true)));
        }
        Assert.False(WindowCapturePolicy.IncludesShellCloakedTask(window, WindowCandidatePolicy.SwitchCloseCandidate, unknown));
    }

    [Fact]
    public void Untitled_tasks_are_captured_without_expanding_destructive_policies()
    {
        var window = TaskWindow() with { Title = "" };
        Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.CaptureCandidate));
        Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.RestoreMatchCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.SwitchCloseCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.MinimizeCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window with { ProcessId = 0 }, WindowCandidatePolicy.CaptureCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window with { ClassName = "#32768" }, WindowCandidatePolicy.CaptureCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window with { ClassName = "tooltips_class32" }, WindowCandidatePolicy.CaptureCandidate));
    }

    [Fact]
    public void Minimized_tasks_use_normal_size_for_capture_and_matching_only()
    {
        var window = TaskWindow() with
        {
            IsMinimized = true, Bounds = new WindowBounds(-32000, -32000, -31840, -31970),
            NormalBounds = new WindowBounds(-1500, 40, -700, 640)
        };
        Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.CaptureCandidate));
        Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.RestoreMatchCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.SwitchCloseCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.MinimizeCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window with { IsMinimized = false }, WindowCandidatePolicy.CaptureCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(window with { NormalBounds = new WindowBounds(0, 0, 40, 40) }, WindowCandidatePolicy.CaptureCandidate));
    }

    internal static ObservedWindow TaskWindow(IntPtr hwnd = default) => new(
        hwnd == IntPtr.Zero ? new IntPtr(101) : hwnd, 777, IntPtr.Zero, true,
        "CaptureTestWindow", "Window", new WindowBounds(10, 20, 810, 620),
        @"C:\Apps\editor.exe", "editor", "");

    [Fact]
    public void Independent_windows_of_one_process_remain_distinct()
    {
        var windows = WindowInventory.ResolveTaskProxyWindows(
            [TaskWindow(new IntPtr(101)), TaskWindow(new IntPtr(102))]);
        Assert.Equal(2, windows.Count);
        Assert.Equal(2, windows.Select(window => window.Hwnd).Distinct().Count());
        Assert.All(windows, window => Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.CaptureCandidate)));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    public void Shell_surfaces_stay_excluded(string className) =>
        Assert.False(WindowPolicyEvaluator.Includes(TaskWindow() with { ClassName = className }, WindowCandidatePolicy.CaptureCandidate));

    [Fact]
    public void Owned_and_tool_surfaces_stay_excluded()
    {
        Assert.False(WindowPolicyEvaluator.Includes(TaskWindow() with { OwnerHwnd = new IntPtr(7) }, WindowCandidatePolicy.CaptureCandidate));
        Assert.False(WindowPolicyEvaluator.Includes(TaskWindow() with { ExtendedStyle = NativeMethodsWindow.WS_EX_TOOLWINDOW }, WindowCandidatePolicy.CaptureCandidate));
    }
}
