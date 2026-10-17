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

    /// <summary>
    /// 操作系统自己的外壳表面：被 Shell 隐藏、虚拟桌面归属读不到、空标题、无 AUMID。
    /// 这正是本机每块显示器一个、整屏尺寸的 <c>ApplicationFrameWindow</c>（explorer.exe 持有）。
    /// </summary>
    [Fact]
    public void Shell_cloaked_task_without_any_restorable_context_is_excluded()
    {
        var window = TaskWindow() with
        {
            Title = "",
            AppUserModelId = "",
            ClassName = "ApplicationFrameWindow",
            IsCloaked = true,
            CloakState = 2
        };
        var unknown = new VirtualDesktopCaptureResult(VirtualDesktopAssociationStatus.Unavailable);

        Assert.True(ShellSurfaceFilter.IsShellSurface(window, unknown));
        foreach (var policy in new[] { WindowCandidatePolicy.CaptureCandidate, WindowCandidatePolicy.RestoreMatchCandidate })
            Assert.False(WindowCapturePolicy.IncludesShellCloakedTask(window, policy, unknown));
    }

    /// <summary>只要还能说出标题或 AUMID，就仍然当作用户的窗口保留。</summary>
    [Theory]
    [InlineData("", "Microsoft.Windows.Explorer")]
    [InlineData("随机过程 - 文件资源管理器", "")]
    public void Shell_cloaked_task_with_restorable_context_is_retained(string title, string aumid)
    {
        var window = TaskWindow() with
        {
            Title = title,
            AppUserModelId = aumid,
            ClassName = "ApplicationFrameWindow",
            IsCloaked = true,
            CloakState = 2
        };
        var unknown = new VirtualDesktopCaptureResult(VirtualDesktopAssociationStatus.Unavailable);

        Assert.False(ShellSurfaceFilter.IsShellSurface(window, unknown));
        Assert.True(WindowCapturePolicy.IncludesShellCloakedTask(
            window, WindowCandidatePolicy.CaptureCandidate, unknown));
    }

    /// <summary>普通（未被 Shell 隐藏的）无标题窗口走标准判定，不受这道门槛影响。</summary>
    [Fact]
    public void Ordinary_untitled_task_is_unaffected_by_the_shell_surface_gate()
    {
        var window = TaskWindow() with { Title = "" };
        Assert.False(ShellSurfaceFilter.IsShellSurface(
            window, new VirtualDesktopCaptureResult(VirtualDesktopAssociationStatus.Unavailable)));
        Assert.True(WindowPolicyEvaluator.Includes(window, WindowCandidatePolicy.CaptureCandidate));
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
