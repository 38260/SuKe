using System.Collections.Generic;

namespace SuKe.Localization;

internal static partial class LocalizationCatalog
{
    private static void AddReference(Dictionary<string, string> entries)
    {
        Add(entries, "SuKe could not finish startup or open its main window. Error type: {0}. Details: {1}. Check app.log for details.", "SuKe 无法完成启动或打开主界面。错误类型：{0}。详情：{1}。请查看 app.log 获取详情。");
        Add(entries, "OwnApplicationWindow", "本应用窗口");
        Add(entries, "Application identity unavailable.", "无法读取应用身份，暂不可保存。");
        Add(entries, "Window enumeration failed. Refresh the list and try again.", "窗口枚举失败，请刷新列表后重试。");
        Add(entries, "Listed windows: {0}", "列表窗口：{0}");
        Add(entries, "Discovered {0}; saveable {1}; filtered {2}; limited {3}; failed {4}. Desktop unknown {5}; monitor unknown {6}.", "发现 {0}；可保存 {1}；已过滤 {2}；受限 {3}；失败 {4}。桌面未识别 {5}；显示器未识别 {6}。");
        Add(entries, "Window details could not be read.", "无法读取窗口恢复信息，暂不可保存。");
        Add(entries, "Window closed during capture.", "窗口已在采集过程中关闭。");
        Add(entries, "Executable path unavailable; reopening may be limited.", "无法读取程序路径，重新启动该程序可能受限。");
        Add(entries, "SystemWindow", "系统窗口");
        Add(entries, "HiddenWindow", "隐藏窗口");
        Add(entries, "AuxiliaryWindow", "辅助窗口");
        Add(entries, "CloakedSurface", "被隐藏的非目标窗口");
        Add(entries, "SmallWindow", "尺寸不足");
        Add(entries, "IdentityUnavailable", "应用身份未知");
        Add(entries, "WindowClosed", "窗口已关闭");
        Add(entries, "ReadFailed", "读取失败");
        Add(entries, "CaptureFailed", "采集失败");
        Add(entries, "Selected windows changed or cannot be read ({0}). Refresh the window list and try again.", "选中的窗口已变化或无法读取（{0} 个）。请刷新窗口列表后重试。");
        Add(entries, "Unidentified monitor ({0})", "显示器未识别（{0}）");
        Add(entries, "Desktop unidentified", "桌面未识别");
        Add(entries, "Open Main Window", "打开主界面");
        Add(entries, "Text size", "文字大小");
        Add(entries, "Applies to text in all windows and menus. Saved automatically.", "调整所有窗口和菜单的文字大小，自动保存。");
        Add(entries, "Previous image", "上一张");
        Add(entries, "Next image", "下一张");
        Add(entries, "Image {0} of {1}", "第 {0} 张，共 {1} 张");
        Add(entries, "Restore Workflow", "恢复流程");
        Add(entries, "Workspace actions", "工作区操作");
        Add(entries, "Select workspace", "选择工作区");
        Add(entries, "More restore options", "更多恢复选项");
        Add(entries, "Other browsers and connection management", "其他浏览器与连接管理");
        Add(entries, "Use the installed extension to save and restore browser tabs", "使用已安装的扩展保存和恢复浏览器标签页");
        Add(entries, "Saved screen previews", "已保存的屏幕截图");
        Add(entries, "No screen preview is available for this saved workspace. Save a new workspace to capture monitors.", "此工作区没有可用的屏幕截图。新建工作区时会采集显示器画面。");
        Add(entries, "Hide screen previews", "隐藏屏幕截图");
        Add(entries, "Show screen previews", "显示屏幕截图");
        Add(entries, "Screen {0}", "显示器 {0}");
        Add(entries, "Workspace: {0}", "工作区：{0}");
        Add(entries, "Operation: {0}", "操作类型：{0}");
        Add(entries, "Not recorded", "未记录");
        Add(entries, "Workspace saved", "保存工作区");
        Add(entries, "Saved content", "保存的内容");
        Add(entries, "Desktop captured before this operation", "操作前的桌面快照");
        Add(entries, "The original snapshot is no longer available. Only recorded execution results are shown.", "原始快照已不可用，仅展示已记录的执行结果。");
        Add(entries, "Browser tabs: {0} · Explorer tabs: {1} · Terminal tabs: {2}", "浏览器标签页：{0} · 资源管理器标签页：{1} · 终端标签页：{2}");
        Add(entries, "Virtual desktops: {0} · File/folder capture: {1}", "虚拟桌面：{0} · 文件/文件夹采集：{1}");
        Add(entries, "Applications in snapshot", "快照中的应用");
        Add(entries, "{0}: {1} window(s)", "{0}：{1} 个窗口");
        Add(entries, "No windows in this snapshot.", "此快照中没有窗口。");
        Add(entries, "Monitor configuration", "显示器配置");
        Add(entries, "Monitor configuration was not recorded.", "未记录显示器配置。");
        Add(entries, "Monitor {0}: {1} · {2} × {3} · {4}% · {5} window(s){6}", "显示器 {0}：{1} · {2} × {3} · {4}% · {5} 个窗口{6}");
        Add(entries, " · Primary", " · 主显示器");
        Add(entries, "Record identifiers", "记录标识");
        Add(entries, "Select all restore entries", "全选恢复条目");
        Add(entries, "Unassigned monitor: {0} window(s)", "显示器未识别：{0} 个窗口");
        Add(entries, "Workspace ID: {0}", "工作区标识：{0}");
        Add(entries, "Checkpoint ID: {0}", "检查点标识：{0}");
        Add(entries, "Operation ID: {0}", "操作标识：{0}");
    }
}
