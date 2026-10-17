using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 窗口「打开了什么」的最小证据集，用来区分用户手动打开的窗口与操作系统自己的外壳表面。
/// </summary>
/// <remarks>
/// 捕获侧与恢复侧掌握的事实并不一样，因此各自投影后再交给同一条原则判断：
/// <b>拿不出任何可恢复上下文的窗口不值得收录。</b>
/// <list type="bullet">
/// <item>捕获时只看到原生事实：标题与 <c>AppUserModelId</c>。文件夹、网址、标签页要到
/// <c>CaptureWindowRecord</c> 才解析出来，且 <c>SaveFiles=false</c> 时它们恒为空，
/// 所以捕获阶段只能用这两个字段。</item>
/// <item>恢复时还能读到保存下来的文件夹、网址、标签页、打开的文件与启动参数，证据更多。</item>
/// </list>
/// 证据越多越倾向保留——宁可多留，也不误杀用户数据。这与本产品「保留上下文而不只是几何」
/// 的定位一致：只有连一项上下文都说不出来的窗口才被丢弃。
/// </remarks>
internal readonly record struct WindowContentIdentity(
    string Title,
    string AppUserModelId,
    string WebAppName = "",
    string FolderPath = "",
    string BrowserUrl = "",
    bool HasExplorerTabs = false,
    bool HasTerminalTabs = false,
    string FilePath = "",
    string LaunchArg = "")
{
    /// <summary>从原生窗口事实投影；此阶段尚未解析文件夹与标签页。</summary>
    internal static WindowContentIdentity FromObserved(ObservedWindow window) => new(
        window.Title,
        window.AppUserModelId);

    /// <summary>
    /// 从已保存的条目投影。标题、文件夹、网址与标签页都存放在 <c>Position</c> 里，
    /// 只有应用身份（AUMID、Web 应用名）在顶层，顶层缺失时回落到 <c>Position</c>。
    /// </summary>
    internal static WindowContentIdentity FromSaved(WorkspaceEntry entry)
    {
        WindowRecord position = entry.Position ?? new WindowRecord();
        return new WindowContentIdentity(
            position.TitleSnippet ?? "",
            FirstNonEmpty(entry.AppUserModelId, position.AppUserModelId),
            entry.WebAppName ?? "",
            position.FolderPath ?? "",
            position.BrowserUrl ?? "",
            (position.ExplorerTabPaths?.Count ?? 0) > 0,
            (position.TerminalTabs?.Count ?? 0) > 0,
            entry.FilePath ?? "",
            entry.LaunchArg ?? "");
    }

    /// <summary>为 true 时表示这个窗口没有任何可恢复上下文。</summary>
    internal bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(AppUserModelId) &&
        string.IsNullOrWhiteSpace(WebAppName) &&
        string.IsNullOrWhiteSpace(FolderPath) &&
        string.IsNullOrWhiteSpace(BrowserUrl) &&
        string.IsNullOrWhiteSpace(FilePath) &&
        string.IsNullOrWhiteSpace(LaunchArg) &&
        !HasExplorerTabs &&
        !HasTerminalTabs;

    private static string FirstNonEmpty(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary! : fallback ?? "";
}

/// <summary>
/// 识别操作系统自己的外壳表面，把它们从捕获与恢复中排除，让恢复列表只留下用户
/// 手动打开的窗口。捕获与恢复两侧共用本类型，避免出现「保存时收进来、恢复时又显示」的不一致。
/// </summary>
/// <remarks>
/// 背景：Windows 用 <c>DWM_CLOAKED_SHELL</c> 隐藏非当前虚拟桌面上的窗口。为了不丢这些窗口，
/// 原实现会「先摘掉 cloaked 标记再判一次」，并且当虚拟桌面归属读不到时保守保留。
/// 但这条兜底把操作系统自己的外壳表面也一起放行了——本机实测是每块显示器一个、
/// 整屏工作区尺寸、空标题的 <c>ApplicationFrameWindow</c>（由 explorer.exe 持有）。
/// 用户从未打开过它们，恢复时也永远匹配不上，只会污染列表。
///
/// 两侧证据不同，因此判定形式也不同，但原则相同：
/// <list type="bullet">
/// <item>捕获侧能直接读到 cloak 状态，用它作为「被系统隐藏」的硬证据；</item>
/// <item>恢复侧读不到 cloak（历史文件里没有），改用窗口类作为兜底证据，
/// 并要求同时满足「无任何可恢复上下文」，双重条件才排除。</item>
/// </list>
/// 真实的资源管理器窗口窗口类是 <c>CabinetWClass</c>，不在宿主集合里，永远不会被排除。
/// </remarks>
internal static class ShellSurfaceFilter
{
    /// <summary>操作系统用来承载 XAML 表面的宿主窗口类；真实的资源管理器不在此列。</summary>
    private static readonly HashSet<string> ShellHostClasses = new(StringComparer.Ordinal)
    {
        "ApplicationFrameWindow",              // UWP/外壳 XAML 宿主；explorer.exe 持有的是外壳自己的
        "XamlExplorerHostIslandWindow",        // 任务切换、搜索等外壳表面
        "ApplicationManager_DesktopShellWindow" // 桌面外壳管理窗口
    };

    /// <summary>窗口类属于操作系统自己的 XAML 宿主。</summary>
    internal static bool IsShellHostClass(string? className) =>
        !string.IsNullOrWhiteSpace(className) && ShellHostClasses.Contains(className);

    /// <summary>虚拟桌面归属未知，无法证明窗口在别的桌面上。</summary>
    internal static bool ShellHiddenWithUnknownDesktop(VirtualDesktopCaptureResult desktop) =>
        desktop.Status != VirtualDesktopAssociationStatus.Available ||
        desktop.IsOnCurrentDesktop != true;

    /// <summary>
    /// 捕获侧判定。三条同时成立才算外壳表面：
    /// <list type="number">
    /// <item>被 Shell 隐藏（<c>DWM_CLOAKED_SHELL</c>）——普通窗口不会处于这个状态；</item>
    /// <item>虚拟桌面归属读不到，因此无法证明它属于别的虚拟桌面；</item>
    /// <item>无标题且无 <c>AppUserModelId</c>，完全说不出自己打开了什么。</item>
    /// </list>
    /// 真实的跨虚拟桌面窗口不会被误伤：它们几乎总有标题，且虚拟桌面查询通常可用。
    /// </summary>
    internal static bool IsShellSurface(ObservedWindow window, VirtualDesktopCaptureResult desktop) =>
        window.IsCloaked &&
        window.CloakState == WindowPolicyEvaluator.DwmCloakedByShell &&
        ShellHiddenWithUnknownDesktop(desktop) &&
        WindowContentIdentity.FromObserved(window).IsEmpty;

    /// <summary>
    /// 恢复侧判定。历史工作区（例如修改前保存的）里已经混入了外壳表面，而文件里没有
    /// cloak 信息，因此改用窗口类兜底：宿主窗口类 <b>且</b> 无任何可恢复上下文才排除。
    /// 被排除的条目标为 <c>Excluded</c>——不显示、不执行、不计数，
    /// <b>但保留原文件不动</b>，不触碰用户已保存的数据。
    /// </summary>
    internal static bool IsSavedShellSurface(WorkspaceEntry entry) =>
        IsShellHostClass(entry.WindowClassName) &&
        WindowContentIdentity.FromSaved(entry).IsEmpty;
}
