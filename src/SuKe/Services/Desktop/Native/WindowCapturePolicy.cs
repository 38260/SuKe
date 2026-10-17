namespace SuKe.Services;

/// <summary>
/// 仅采集与匹配身份明确、但桌面查询受限的 Shell 隐藏窗口；
/// 拿不出任何可恢复上下文的操作系统外壳表面一律排除。
/// </summary>
internal static class WindowCapturePolicy
{
    internal static bool IncludesShellCloakedTask(ObservedWindow window, WindowCandidatePolicy policy,
        VirtualDesktopCaptureResult desktop) =>
        WindowPolicyEvaluator.IncludesInactiveVirtualDesktopCandidate(window, policy) &&
        ShellSurfaceFilter.ShellHiddenWithUnknownDesktop(desktop) &&
        !ShellSurfaceFilter.IsShellSurface(window, desktop);
}
