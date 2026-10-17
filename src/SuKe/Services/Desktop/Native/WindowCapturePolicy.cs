namespace SuKe.Services;

/// <summary>仅采集与匹配可以保留身份明确但桌面查询受限的 Shell 隐藏窗口。</summary>
internal static class WindowCapturePolicy
{
    internal static bool IncludesShellCloakedTask(ObservedWindow window, WindowCandidatePolicy policy,
        VirtualDesktopCaptureResult desktop) =>
        WindowPolicyEvaluator.IncludesInactiveVirtualDesktopCandidate(window, policy) &&
        (desktop.Status != VirtualDesktopAssociationStatus.Available || desktop.IsOnCurrentDesktop != true);
}
