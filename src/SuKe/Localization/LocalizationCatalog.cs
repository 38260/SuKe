using System;
using System.Collections.Generic;

namespace SuKe.Localization;

/// <summary>
/// English to Chinese string catalog for the SuKe user interface.
/// </summary>
/// <remarks>
/// <para>
/// Keys are the exact English source strings; values are the Chinese replacements. Keys are
/// compared with <see cref="StringComparer.Ordinal"/> so punctuation, dashes, and curly quotes
/// must match the source exactly.
/// </para>
/// <para>
/// Strings that are user data, machine identifiers, registry paths, log event identifiers, or
/// protocol constants deliberately have no entry. A missing entry is not an error: the original
/// English text is shown unchanged.
/// </para>
/// </remarks>
internal static partial class LocalizationCatalog
{
    /// <summary>Immutable lookup table used by <see cref="Loc"/>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Entries = Build();

    private static Dictionary<string, string> Build()
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        AddTray(entries);
        AddSettingsWindow(entries);
        AddHelpWindow(entries);
        AddSaveWorkspaceDialog(entries);
        AddRestorePreviewDialog(entries);
        AddDialogs(entries);
        AddCodeMessages(entries);
        AddRestorePlanPresentation(entries);
        AddPlanExplanations(entries);
        AddServiceMessages(entries);
        AddCoordinatorMessages(entries);
        AddReference(entries);
        AddShell(entries);
        return entries;
    }

    // ── System tray (App.xaml) ────────────────────────────────────────────────────────────────

    private static void AddTray(Dictionary<string, string> entries)
    {
        Add(entries, "SuKe — Active", "SuKe — 运行中");
        Add(entries, "Workspaces", "工作区");
        Add(entries, "Undo Last Restore", "撤销上次恢复");
        Add(entries, "Restore Returned Display Layout", "恢复已返回的显示布局");
        Add(entries, "Dismiss Display Recovery", "忽略显示恢复");
        Add(entries, "Copy Last Restore Diagnostics", "复制上次恢复诊断");
        Add(entries, "Rescue Active Window", "救援活动窗口");
        Add(entries, "Move the selected off-screen window into a reachable monitor work area.",
            "把选中的离屏窗口移入可达的显示器工作区。");
        Add(entries, "Settings...", "设置…");
        Add(entries, "Help & Guide...", "帮助与指南…");
        Add(entries, "Exit", "退出");
        Add(entries, "Restore: {0}", "恢复：{0}");
        Add(entries, "Switch to: {0}", "切换到：{0}");
        Add(entries, "Align + minimize others: {0}", "对齐并最小化其他窗口：{0}");
        Add(entries, "Restore Returned Layout: {0}", "恢复已返回的布局：{0}");
        Add(entries, "Save Current Workspace...", "保存当前工作区…");
        Add(entries, "Manage Workspaces", "管理工作区");
        Add(entries, "(no saved workspaces)", "（没有已保存的工作区）");
    }

    // ── Settings window (SettingsWindow.xaml) ─────────────────────────────────────────────────

    private static void AddSettingsWindow(Dictionary<string, string> entries)
    {
        Add(entries, "SuKe — Settings", "SuKe — 设置");
        Add(entries, "System", "系统");
        Add(entries, "Startup", "启动");
        Add(entries, "Monitors", "显示器");
        Add(entries, "Help & Guide", "帮助与指南");
        Add(entries, "Workspaces", "工作区");

        Add(entries, "Need help choosing a restore mode?", "不确定该选哪种恢复模式？");
        Add(entries,
            "Open the permanent in-app guide for setup, modes, policies, matching, recovery, and privacy.",
            "打开应用内常驻指南：配置、恢复模式、策略、匹配、恢复与隐私。");

        Add(entries, "Start with Windows", "随 Windows 启动");
        Add(entries, "Launch SuKe automatically when you log in", "登录时自动启动 SuKe");
        Add(entries, "SuKe notifications", "SuKe 通知");
        Add(entries, "Show workspace progress and completion messages in the system tray",
            "在系统托盘中显示工作区进度与完成消息");
        Add(entries, "Choose what happens when SuKe starts", "选择 SuKe 启动时的行为");
        Add(entries, "On startup", "启动时");

        Add(entries, "Browser Integration", "浏览器集成");
        Add(entries, "Browser connection", "浏览器连接");
        Add(entries, "Enable browser tab capture and restoration", "启用浏览器标签页的采集与恢复");
        Add(entries,
            "Install the extension from the browser store. Developer Mode is only needed for local testing.",
            "扩展需要从浏览器商店安装。开发者模式仅用于本地测试。");
        Add(entries,
            "The extension must be installed by your browser. SuKe can open the correct setup page and remove its native connection.",
            "扩展必须由你的浏览器来安装。SuKe 可以打开正确的配置页面，并移除其原生连接。");
        Add(entries, "Set up Chrome", "配置 Chrome");
        Add(entries, "Set up Edge", "配置 Edge");
        Add(entries, "Set up Brave", "配置 Brave");
        Add(entries, "Set up Opera", "配置 Opera");
        Add(entries, "Set up Firefox", "配置 Firefox");
        Add(entries, "Remove browser connection", "移除浏览器连接");
        Add(entries, "Matching browser tabs", "浏览器标签页匹配");
        Add(entries,
            "Choose whether a matching tab in the same browser profile is reused, reopened, or left for review.",
            "选择当同一浏览器配置文件中存在匹配标签页时，是复用它、重新打开，还是留待你复核。");
        Add(entries, "Reuse matching tab", "复用匹配的标签页");
        Add(entries, "Always reopen", "总是重新打开");
        Add(entries, "Ask before reuse", "复用前询问");

        Add(entries, "Path Portability", "路径可移植性");
        Add(entries, "Logical path aliases", "逻辑路径别名");
        Add(entries, "Alias name, for example PROJECTS", "别名名称，例如 PROJECTS");
        Add(entries, "Absolute local directory", "本地绝对目录");
        Add(entries, "Save", "保存");
        Add(entries, "Remove", "移除");

        Add(entries, "Window Matching", "窗口匹配");
        Add(entries, "Remembered window choices", "记住的窗口选择");
        Add(entries, "Remembered choices", "记住的选择");
        Add(entries, "Clear remembered choices", "清除记住的选择");
        Add(entries,
            "A remembered ambiguity choice is scoped to a stable workspace and entry ID and stores composite application identity—not a temporary process ID or window handle. Clear remembered choices from Settings whenever an application’s windows change purpose.",
            "记住的歧义选择以稳定的工作区 ID 与条目 ID 为作用域，存储的是复合应用身份——而不是临时的进程 ID 或窗口 handle。当某个应用的窗口用途发生变化时，请到设置中清除记住的选择。");
        Add(entries, "Keep open during workspace switches", "工作区切换期间保持打开");
        Add(entries, "No apps are currently preserved.", "当前没有保留任何应用。");
        Add(entries, "Add open app", "添加已打开的应用");
        Add(entries, "Packaged app", "打包应用");
        Add(entries, "Application", "应用");

        Add(entries, "Restore Workflow", "恢复工作流");
        Add(entries, "Create recovery checkpoints", "创建恢复检查点");
        Add(entries, "Checkpoint routine restores", "为常规恢复创建检查点");
        Add(entries,
            "Capture the current desktop before routine restores so they create a new undo point. Workspace switches and Undo always keep their recovery checkpoint.",
            "在常规恢复之前先采集当前桌面，使其产生一个新的撤销点。工作区切换与撤销始终保留其恢复检查点。");
        Add(entries, "Show Restore Preview", "显示恢复预览");
        Add(entries,
            "Review matches before manual restores and switches. A choice dialog still appears when ambiguity requires input.",
            "手动恢复与切换前先复核匹配结果。当存在歧义必须由你决定时，仍会弹出选择对话框。");
        Add(entries, "Temporary display recovery", "临时显示恢复");
        Add(entries,
            "When a temporary resolution, docking, or monitor change returns to a saved layout, ask before restoring it, restore it automatically, or do nothing.",
            "当临时的分辨率、扩展坞或显示器变化恢复到某个已保存布局时，可以选择恢复前先询问、自动恢复，或不做任何操作。");
        Add(entries, "Ask before restoring", "恢复前询问");
        Add(entries, "Restore automatically", "自动恢复");
        Add(entries, "Do nothing", "不做任何操作");
        Add(entries, "Rescue active window", "救援活动窗口");
        Add(entries, "Window rescue visibility threshold", "窗口救援可见面积阈值");
        Add(entries, "Rescue Active Window only moves the active window when less than this area is reachable.",
            "仅当活动窗口在屏可达面积小于此比例时，「救援活动窗口」才会移动它。");

        Add(entries, "Default workspace", "默认工作区");
        Add(entries, "Keyboard Shortcuts", "键盘快捷键");
        Add(entries, "Enable global keyboard shortcuts", "启用全局键盘快捷键");
        Add(entries, "Control SuKe from anywhere with hotkeys", "用热键在任何位置控制 SuKe");
        Add(entries, "Saved Workspaces", "已保存的工作区");
        Add(entries, "Saved", "已保存");
        Add(entries, "Name", "名称");
        Add(entries, "Change", "更改");
        Add(entries, "Reset", "重置");
        Add(entries, "Disabled", "已禁用");
        Add(entries, "Rename monitor", "重命名显示器");
        Add(entries, "Give your monitors custom names to easily identify them", "给显示器起自定义名称，便于识别");
        Add(entries, "Import Workspace", "导入工作区");
        Add(entries, "Import a local workspace copy", "导入本地工作区副本");
        Add(entries, "Preview the file first; matching names or IDs are always imported as a separate copy",
            "先预览文件；名称或 ID 相同的条目一律作为独立副本导入");
        Add(entries, "Delete selected", "删除所选");
        Add(entries, "Select all workspaces", "全选工作区");
        Add(entries, "Clear selection", "清除选择");
        Add(entries, "Save Workspace", "保存工作区");
        Add(entries, "Save current workspace", "保存当前工作区");
        Add(entries, "Save my first workspace", "保存我的第一个工作区");
        Add(entries, "Choose monitors and open-file options, then save", "选择显示器与打开文件的选项，然后保存");
        Add(entries, "Save open files", "保存打开的文件");
        Add(entries, "Save documents, editor workspaces, Explorer tabs, and Terminal tabs",
            "保存文档、编辑器工作区、资源管理器标签页与终端标签页");
        Add(entries, "File Explorer tabs", "文件资源管理器标签页");
        Add(entries, "VS Code workspaces", "VS Code 工作区");
        Add(entries, "Configure Terminal tabs", "配置终端标签页");
        Add(entries, "Windows to Save", "要保存的窗口");
        Add(entries, "No saved workspaces yet. Save your first workspace above.",
            "还没有保存的工作区。请在上方保存第一个工作区。");
        Add(entries, "Close", "关闭");
        Add(entries, "Cancel", "取消");
        Add(entries, "Open Settings", "打开设置");
        Add(entries, "All", "全部");
        Add(entries, "None", "无");
        Add(entries, "Windows", "窗口");
        Add(entries, "Files", "文件");

        Add(entries, "Getting started", "快速上手");
        Add(entries, "Workspace Name", "工作区名称");
        Add(entries, "e.g. Work — triple monitor", "例如：工作 — 三屏");
        Add(entries, "FIRST RUN", "首次运行");
        Add(entries, "SuKe lives in the notification area", "SuKe 常驻通知区域");
        Add(entries, "On startup", "启动时");
        Add(entries, "Restore default workspace", "恢复默认工作区");
        Add(entries, "Restore last-used workspace", "恢复上次使用的工作区");
        Add(entries, "Ask me which workspace", "让我选择工作区");
        Add(entries, "Don’t restore any workspace", "不恢复任何工作区");
        Add(entries, "Don't restore any workspace", "不恢复任何工作区");
        Add(entries,
            "When a temporary resolution, docking, or monitor change returns to a saved layout, ask before restoring it, restore it automatically, or do nothing. Automatic recovery always saves an undo checkpoint and pauses for fullscreen apps.",
            "当临时分辨率、扩展坞或显示器变化恢复到某个已保存布局时，可以选择先询问再恢复、自动恢复，或什么都不做。自动恢复总会保存一个撤销检查点，并在全屏应用运行时暂停。");
        Add(entries, "The workspace to restore automatically on startup", "启动时自动恢复的工作区");
        Add(entries, "Restore modes", "恢复模式");
        Add(entries, "Restore Workflow", "恢复工作流");
        Add(entries, "What SuKe does", "SuKe 会做什么");

        // Hotkey action names (shown through Settings bindings, so translated at the source).
        Add(entries, "Quick Save", "快速保存");
        Add(entries, "Restore Default", "恢复默认工作区");
        Add(entries, "Restore Workspace 1", "恢复工作区 1");
        Add(entries, "Restore Workspace 2", "恢复工作区 2");
        Add(entries, "Restore Workspace 3", "恢复工作区 3");
        Add(entries, "Switch to Workspace 1", "切换到工作区 1");
        Add(entries, "Switch to Workspace 2", "切换到工作区 2");
        Add(entries, "Switch to Workspace 3", "切换到工作区 3");
        Add(entries, "Switch to Default", "切换到默认工作区");
    }

    // ── Help & Guide window (HelpReferenceWindow.xaml) ────────────────────────────────────────

    private static void AddHelpWindow(Dictionary<string, string> entries)
    {
        Add(entries, "SuKe — Help & Guide", "SuKe — 帮助与指南");
        Add(entries, "SuKe Help & Reference", "SuKe 帮助与参考");
        Add(entries, "Welcome to SuKe", "欢迎使用 SuKe");
        Add(entries,
            "SuKe runs in the notification area. Start here, then reopen this guide from the tray or Settings whenever you need it.",
            "SuKe 常驻在通知区域。可以从这里开始；之后随时通过托盘或设置重新打开本指南。");
        Add(entries, "Dismiss", "关闭");
        Add(entries,
            "Everything needed to save, restore, and troubleshoot a workspace—without leaving the app.",
            "保存、恢复与排查工作区所需的一切——无需离开应用。");
        Add(entries, "1. Arrange and save", "1. 摆放并保存");
        Add(entries, "2. Restore or switch", "2. 恢复或切换");
        Add(entries, "3. Review, execute, and verify", "3. 复核、执行并校验");
        Add(entries,
            "Arrange the windows you want, choose Save current workspace, select the relevant monitors and windows, and give the workspace a name. File and folder detection is optional; enable it when reopening documents matters more than the extra capture work.",
            "把想要的窗口摆放好，点击「保存当前工作区」，勾选相关显示器与窗口，并为工作区命名。文件与文件夹检测是可选项；如果你更看重日后能重新打开文档、而不是采集时的额外耗时，可以开启它。");
        Add(entries, "Restore modes", "恢复模式");
        Add(entries, "Policies & options", "策略与选项");
        Add(entries, "Per-window restore policies", "逐窗口恢复策略");
        Add(entries, "Startup, notifications, hotkeys, and monitors", "启动、通知、热键与显示器");
        Add(entries, "Matching & privacy", "匹配与隐私");
        Add(entries, "Updates and changing monitors", "更新与显示器变化");
        Add(entries, "How windows are identified", "窗口如何被识别");
        Add(entries, "Important limits", "重要限制");
        Add(entries, "Local data and diagnostics", "本地数据与诊断");
        Add(entries, "Recovery checkpoints and Undo", "恢复检查点与撤销");

        Add(entries, "Resume", "续用");
        Add(entries, "Repair", "修复");
        Add(entries, "Move Existing", "移动已存在窗口");
        Add(entries, "Launch Fresh", "全新启动");
        Add(entries, "Exact Switch", "精确切换");
        Add(entries, "Preview Only", "仅预览");
        Add(entries,
            "Best general-purpose choice. Reuse confidently matched windows, launch missing entries when possible, and apply their saved placement.",
            "最稳妥的通用选择。复用可确信匹配的窗口，尽可能启动缺失的条目，并应用其保存的位置。");
        Add(entries,
            "Correct placement differences on windows that already exist. Missing applications are not launched.",
            "只修正已存在窗口的位置差异，不会启动缺失的应用。");
        Add(entries,
            "Move matched live windows to their saved targets and never launch missing applications. Useful when you only want to rearrange what is already open.",
            "把匹配的活动窗口移到其保存的目标位置，绝不启动缺失的应用。适合只想重新排布已打开窗口的场景。");
        Add(entries,
            "Prefer a distinct new instance when the application has a reliable launch contract. When it cannot safely create another instance, SuKe reports the limitation and chooses the safest supported behavior.",
            "当应用具备可靠的启动契约时，优先创建独立的新实例。若无法安全地再开一个实例，SuKe 会说明该限制，并选择最安全的受支持行为。");
        Add(entries,
            "Gracefully close unrelated user-facing task windows, preserve destination and protected entries, then resume the target workspace. Review the plan before using this mode with unsaved work.",
            "优雅地关闭无关的面向用户的任务窗口，保留目标条目与受保护条目，然后恢复目标工作区。存在未保存的工作时，请先复核计划再使用此模式。");
        Add(entries,
            "Inspect matches, launch requirements, monitor mapping, warnings, and planned actions without crossing a mutation boundary.",
            "在不触发任何变更的前提下，查看匹配结果、启动需求、显示器映射、警告与计划操作。");
        Add(entries,
            "Restore reuses suitable windows and recreates missing entries according to the workspace mode. Exact Switch additionally asks unrelated task windows to close. Align + minimize others restores the workspace and minimizes other windows without closing them.",
            "恢复会按工作区模式复用合适的窗口并重建缺失的条目。精确切换还会请求关闭无关的任务窗口。「对齐并最小化其他窗口」会恢复工作区，并把其他窗口最小化而不关闭。");
        Add(entries,
            "Selective Restore limits Resume to chosen entries or monitors. Align + minimize restores the workspace and minimizes unrelated windows; it does not perform an Exact Switch.",
            "「选择性恢复」把「续用」限制在你选中的条目或显示器上。「对齐并最小化」会恢复工作区并最小化无关窗口；它不会执行精确切换。");
        Add(entries, "Selective and Align + minimize", "选择性恢复与「对齐并最小化」");

        Add(entries,
            "Workspace Default follows the selected mode. Reuse Existing prefers a current match. Launch If Missing may recreate an absent entry. Always Launch New requests a separate instance. Never Launch only reuses. Never Close protects a matching window during Exact Switch. Ignore During Switch neither restores nor closes that entry during a switch.",
            "「工作区默认」跟随所选模式。「复用已存在」优先使用当前匹配。「缺失则启动」可重建不存在的条目。「总是新开」请求一个独立实例。「从不启动」只做复用。「从不关闭」在精确切换期间保护匹配窗口。「切换期间忽略」在切换时既不恢复也不关闭该条目。");

        Add(entries,
            "Startup controls whether nothing, the default workspace, the most recently saved workspace, or a chooser runs after launch. Notifications affect tray messages, not this guide. Hotkeys can restore or switch the default and first three ordered workspaces. Monitor aliases change display names without changing stable monitor identity.",
            "「启动」决定启动后执行什么：什么都不做、恢复默认工作区、恢复最近保存的工作区，还是弹出选择器。通知设置只影响托盘消息，不影响本指南。热键可以恢复或切换默认工作区以及排序最前的三个工作区。显示器别名只改变显示名称，不改变稳定的显示器身份。");

        Add(entries,
            "The optional Chromium and Firefox connectors add metadata-based tab/session capture. Private windows, cookies, passwords, general history, page bodies, and Firefox container identity are excluded. Dedicated URL patterns intentionally read matching address bars so selected sites can reopen as separate windows; leave the list empty if you do not need that behavior.",
            "可选的 Chromium 与 Firefox 连接器提供基于元数据的标签页/会话采集。隐私窗口、Cookie、密码、常规历史、页面正文与 Firefox 容器身份都被排除在外。专用 URL 模式会有意读取匹配的地址栏，以便把选中的站点作为独立窗口重新打开；若不需要该行为，请把列表留空。");
        Add(entries,
            "SuKe combines executable or package identity, window class, document/folder/browser evidence, title similarity, process evidence, and weak geometry context. It assigns one live window to at most one saved entry. Low-confidence or tied candidates are shown for selection instead of being guessed.",
            "SuKe 综合可执行文件或包身份、窗口类名、文档/文件夹/浏览器证据、标题相似度、进程证据与弱几何上下文。它把一个活动窗口最多分配给一个保存条目。置信度低或并列的候选会被列出来供你选择，而不是靠猜。");
        Add(entries,
            "SuKe inventories the desktop, matches each saved entry, builds a plan, optionally asks for approval, executes approved actions, and verifies final placement. Ambiguous or blocked entries still require attention even when routine previews are disabled.",
            "SuKe 会盘点桌面、逐条匹配保存的条目、生成计划、可选地请求批准、执行已批准的操作，并校验最终位置。即使关闭了常规预览，存在歧义或被阻断的条目依然需要你处理。");
        Add(entries,
            "When enabled, manual restores show the planned matches and actions before execution. When disabled, routine executable plans continue immediately, but ambiguity and blocking errors still open the review dialog because SuKe needs a safe decision.",
            "启用后，手动恢复会在执行前显示计划中的匹配与动作。禁用后，可执行的常规计划会直接继续，但歧义与阻断错误仍会打开复核对话框，因为 SuKe 需要你做出安全决定。");
        Add(entries,
            "When enabled, SuKe captures a local metadata-only recovery point before restore mutations. Disabling checkpoints reduces work but new operations cannot create an Undo point. Undo uses the normal matching and restore pipeline; applications may still refuse to close, launch, or move.",
            "启用后，SuKe 会在恢复变更之前采集一个仅含元数据的本地恢复点。禁用检查点可以减少开销，但新操作将无法创建撤销点。撤销走的是常规匹配与恢复流水线；应用仍可能拒绝关闭、启动或移动。");

        Add(entries,
            "Store applications use stable package activation identities. Recognized Squirrel applications with versioned app-* folders can resolve to the newest installed executable. When the saved monitor is unavailable, semantic or normalized layout is mapped deterministically onto a visible monitor and may be clamped to its work area.",
            "商店应用使用稳定的包激活身份。带有版本化 app-* 目录的、可识别的 Squirrel 应用可以解析到最新安装的可执行文件。当保存的显示器不可用时，语义化或归一化布局会被确定性地映射到某台可见显示器上，并可能被钳制到其工作区内。");
        Add(entries,
            "When file detection identifies a `.code-workspace` file or a project folder, SuKe saves that workspace context separately from an open document. Restore opens it through VS Code in a new or assigned editor window according to the entry policy. A title alone is not treated as a workspace path; Cursor continues to use its normal registered-file handler.",
            "当文件检测识别出 `.code-workspace` 文件或项目文件夹时，SuKe 会把它作为工作区上下文单独保存，而不与打开的文档混在一起。恢复时会按条目策略在新建或已分配的编辑器窗口中通过 VS Code 打开它。仅凭标题不会被当作工作区路径；Cursor 继续使用其常规的注册文件处理器。");

        Add(entries,
            "Use the tray's Rescue Active Window command after selecting a window that is partly or fully off-screen. It moves only that foreground window into the nearest reachable monitor work area when less than the configured visible-area threshold remains on screen. It does not restore a workspace, reopen applications, change other windows, or unmaximize a maximized window.",
            "当某个窗口部分或完全离屏时，选中它并使用托盘的「救援活动窗口」命令。仅当该前台窗口在屏可见面积低于配置阈值时，它才会把这一个窗口移到最近的可达显示器工作区。它不会恢复工作区、不会重新打开应用、不会改动其他窗口，也不会把最大化窗口还原。");
        Add(entries,
            "SuKe cannot guarantee that another application has saved its documents, that every app supports multiple instances, or that an app will accept a requested close or position. Review destructive plans, save important work first, and use the progress and result details when an entry needs attention.",
            "SuKe 无法保证其他应用已经保存了文档、每个应用都支持多实例，或某个应用一定会接受关闭/定位请求。请复核具破坏性的计划、先保存重要工作，并在某个条目需要处理时查看进度与结果详情。");
        Add(entries,
            "Workspaces, settings, learned matches, and checkpoints are stored locally in versioned JSON. Diagnostic output uses structured sensitivity labels and redaction. SuKe does not store screenshots, browser page contents, cookies, credentials, or runtime HWND/PID values as learned identity.",
            "工作区、设置、学习到的匹配与检查点都以带版本号的 JSON 保存在本地。诊断输出使用结构化的敏感等级标记与脱敏。SuKe 不会存储截图、浏览器页面内容、Cookie、凭据，也不会把运行时的 HWND/PID 存为学习身份。");
        Add(entries,
            "Closing Settings or this guide does not exit SuKe. Look near the Windows clock for the application icon. Right-click it to save, restore, switch, undo, open Settings, or return to this guide.",
            "关闭设置或本指南并不会退出 SuKe。请到 Windows 时钟附近找到应用图标：右键即可保存、恢复、切换、撤销、打开设置，或重新回到本指南。");
        Add(entries,
            "On Windows 11, enabling Save open files also records every folder tab in each selected File Explorer window and which tab was active. Restore adds only missing saved tabs to the assigned Explorer window, leaves unrelated tabs open, and reselects the saved active tab. Missing folders are reported as a partial restore instead of closing tabs or guessing another location.",
            "在 Windows 11 上，启用「保存打开的文件」还会记录每个被选中的资源管理器窗口中的所有文件夹标签页，以及当时激活的是哪一个。恢复时只向被分配的资源管理器窗口补齐缺失的已保存标签页，保留无关标签页，并重新选中保存时的激活项。若文件夹缺失，会报告为部分恢复，而不会去关闭标签页或猜测另一个位置。");
    }

    // ── Save workspace dialog (SaveWorkspaceDialog.xaml) ──────────────────────────────────────

    private static void AddSaveWorkspaceDialog(Dictionary<string, string> entries)
    {
        Add(entries, "SuKe — Save Workspace", "SuKe — 保存工作区");
        Add(entries, "Save Workspace", "保存工作区");
        Add(entries, "Windows to Save", "要保存的窗口");
        Add(entries, "Save open files", "保存打开的文件");
        Add(entries, "Save documents, editor workspaces, Explorer tabs, and Terminal tabs",
            "保存文档、编辑器工作区、资源管理器标签页与终端标签页");
        Add(entries, "Configure Terminal tabs", "配置终端标签页");
        Add(entries, "Workspace Name", "工作区名称");
    }

    // ── Dialogs built in code (UI/*.cs) ──────────────────────────────────────────────────────

    // ── Restore preview dialog (grouped-by-application redesign) ──────────────────────────────

    private static void AddRestorePreviewDialog(Dictionary<string, string> entries)
    {
        Add(entries, "Restore \"{0}\"", "恢复「{0}」");
        Add(entries, "{0} apps · {1} windows", "{0} 个应用 · {1} 个窗口");
        Add(entries, "{0} applications, {1} saved windows", "{0} 个应用，{1} 个已保存窗口");
        Add(entries, "Restore plan preview for {0}", "{0} 的恢复计划预览");
        Add(entries, "Select all windows", "全选窗口");
        Add(entries, "Select all restore entries", "全选全部恢复条目");
        Add(entries, "Expand", "展开");
        Add(entries, "Expand or collapse {0}", "展开或收起 {0}");
        Add(entries, "Include every {0} window", "包含 {0} 的所有窗口");
        Add(entries, "{0} windows", "{0} 个窗口");
        Add(entries, "To restore", "待恢复");
        Add(entries, "Already running · skipped", "已运行 · 跳过");
        Add(entries, "Not restorable", "无法恢复");
        Add(entries, "Not restorable: {0}", "无法恢复：{0}");
        Add(entries, "Choose the matching window", "请选择对应窗口");
        Add(entries, "Cleared", "已取消");
        Add(entries, "Not selected", "未选择");
        Add(entries, "Restore {0} · skip {1}", "恢复 {0} 个 · 跳过 {1} 个");
        Add(entries, "Restore {0} · {1} not restorable", "恢复 {0} 个 · {1} 个无法恢复");
        Add(entries, "Selected {0} windows · restoring {1} · skipping {2} already running",
            "已选 {0} 个窗口 · 将恢复 {1} 个 · 已运行跳过 {2} 个");
        Add(entries, "Selected {0} windows", "已选 {0} 个窗口");
        Add(entries, "Restoring {0} · skipping {1} already running", "将恢复 {0} 个 · 已运行跳过 {1} 个");
        Add(entries, "Restore {0} windows", "恢复 {0} 个窗口");
        Add(entries, "Switch {0} windows", "切换 {0} 个窗口");
        Add(entries, "Show match details", "显示匹配详情");
        Add(entries, "View details", "查看详情");
        Add(entries, "Target: {0}", "目标：{0}");
        Add(entries, "Saved resource is no longer available.", "保存的资源已不存在。");
        Add(entries, "Kept as it is; nothing will be launched, moved or reopened.",
            "保持现状：不启动、不移动、不重新打开。");
        Add(entries, "Preview only: no windows or browser sessions will be changed.",
            "仅预览：不会更改任何窗口或浏览器会话。");
        Add(entries, "{0} selected windows cannot be restored. Clear them to continue.",
            "已选的 {0} 个窗口无法恢复，取消勾选后才能继续。");
        Add(entries, "Unrelated open windows may ask to save before they close; SuKe never force-closes them.",
            "无关窗口可能弹出保存提示后才关闭；SuKe 不会强制关闭它们。");
        Add(entries, "Other open windows will be minimized. No window will be closed.",
            "其他窗口会被最小化，不会关闭任何窗口。");
        Add(entries, "Select {0}. {1}", "选择 {0}。{1}");
    }

    private static void AddDialogs(Dictionary<string, string> entries)
    {
        // Restore plan preview
        Add(entries, "preview", "预览");
        Add(entries, "switch", "切换");
        Add(entries, "restore", "恢复");
        Add(entries, "Review {0}: {1}{2}", "复核{0}：{1}{2}");
        Add(entries, "{0} plan preview for {1}", "{0}计划预览 — {1}");
        Add(entries, "Preview this workspace without making changes", "预览此工作区，不做任何更改");
        Add(entries, "Review the destination workspace", "复核目标工作区");
        Add(entries, "Review what SuKe will change", "复核 SuKe 将要做的更改");
        Add(entries,
            "Mode: {0} · {1} saved entries. Matching destination windows stay open; unrelated windows will be asked to close after approval. Clear an entry to exclude it.",
            "模式：{0} · {1} 个已保存条目。匹配上的目标窗口保持打开；无关窗口会在批准后收到关闭请求。取消勾选某个条目即可将其排除。");
        Add(entries, "Mode: {0} · {1} saved entries. Clear an entry to remove its actions from the approved plan.",
            "模式：{0} · {1} 个已保存条目。取消勾选某个条目即可把它的操作从已批准计划中移除。");
        Add(entries, "Preview only: no applications, windows, or browser sessions will be changed.",
            "仅预览：不会改动任何应用、窗口或浏览器会话。");
        Add(entries,
            "Switching can show save prompts for unrelated open windows; SuKe never force-closes them.",
            "切换时，无关的已打开窗口可能弹出保存提示；SuKe 绝不会强制关闭它们。");
        Add(entries, "Destructive action summary. {0}", "破坏性操作摘要。{0}");
        Add(entries, "Blocking errors must be resolved or their entries disabled before restore.",
            "阻断错误必须解决，或把相关条目禁用后才能恢复。");
        Add(entries, "Blocking errors are present. Disable affected entries before restoring.",
            "存在阻断错误。请先禁用受影响的条目再恢复。");
        Add(entries, "Restore plan entries", "恢复计划条目");
        Add(entries, "Cancel restore", "取消恢复");
        Add(entries, "Close preview", "关闭预览");
        Add(entries, "Switch to selected", "切换到所选");
        Add(entries, "Restore selected", "恢复所选");
        Add(entries, "Approve plan and switch to selected entries", "批准计划并切换到所选条目");
        Add(entries, "Approve plan and restore selected entries", "批准计划并恢复所选条目");
        Add(entries,
            "Preserves approved destination windows, closes unrelated windows safely, then executes this exact plan.",
            "保留已批准的目标窗口，安全关闭无关窗口，然后执行这份精确计划。");
        Add(entries, "Executes the exact actions shown in this preview after stale-plan validation.",
            "通过过期计划校验后，精确执行本预览中展示的操作。");
        Add(entries, "Include {0}", "包含 {0}");
        Add(entries, "Outcome: {0}", "结果：{0}");
        Add(entries, "Policy: {0}", "策略：{0}");
        Add(entries, "Choose the window that belongs to this entry:", "选择属于该条目的窗口：");
        Add(entries, "Selected match evidence:", "已选中匹配的证据：");
        Add(entries, "Select {0}. {1}. {2}. {3}", "选择 {0}。{1}。{2}。{3}");
        Add(entries, "Remember this choice for this workspace entry", "为此工作区条目记住该选择");
        Add(entries,
            "Uses stable workspace/entry IDs and a composite app identity; HWND and PID are never saved.",
            "使用稳定的工作区/条目 ID 与复合应用身份；绝不会保存 HWND 与 PID。");
        Add(entries,
            "These candidates do not have distinct stable composite identities, so this choice cannot be remembered safely.",
            "这些候选窗口没有彼此区分的稳定复合身份，因此无法安全地记住该选择。");
        Add(entries, "Remember the selected window match", "记住选中的窗口匹配");
        Add(entries, "Evidence: {0}", "证据：{0}");
        Add(entries, "• {0} — {1}", "• {0} — {1}");
        Add(entries, "Planned action. {0}. {1}", "计划操作。{0}。{1}");
        Add(entries, "Warning: {0}", "警告：{0}");
        Add(entries, "Blocking: {0}", "阻断：{0}");
        Add(entries, "Workspace action · {0}", "工作区操作 · {0}");
        Add(entries, "Workspace action. {0}. {1}", "工作区操作。{0}。{1}");
        Add(entries, "{0} entries · preview only", "{0} 个条目 · 仅预览");
        Add(entries, "{0} selected · {1} blocking error(s)", "{0} 项已选 · {1} 个阻断错误");
        Add(entries, "{0} selected · ready to {1}", "{0} 项已选 · 可以{1}了");

        // Diagnostics dialog
        Add(entries, "Restore needs attention", "恢复需要处理");
        Add(entries, "Learn more about SuKe", "了解 SuKe 的更多信息");
        Add(entries, "Copy diagnostics", "复制诊断");
        Add(entries, "Diagnostics copied.", "诊断已复制。");
        Add(entries, "SuKe could not copy diagnostics.", "SuKe 无法复制诊断。");
        Add(entries, "Entry {0}: {1} ({2}){3}{4}", "条目 {0}：{1}（{2}）{3}{4}");
        Add(entries,
            "The restore reported an incomplete result. Copy diagnostics for the structured details.",
            "恢复报告了不完整的结果。复制诊断以查看结构化详情。");

        // Progress windows
        Add(entries, "Switching Workspace…", "正在切换工作区…");
        Add(entries, "Restoring Workspace…", "正在恢复工作区…");
        Add(entries, "Switching Workspace", "切换工作区");
        Add(entries, "Restoring Workspace", "恢复工作区");
        Add(entries, "Switching to", "正在切换到");
        Add(entries, "Restoring", "正在恢复");
        Add(entries, "{0}“{1}”…", "{0}“{1}”…");
        Add(entries, "Creating recovery checkpoint", "正在创建恢复检查点");
        Add(entries, "Capturing the current desktop before making changes.",
            "正在做出更改之前采集当前桌面。");
        Add(entries, "Cancelling safely…", "正在安全地取消…");
        Add(entries, "SuKe will stop before the next operation boundary.",
            "SuKe 会在下一个操作边界之前停止。");
        Add(entries, "Elapsed {0}", "已用 {0}");
        Add(entries, "{0} · limit {1}", "{0} · 上限 {1}");
        Add(entries, "Saving Workspace…", "正在保存工作区…");
        Add(entries, "Saving Workspace", "保存工作区");
        Add(entries, "Saving “{0}”…", "正在保存“{0}”…");
        Add(entries, "Building file detection cache…", "正在构建文件检测缓存…");

        // Selective restore
        Add(entries, "Restore: {0}", "恢复：{0}");
        Add(entries,
            "⚠️  The current monitor configuration doesn’t match this workspace. Windows may restore to unexpected positions.",
            "⚠️  当前显示器配置与该工作区不匹配。窗口可能恢复到非预期的位置。");
        Add(entries, "Choose which monitors to restore:", "选择要恢复的显示器：");
        Add(entries, "Primary", "主显示器");
        Add(entries, "{0}{1}  —  {2} window", "{0}{1}  —  {2} 个窗口");
        Add(entries, "{0}{1}  —  {2} windows", "{0}{1}  —  {2} 个窗口");
        Add(entries, "Restore Selected", "恢复所选");

        // Startup chooser
        Add(entries, "SuKe — Restore Workspace", "SuKe — 恢复工作区");
        Add(entries, "Restore Workspace", "恢复工作区");
        Add(entries, "Choose a workspace to restore:", "选择要恢复的工作区：");
        Add(entries, "{0} windows  •  saved {1}", "{0} 个窗口  •  保存于 {1}");
        Add(entries, "Skip", "跳过");
        Add(entries, "Restore", "恢复");

        // Terminal tabs dialog
        Add(entries, "Windows Terminal tabs", "Windows 终端标签页");
        Add(entries,
            "Confirm each tab in left-to-right order. Tracked PowerShell tabs are prefilled. For other tabs, enter the Terminal profile name or GUID and its current directory. A blank profile uses Terminal's default.",
            "请按从左到右的顺序逐个确认标签页。已被追踪的 PowerShell 标签页已预填。其他标签页请填写 Terminal 配置文件名称或 GUID，以及其当前目录。配置文件留空则使用 Terminal 的默认值。");
        Add(entries, "Add tab", "添加标签页");
        Add(entries, "PowerShell setup", "PowerShell 配置");
        Add(entries, "Open the Windows Terminal integration section in the SuKe README.",
            "打开 SuKe README 中的 Windows 终端集成章节。");
        Add(entries, "Setup guide", "配置指南");
        Add(entries, "Remove selected", "移除所选");
        Add(entries, "Set active", "设为激活");
        Add(entries, "Use these tabs", "使用这些标签页");
        Add(entries, "Visible tab title (hint)", "可见标签页标题（提示）");
        Add(entries, "Profile name / GUID", "配置文件名称 / GUID");
        Add(entries, "Current directory", "当前目录");
        Add(entries,
            "Every tab needs an existing directory. Check the paths before saving. If you don't want to save Terminal tabs, turn off 'Save open files' instead.",
            "每个标签页都需要一个已存在的目录。保存前请检查路径。如果不想保存终端标签页，请改为关闭「保存打开的文件」。");
        Add(entries, "Terminal directory required", "需要终端目录");
        Add(entries, "Windows Terminal tabs — active tab {0}", "Windows 终端标签页 — 激活第 {0} 个");

        // Workspace windows dialog
        Add(entries, "{0} — Saved Windows", "{0} — 已保存的窗口");
        Add(entries, "Default restore mode", "默认恢复模式");
        Add(entries, "Used by tray actions and restore hotkeys. Exact switch can close unrelated windows.",
            "托盘操作与恢复热键会使用它。精确切换可以关闭无关窗口。");
        Add(entries, "Window Title", "窗口标题");
        Add(entries, "Restore Policy", "恢复策略");
        Add(entries,
            "Entry policies override the selected workspace mode. “Always launch new” falls back to a safe existing match when an app has no reliable multi-window launch contract.",
            "条目策略会覆盖所选的工作区模式。当应用没有可靠的多窗口启动契约时，「总是新开」会安全回退到已有的匹配窗口。");
        Add(entries, "No windows remaining in this workspace.", "此工作区中已没有窗口。");
        Add(entries, "Override restore behavior for this saved entry", "覆盖此已保存条目的恢复行为");
        Add(entries, "Remove from workspace", "从工作区中移除");
        Add(entries, "Workspace mode default", "工作区模式默认");
        Add(entries, "Workspace default", "工作区默认");
        Add(entries, "Reuse existing", "复用已存在");
        Add(entries, "Launch if missing", "缺失则启动");
        Add(entries, "Always launch new", "总是新开");
        Add(entries, "Never launch", "从不启动");
        Add(entries, "Never close", "从不关闭");
        Add(entries, "Ignore during switch", "切换期间忽略");
        Add(entries, "Move existing", "移动已存在窗口");
        Add(entries, "Launch fresh", "全新启动");
        Add(entries, "Exact switch", "精确切换");
        Add(entries, "Preview only", "仅预览");
        Add(entries, "Selective restore", "选择性恢复");
        Add(entries, "Align and minimize others", "对齐并最小化其他窗口");

        // Workspace transfer dialogs
        Add(entries, "Export Workspace", "导出工作区");
        Add(entries,
            "Create a local copy of “{0}”. Settings, credentials, cookies, document contents, and native browser-host data are never exported.",
            "为“{0}”创建一个本地副本。设置、凭据、Cookie、文档内容与浏览器原生 host 数据永远不会被导出。");
        Add(entries, "Exact backup — keep the selected local restore metadata",
            "精确备份 — 保留所选中的本地恢复元数据");
        Add(entries, "Portable / redacted — remove local absolute paths by default",
            "可移植/脱敏 — 默认移除本地绝对路径");
        Add(entries, "Include in this file", "包含在文件中的内容");
        Add(entries, "Layout and geometry", "布局与几何");
        Add(entries, "Monitor arrangement, placement, and saved window size.",
            "显示器排布、放置位置与已保存的窗口尺寸。");
        Add(entries, "Application identities", "应用身份");
        Add(entries, "Executable, process, class, and supported app metadata.",
            "可执行文件、进程、类名，以及受支持的应用元数据。");
        Add(entries, "Files and folders", "文件与文件夹");
        Add(entries, "Open document, Explorer, Terminal, and editor locations.",
            "打开的文档、资源管理器、终端与编辑器位置。");
        Add(entries, "Browser URLs and tabs", "浏览器 URL 与标签页");
        Add(entries, "Dedicated browser locations and connector tab sessions.",
            "专用浏览器位置与连接器标签页会话。");
        Add(entries, "Machine identifiers", "机器标识符");
        Add(entries, "Monitor, profile, and virtual-desktop identifiers.",
            "显示器、配置文件与虚拟桌面标识符。");
        Add(entries, "Logical path aliases", "逻辑路径别名");
        Add(entries, "Portable ${ALIAS} path forms, never the alias mapping itself.",
            "可移植的 ${ALIAS} 路径形式，绝不包含别名映射本身。");
        Add(entries,
            "The receiving device never receives your SuKe settings or alias roots; configure aliases there if needed.",
            "接收设备不会收到你的 SuKe 设置或别名根路径；如有需要请在该设备上配置别名。");
        Add(entries, "Choose destination", "选择目标位置");
        Add(entries, "Import Workspace", "导入工作区");
        Add(entries, "Exact backup", "精确备份");
        Add(entries, "Portable / redacted", "可移植/脱敏");
        Add(entries, "“{0}”\n{1} saved window · {2}", "“{0}”\n{1} 个已保存窗口 · {2}");
        Add(entries, "“{0}”\n{1} saved windows · {2}", "“{0}”\n{1} 个已保存窗口 · {2}");
        Add(entries, "This older export will be migrated to the current local schema before it is saved.",
            "这份较早的导出会在保存之前迁移到当前的本地 schema。");
        Add(entries, "Import as Copy", "作为副本导入");
        Add(entries, "The file is valid. Importing adds it as a new local workspace.",
            "文件有效。导入后它会成为一个新的本地工作区。");
        Add(entries, "A workspace with this name already exists. Importing creates a distinct copy with an “Imported” suffix.",
            "已存在同名工作区。导入会创建一个带「Imported」后缀的独立副本。");
        Add(entries, "A workspace with this stable ID already exists. Importing creates a new ID and keeps the existing workspace unchanged.",
            "已存在相同稳定 ID 的工作区。导入会生成新 ID，并保持既有工作区不变。");
        Add(entries,
            "A workspace with this name and stable ID already exists. Importing creates a new ID and an “Imported” name; the existing workspace is never overwritten.",
            "已存在同名且同稳定 ID 的工作区。导入会生成新 ID 与带「Imported」的名称；既有工作区绝不会被覆盖。");

        // Workspace rows and monitor labels
        Add(entries, "{0} workspace saved", "已保存 {0} 个工作区");
        Add(entries, "{0} workspaces saved", "已保存 {0} 个工作区");
        Add(entries, "{0} workspace selected", "已选择 {0} 个工作区");
        Add(entries, "{0} workspaces selected", "已选择 {0} 个工作区");
        Add(entries, "Delete {0} workspace?{1}{2}This cannot be undone.",
            "删除 {0} 个工作区？{1}{2}此操作无法撤销。");
        Add(entries, "Delete {0} workspaces?{1}{2}This cannot be undone.",
            "删除 {0} 个工作区？{1}{2}此操作无法撤销。");
        Add(entries, "• {0}", "• {0}");
        Add(entries, "• …and {0} more", "• …还有 {0} 个");
        Add(entries, "Delete Workspaces", "删除工作区");
        Add(entries, "Workspace Imported", "工作区已导入");
        Add(entries, "Workspace Import", "导入工作区");
        Add(entries, "Workspace Exported", "工作区已导出");
        Add(entries, "Workspace Export", "导出工作区");
        Add(entries, "Import SuKe Workspace", "导入 SuKe 工作区");
        Add(entries, "Export SuKe Workspace", "导出 SuKe 工作区");
        Add(entries, "SuKe workspace (*.suke.json)|*.suke.json|JSON files (*.json)|*.json",
            "SuKe 工作区 (*.suke.json)|*.suke.json|JSON 文件 (*.json)|*.json");
        Add(entries, "SuKe workspace (*.suke.json)|*.suke.json",
            "SuKe 工作区 (*.suke.json)|*.suke.json");
        Add(entries, "“{0}” was imported as a separate workspace.", "“{0}”已作为独立工作区导入。");
        Add(entries, "“{0}” was exported. Keep this file private if you included local paths or browser URLs.",
            "“{0}”已导出。如果其中包含本地路径或浏览器 URL，请把该文件保持私密。");
        Add(entries, "SuKe could not import that file. It was not added to your saved workspaces.\n\n{0}",
            "SuKe 无法导入该文件。它没有被加入你已保存的工作区。\n\n{0}");
        Add(entries, "SuKe could not write that export file.\n\n{0}",
            "SuKe 无法写入该导出文件。\n\n{0}");
        Add(entries, "Restore Workspace", "恢复工作区");
        Add(entries, "Restore As", "以指定模式恢复");
        Add(entries, "Switch to Workspace", "切换到工作区");
        Add(entries, "Restore Selected Monitors…", "恢复所选显示器…");
        Add(entries, "View & Edit Windows…", "查看并编辑窗口…");
        Add(entries, "Export…", "导出…");
        Add(entries, "Move Up", "上移");
        Add(entries, "Move Down", "下移");
        Add(entries, "Remove as Default", "取消默认");
        Add(entries, "Set as Default", "设为默认");
        Add(entries, "Rename…", "重命名…");
        Add(entries, "Delete", "删除");
        Add(entries,
            "Switch to “{0}”?\n\nYou will review destination matches and monitor adaptation first. Only unrelated windows will then be asked to close; apps with unsaved work can prompt you to save.\n\nThe approved workspace plan runs after those windows close.",
            "切换到“{0}”？\n\n你会先复核目标匹配与显示器适配情况。随后只有无关窗口会收到关闭请求；存在未保存工作的应用可能会提示你保存。\n\n已批准的工作区计划会在这些窗口关闭后执行。");
        Add(entries, "Switch Workspace", "切换工作区");
        Add(entries, "Yes", "是");
        Add(entries, "No", "否");
        Add(entries, "Current virtual desktop", "当前虚拟桌面");
        Add(entries, "Inactive virtual desktop {0}", "非活动虚拟桌面 {0}");
        Add(entries, "Observed virtual desktop {0}", "观测到的虚拟桌面 {0}");
        Add(entries, "Desktop unavailable", "桌面不可用");
        Add(entries, "{0}  ·  No capturable windows", "{0}  ·  没有可采集的窗口");
        Add(entries, "(Current)", "（当前）");
        Add(entries, "(Primary)", "（主显示器）");
        Add(entries, "Monitor {0}: {1}{2}  —  {3}×{4}  ({5} window)",
            "显示器 {0}：{1}{2}  —  {3}×{4}  （{5} 个窗口）");
        Add(entries, "Monitor {0}: {1}{2}  —  {3}×{4}  ({5} windows)",
            "显示器 {0}：{1}{2}  —  {3}×{4}  （{5} 个窗口）");
        Add(entries, "{0}  ({1} window)", "{0}  （{1} 个窗口）");
        Add(entries, "{0}  ({1} windows)", "{0}  （{1} 个窗口）");
        Add(entries, "{0} detected tab(s) · configure profiles and directories",
            "检测到 {0} 个标签页 · 请配置配置文件与目录");
        Add(entries, "Please enter a workspace name.", "请输入工作区名称。");
        Add(entries, "Name Required", "需要名称");
        Add(entries, "Please select at least one window to save.", "请至少选择一个要保存的窗口。");
        Add(entries, "No Windows Selected", "未选择窗口");
        Add(entries, "Press keys…", "请按下按键…");
        Add(entries, "No aliases configured. Workspace paths remain absolute on this device.",
            "未配置别名。工作区路径在此设备上保持为绝对路径。");
        Add(entries, "No remembered choices. Ambiguous matches will always ask before assignment.",
            "没有记住的选择。存在歧义的匹配总会在分配前询问你。");
        Add(entries, "{0} remembered choice. Hints use stable workspace/entry IDs and composite app identity; HWND/PID are never saved.",
            "{0} 条记住的选择。提示使用稳定的工作区/条目 ID 与复合应用身份；绝不会保存 HWND/PID。");
        Add(entries, "{0} remembered choices. Hints use stable workspace/entry IDs and composite app identity; HWND/PID are never saved.",
            "{0} 条记住的选择。提示使用稳定的工作区/条目 ID 与复合应用身份；绝不会保存 HWND/PID。");
        Add(entries, "Clear {0} remembered window choice? Future ambiguous restores will ask again.",
            "清除 {0} 条记住的窗口选择？之后遇到歧义恢复会重新询问。");
        Add(entries, "Clear {0} remembered window choices? Future ambiguous restores will ask again.",
            "清除 {0} 条记住的窗口选择？之后遇到歧义恢复会重新询问。");
        Add(entries, "Clear Remembered Window Choices", "清除记住的窗口选择");
        Add(entries,
            "No apps are globally preserved. Exact Switch will only keep destination and per-entry protected windows.",
            "没有全局保留的应用。精确切换只会保留目标窗口与逐条目受保护的窗口。");
        Add(entries, "{0} app will remain open during Exact Switch in every workspace.",
            "在精确切换时，{0} 个应用会在每个工作区中保持打开。");
        Add(entries, "{0} apps will remain open during Exact Switch in every workspace.",
            "在精确切换时，{0} 个应用会在每个工作区中保持打开。");
        Add(entries, "No additional eligible open applications were found.", "没有找到其他符合条件的已打开应用。");
        Add(entries, "Keep Open During Workspace Switches", "工作区切换期间保持打开");
        Add(entries, "Stop keeping {0} open", "不再保持 {0} 打开");
        Add(entries, "Logical Path Alias", "逻辑路径别名");
        Add(entries, "Aliases must start with a letter and use letters, digits, or underscores.",
            "别名的首字符必须是字母，之后只能包含字母、数字或下划线。");
        Add(entries, "Logical path aliases require an absolute local directory.",
            "逻辑路径别名需要一个本地绝对目录。");
        Add(entries,
            "Remove SuKe's native browser connection registrations? The browser extension itself will not be uninstalled.",
            "移除 SuKe 注册的浏览器原生连接？浏览器扩展本身不会被卸载。");
        Add(entries, "Remove Browser Connection", "移除浏览器连接");

        // Workflow errors
        Add(entries, "Undo Needs Attention", "撤销需要处理");
        Add(entries, "Switch Needs Attention", "切换需要处理");
        Add(entries, "Restore Needs Attention", "恢复需要处理");
        Add(entries, "SuKe could not prepare or execute the workspace switch.",
            "SuKe 无法准备或执行工作区切换。");
        Add(entries, "SuKe could not prepare or execute the restore preview.",
            "SuKe 无法准备或执行恢复预览。");
        Add(entries, "Restore Preview", "恢复预览");
        Add(entries, "Failed to save workspace: {0}", "保存工作区失败：{0}");
        Add(entries, "SuKe", "SuKe");
        Add(entries, "Previous desktop", "上一个桌面");
    }

    // ── Application shell messages (App.xaml.cs) ──────────────────────────────────────────────

    private static void AddCodeMessages(Dictionary<string, string> entries)
    {
        Add(entries, "A redacted diagnostic export was written to:{0}{1}",
            "已写入一份脱敏的诊断导出：{0}{1}");
        Add(entries, "SuKe Diagnostics", "SuKe 诊断");
        Add(entries, "SuKe could not create the diagnostic export.",
            "SuKe 无法创建诊断导出。");
        Add(entries, "Workspace Saved", "工作区已保存");
        Add(entries, "“{0}” saved — {1} window(s)", "“{0}”已保存 — {1} 个窗口");
        Add(entries, "Workspace Restored", "工作区已恢复");
        Add(entries, "“{0}” restored on startup", "启动时已恢复“{0}”");
        Add(entries, "Undo Failed", "撤销失败");
        Add(entries, "The previous desktop state could not be restored.", "无法恢复上一个桌面状态。");
        Add(entries, "Display Recovery Failed", "显示恢复失败");
        Add(entries, "The returned display layout could not be restored.", "无法恢复已返回的显示布局。");
        Add(entries, "Restore Diagnostics Copied", "恢复诊断已复制");
        Add(entries, "The latest redacted restore report is ready to paste.", "最新的脱敏恢复报告可以粘贴了。");
        Add(entries, "Diagnostics Not Copied", "诊断未复制");
        Add(entries, "SuKe could not copy the latest restore report.",
            "SuKe 无法复制最新的恢复报告。");
        Add(entries, "Active Window Rescued", "已救援活动窗口");
        Add(entries, "The active window was moved into a reachable work area.",
            "活动窗口已被移入可达的工作区。");
        Add(entries, "Active Window Is Reachable", "活动窗口本就可达");
        Add(entries, "The active window already has enough visible area.", "活动窗口的可见面积已经足够。");
        Add(entries, "No Active Window", "没有活动窗口");
        Add(entries, "Select an application window, then try again.", "请先选中一个应用窗口，然后重试。");
        Add(entries, "Window Rescue Needs Attention", "窗口救援需要处理");
        Add(entries, "SuKe could not update the active window placement.",
            "SuKe 无法更新活动窗口的位置。");
        Add(entries, "Display Recovery Paused", "显示恢复已暂停");
        Add(entries, "Display Layout Returned", "显示布局已返回");
        Add(entries, "Display Recovery Needs Attention", "显示恢复需要处理");
        Add(entries, "The recovered layout could not be restored cleanly. Review it from the SuKe tray.",
            "已恢复的布局无法顺利完成。请从 SuKe 托盘复核。");
        Add(entries, "Display Layout Restored", "显示布局已恢复");
        Add(entries, "Workspace Aligned", "工作区已对齐");
        Add(entries, "Alignment Needs Attention", "对齐需要处理");
        Add(entries, "The workspace could not be fully aligned. Open the preview for details.",
            "工作区无法完全对齐。打开预览查看详情。");
        Add(entries, "The workspace restore did not complete cleanly. Open the preview for details.",
            "工作区恢复未能顺利完成。打开预览查看详情。");
        Add(entries, "The selective restore did not complete cleanly. Open the preview for details.",
            "选择性恢复未能顺利完成。打开预览查看详情。");
        Add(entries, "Use “Switch to Workspace” to close everything else first.",
            "如需先关闭其他所有内容，请使用「切换到工作区」。");
        Add(entries, "Restore Preview Is Stale", "恢复预览已过期");
        Add(entries, "The desktop changed after preview. No stale action was applied; review a fresh plan.",
            "预览之后桌面发生了变化。没有应用任何过期操作；请复核一份新的计划。");
        Add(entries, "The reviewed plan could not be completed. Open the preview for details.",
            "已复核的计划无法完成。打开预览查看详情。");
        Add(entries, "Workspace switch needs review", "工作区切换需要复核");
        Add(entries, "Undo Needs Review", "撤销需要复核");
        Add(entries, "Switch Needs Review", "切换需要复核");
        Add(entries, "The recovery checkpoint contains entries that require review before it can be restored.",
            "该恢复检查点包含需要复核的条目，复核后才能恢复。");
        Add(entries, "Resolve blocking entries in the restore preview before switching.",
            "请在切换前解决恢复预览中的阻断条目。");
        Add(entries, "Undoing Restore…", "正在撤销恢复…");
        Add(entries, "Switching…", "正在切换…");
        Add(entries, "Undo Cancelled", "撤销已取消");
        Add(entries, "Switch Cancelled", "切换已取消");
    }

    // ── Restore plan presentation labels (RestorePlanPreview model) ───────────────────────────

    private static void AddRestorePlanPresentation(Dictionary<string, string> entries)
    {
        Add(entries, "Exact match", "精确匹配");
        Add(entries, "Adapted placement", "适配位置");
        Add(entries, "Ambiguous match", "歧义匹配");
        Add(entries, "Missing resource", "资源缺失");
        Add(entries, "Ready", "就绪");
        Add(entries, "Skipped", "已跳过");
        Add(entries, "Cancelled", "已取消");
        Add(entries, "Exact evidence", "精确证据");
        Add(entries, "Strong evidence", "强证据");
        Add(entries, "Probable evidence", "较可能证据");
        Add(entries, "Ambiguous", "歧义");
        Add(entries, "Missing", "缺失");
        Add(entries, "Ineligible", "不合格");

        Add(entries, "Move and resize existing window", "移动并调整已有窗口");
        Add(entries, "Launch application", "启动应用");
        Add(entries, "Open saved resource", "打开已保存的资源");
        Add(entries, "Open dedicated browser window", "打开专用浏览器窗口");
        Add(entries, "Launch installed web app", "启动已安装的 Web 应用");
        Add(entries, "Activate packaged application", "激活打包应用");
        Add(entries, "Restore browser session", "恢复浏览器会话");
        Add(entries, "Restore File Explorer tabs", "恢复文件资源管理器标签页");
        Add(entries, "Wait for window and position it", "等待窗口出现并摆放");
        Add(entries, "Minimize other open windows", "最小化其他已打开窗口");
        Add(entries, "No safe action", "没有安全的操作");
        Add(entries, "No additional action", "无需额外操作");
        Add(entries, " if browser-session restore is unavailable", "（当浏览器会话恢复不可用时）");
        Add(entries, "Other open windows will be minimized. No windows will be closed.",
            "其他已打开的窗口会被最小化。不会关闭任何窗口。");
        Add(entries, "No windows will be closed or minimized.", "不会关闭或最小化任何窗口。");

        Add(entries, "mapped by saved monitor number", "按保存时的显示器编号映射");
        Add(entries, "mapped to primary monitor", "映射到主显示器");
        Add(entries, "monitor unavailable", "显示器不可用");
        Add(entries, "exact monitor", "精确匹配的显示器");
        Add(entries, "normalized layout", "归一化布局");
        Add(entries, "legacy layout kept visible", "旧版布局保持可见");
        Add(entries, "saved coordinates retained", "保留已保存的坐标");
        Add(entries, "exact pixels", "精确像素");
        Add(entries, "unknown class", "未知类名");
        Add(entries, "unknown monitor", "未知显示器");
        Add(entries, "bounds unavailable", "边界不可用");
        Add(entries, "Unknown application", "未知应用");
        Add(entries, "Untitled window", "无标题窗口");
        Add(entries, "Workspace entry", "工作区条目");
        Add(entries, "Application", "应用");
        Add(entries, "Application: {0}", "应用：{0}");
    }

    // ── Restore plan explanations, warnings and blocking errors ───────────────────────────────
    // These strings originate in the placement planner and the per-application adapters. They are
    // rendered by RestorePlanPreviewDialog (Loc.TLabel / Loc.T on entry.Explanation, action.Label,
    // action.Explanation, warning and blocking text), so they must be catalogued verbatim.

    private static void AddPlanExplanations(Dictionary<string, string> entries)
    {
        // Chromium web-app adapter
        Add(entries, "The saved web-app shortcut is missing; planning will use a fallback target when available.",
            "已保存的 Web 应用快捷方式缺失；规划时若可用会改用回退目标。");
        Add(entries, "The saved web-app shortcut is stale; planning will use a fallback target when available.",
            "已保存的 Web 应用快捷方式已失效；规划时若可用会改用回退目标。");
        Add(entries, "Launch the installed web app through its observed shortcut.",
            "通过观测到的快捷方式启动已安装的 Web 应用。");
        Add(entries, "Launch the installed web app through its saved shortcut.",
            "通过已保存的快捷方式启动已安装的 Web 应用。");
        Add(entries, "The saved web app has neither a usable shortcut nor a fallback launch target.",
            "已保存的 Web 应用既没有可用的快捷方式，也没有回退启动目标。");
        Add(entries, "The web app has no launch target.", "该 Web 应用没有启动目标。");
        Add(entries, "The web-app launch target is unavailable.", "该 Web 应用的启动目标不可用。");
        Add(entries, "Launch the installed web app through its saved target and app identity arguments.",
            "通过已保存的目标与应用身份参数启动已安装的 Web 应用。");

        // Dedicated browser window adapter
        Add(entries, "The dedicated browser entry has no saved URL.", "该专用浏览器条目没有已保存的 URL。");
        Add(entries, "The dedicated browser URL is missing.", "专用浏览器的 URL 缺失。");
        Add(entries, "The dedicated browser entry has no executable path.",
            "该专用浏览器条目没有可执行文件路径。");
        Add(entries, "The dedicated browser executable is missing.", "专用浏览器的可执行文件缺失。");
        Add(entries, "The dedicated browser executable is unavailable.", "专用浏览器的可执行文件不可用。");
        Add(entries, "Open the saved site in its own browser window.", "在独立的浏览器窗口中打开已保存的站点。");

        // VS Code workspace adapter
        Add(entries, "The saved VS Code workspace or folder is unavailable.",
            "已保存的 VS Code 工作区或文件夹不可用。");
        Add(entries, "The VS Code workspace entry has no executable path.",
            "该 VS Code 工作区条目没有可执行文件路径。");
        Add(entries, "The VS Code executable is missing.", "VS Code 的可执行文件缺失。");
        Add(entries, "The VS Code executable is unavailable.", "VS Code 的可执行文件不可用。");
        Add(entries, "Open the saved VS Code workspace in the assigned editor window.",
            "在已分配的编辑器窗口中打开已保存的 VS Code 工作区。");
        Add(entries, "Open the saved VS Code workspace in a new editor window.",
            "在新的编辑器窗口中打开已保存的 VS Code 工作区。");

        // Windows Terminal adapter
        Add(entries, "A saved Terminal tab has no starting directory.",
            "有已保存的终端标签页没有起始目录。");
        Add(entries, "Terminal tab directories are incomplete.", "终端标签页的目录不完整。");
        Add(entries, "A saved Terminal tab directory is unavailable.",
            "有已保存的终端标签页目录不可用。");
        Add(entries, "The Windows Terminal wt.exe execution alias is unavailable.",
            "Windows 终端的 wt.exe 执行别名不可用。");
        Add(entries, "Open the saved Windows Terminal tabs in a new window.",
            "在新窗口中打开已保存的 Windows 终端标签页。");

        // ATLauncher adapter
        Add(entries, "The primary ATLauncher entry starts the launcher; wait for its console window.",
            "主 ATLauncher 条目会启动启动器；请等待其控制台窗口出现。");
        Add(entries, "Wait for the ATLauncher process to create its saved console window.",
            "等待 ATLauncher 进程创建其已保存的控制台窗口。");
        Add(entries, "ATLauncher.exe is unavailable; its bundled javaw.exe cannot be launched by itself.",
            "ATLauncher.exe 不可用；其自带的 javaw.exe 无法单独启动。");
        Add(entries, "Start ATLauncher through its stable Windows launcher instead of its Java runtime.",
            "请通过其稳定的 Windows 启动器而非 Java 运行时来启动 ATLauncher。");

        // Approval projection
        Add(entries, "Launch the browser directly because browser-session restoration was disabled with a browser entry.",
            "由于某个浏览器条目关闭了浏览器会话恢复，改为直接启动浏览器。");
        Add(entries, "No launch is approved for a disabled entry.", "被禁用的条目不会获得任何启动批准。");
        Add(entries, "Move the user-selected window to its mapped saved virtual desktop.",
            "把用户选中的窗口移动到其映射的已保存虚拟桌面。");

        // Placement verification
        Add(entries, "The assigned window closed before a placement retry.",
            "在重试摆放之前，被分配的窗口就已关闭。");
        Add(entries, "The assigned HWND became stale before a placement retry.",
            "在重试摆放之前，被分配的 HWND 已失效。");
        Add(entries, "Window placement did not verify within the bounded retry policy",
            "在有限重试策略内未能校验窗口摆放");
        Add(entries, "The application does not permit external window geometry control; launch and virtual-desktop restoration can still complete.",
            "该应用不允许外部控制窗口几何信息；启动与虚拟桌面恢复仍可完成。");

        // Placement planner
        Add(entries, "The saved executable version is no longer installed; the newest compatible Squirrel app-version directory will be used for matching and launch.",
            "已保存的可执行文件版本已不再安装；将使用最新的兼容 Squirrel app-* 版本目录进行匹配与启动。");
        Add(entries, "This entry has no launch contract that guarantees a distinct window; the safest existing match will be reused.",
            "该条目没有能够保证产生独立窗口的启动契约；将复用最安全的已有匹配。");
        Add(entries, "Choose a candidate in Restore Preview or skip this entry.",
            "请在恢复预览中选择一个候选窗口，或跳过该条目。");
        Add(entries, "The application is running, but its only matching window is already assigned to another saved entry. This saved editor entry contains a title only, not an openable file, folder, or .code-workspace path.",
            "应用正在运行，但其唯一匹配的窗口已分配给另一个已保存条目。这个已保存的编辑器条目只包含标题，而不是可打开的文件、文件夹或 .code-workspace 路径。");

        // Restore execution and readiness
        Add(entries, "Requested restoration through the approved browser-session action.",
            "已通过批准的浏览器会话操作请求恢复。");
        Add(entries, "Live windows exist, but none can be assigned safely yet.",
            "存在活动窗口，但暂时没有可以安全分配的窗口。");

        // Browser tab conflict prompt (shown by the browser session bridge)
        Add(entries, "A matching browser tab is already open in the saved browser profile.\n\nYes reuses the existing tab. No opens a duplicate. Cancel leaves it unchanged.",
            "已保存的浏览器配置文件中已打开匹配的标签页。\n\n「是」复用已有的标签页；「否」再打开一个重复的标签页；「取消」保持不变。");
        Add(entries, "{0} matching browser tabs are already open in the saved browser profile.\n\nYes reuses the existing tab. No opens a duplicate. Cancel leaves it unchanged.",
            "已保存的浏览器配置文件中已打开 {0} 个匹配的标签页。\n\n「是」复用已有的标签页；「否」再打开一个重复的标签页；「取消」保持不变。");
    }

    // ── Service messages surfaced in the interface ────────────────────────────────────────────

    private static void AddServiceMessages(Dictionary<string, string> entries)
    {
        Add(entries, "A live window can be positioned, but the saved resource must also be opened.",
            "可以定位一个活动窗口，但已保存的资源也需要被打开。");
        Add(entries, "Assign the candidate explicitly selected in Restore Preview and apply its target placement.",
            "采用你在恢复预览中显式选择的候选窗口，并应用其目标位置。");
        Add(entries, "Assign the selected live window and apply the target placement.",
            "采用选中的活动窗口并应用目标位置。");
        Add(entries, "The existing window already matches the requested placement; no action is needed.",
            "已有窗口已符合请求的位置，无需操作。");
        Add(entries, "No eligible live window exists, so a launch action is required.",
            "不存在符合条件的活动窗口，因此需要执行启动操作。");
        Add(entries, "No eligible live window exists and the resolved restore policy forbids launching it.",
            "不存在符合条件的活动窗口，且解析出的恢复策略禁止启动它。");
        Add(entries, "No live window was assigned because multiple candidates are too close.",
            "由于多个候选窗口过于接近，未分配任何活动窗口。");
        Add(entries, "Selective restore excludes this entry.", "选择性恢复排除了此条目。");
        Add(entries, "The entry is outside the monitors selected for this restore.",
            "该条目不在本次恢复所选的显示器范围内。");
        Add(entries, "Switch policy suppresses restore and launch actions.",
            "切换策略抑制了恢复与启动操作。");
        Add(entries, "The entry is ignored during exact switches; matching windows are preserved without being moved.",
            "该条目在精确切换期间被忽略；匹配窗口会被保留而不移动。");
        Add(entries, "Launch is disabled by the resolved entry policy.", "解析出的条目策略禁用了启动。");
        Add(entries, "Launch is suppressed until the ambiguity is explicitly resolved.",
            "在歧义被显式解决之前，启动被抑制。");
        Add(entries, "No launch is planned after cancellation.", "取消之后不再计划启动。");
        Add(entries, "Restore planning observed cancellation; no executable actions were produced.",
            "恢复规划期间观测到取消，未产生可执行的操作。");
        Add(entries, "The restore was cancelled before any planned action could execute.",
            "在任何计划操作执行之前，恢复已被取消。");
        Add(entries, "Move the assigned existing or newly launched window to its mapped saved virtual desktop.",
            "把已分配的既有窗口或新启动的窗口移到其映射的已保存虚拟桌面。");
        Add(entries, "Wait for the launched application to create an eligible window.",
            "等待已启动的应用创建符合条件的窗口。");
        Add(entries, "Wait for the browser-session connector to recreate the saved browser window.",
            "等待浏览器会话连接器重建已保存的浏览器窗口。");
        Add(entries, "Request restoration through the browser-session connector.",
            "通过浏览器会话连接器请求恢复。");
        Add(entries, "Reconcile the saved File Explorer tabs after the target window is ready.",
            "在目标窗口就绪后对账已保存的文件资源管理器标签页。");
        Add(entries, "One or more required resources are missing or stale.", "一个或多个必需资源缺失或已过期。");
        Add(entries, "Launch is suppressed until the ambiguity is explicitly resolved.",
            "在歧义被显式解决之前，启动被抑制。");
        Add(entries, "The saved document, folder, or resource is unavailable.", "已保存的文档、文件夹或资源不可用。");
        Add(entries, "No executable launch target is available.", "没有可用的可执行启动目标。");
        Add(entries, "The saved entry has no executable identity.", "该已保存条目没有可执行文件身份。");
        Add(entries, "The approved file-system target no longer exists.", "已批准的文件系统目标已不存在。");
        Add(entries, "The approved file-system target still exists.", "已批准的文件系统目标仍然存在。");
        Add(entries, "Eligible live-window candidates changed while the preview was open.",
            "预览打开期间，符合条件的活动窗口候选发生了变化。");
        Add(entries, "Eligible live-window candidates changed while the preview was open.",
            "预览打开期间，符合条件的活动窗口候选发生了变化。");
        Add(entries, "The approved preview became stale before execution; no plan mutation was started.",
            "已批准的预览在执行前过期，未开始任何计划变更。");
        Add(entries, "Preview-only plans are intentionally non-mutating.", "仅预览计划有意不做任何变更。");
        Add(entries, "An eligible responsive window exists; its identity and bounds are still stabilizing.",
            "存在符合条件的可响应窗口，其身份与边界仍在稳定中。");
        Add(entries, "An eligible window exists but is not responsive yet.", "存在符合条件的窗口，但尚未可响应。");
        Add(entries, "The process exists, but no eligible top-level window exists yet.",
            "进程已存在，但还没有符合条件的顶层窗口。");
        Add(entries, "The application process has not started.", "应用进程尚未启动。");
        Add(entries, "The application process is running, but it exposes no unassigned user-facing task window for this entry.",
            "应用进程正在运行，但它没有为该条目暴露未分配的、面向用户的任务窗口。");
        Add(entries, "The approved HWND was closed before its placement mutation.",
            "已批准的 HWND 在其位置变更之前已被关闭。");
        Add(entries, "The approved HWND now belongs to a different process.",
            "已批准的 HWND 现在属于另一个进程。");
        Add(entries, "The approved HWND no longer satisfies the saved identity.",
            "已批准的 HWND 不再符合保存的身份。");
        Add(entries, "The assigned window closed before placement verification completed.",
            "被分配的窗口在位置校验完成之前已关闭。");
        Add(entries, "The assigned window matches the planned normal bounds and show state.",
            "被分配的窗口符合计划的普通边界与显示状态。");
        Add(entries, "The assigned window has not settled at the planned placement yet.",
            "被分配的窗口尚未稳定在计划的位置上。");
        Add(entries, "The assigned window placement is temporarily unreadable while it settles.",
            "被分配窗口在稳定过程中暂时无法读取其位置。");
        Add(entries, "The window did not accept the planned placement within the bounded retry policy.",
            "在有界重试策略内，窗口未接受计划的位置。");
        Add(entries, "The app moved its window after the planned placement was observed.",
            "在观测到计划位置之后，应用移动了自己的窗口。");
        Add(entries, "The adapted placement was clamped to the visible monitor work area.",
            "适配后的位置被钳制到可见的显示器工作区内。");
        Add(entries, "The application does not permit external window geometry control; ",
            "该应用不允许外部控制窗口几何；");
        Add(entries, "Application does not permit external window geometry control",
            "应用不允许外部控制窗口几何");
        Add(entries, "The saved monitor is unavailable; a deterministic topology fallback was selected.",
            "已保存的显示器不可用；已选择确定性的拓扑回退方案。");
        Add(entries, "No current monitor topology was supplied; the saved DPI and coordinates are retained.",
            "未提供当前显示器拓扑；保留已保存的 DPI 与坐标。");
        Add(entries, "The saved placement has non-positive width or height.", "已保存位置的宽或高不是正数。");
        Add(entries, "The saved executable version is no longer installed; the newest compatible ",
            "已保存的可执行文件版本不再安装；最新兼容的 ");
        Add(entries, "Squirrel app-version directory will be used for matching and launch.",
            "Squirrel 应用版本目录将用于匹配与启动。");
        Add(entries, "The versioned package executable changed; the stable package identity will be used.",
            "带版本号的包可执行文件已变化；将使用稳定的包身份。");
        Add(entries, "The saved Squirrel app version changed; the newest installed version was resolved safely.",
            "已保存的 Squirrel 应用版本已变化；已安全解析到最新安装版本。");
        Add(entries, "Another saved entry has the same stable entry ID; list position is used for deterministic planning.",
            "另一个已保存条目具有相同的稳定条目 ID；将以列表位置进行确定性规划。");
        Add(entries, "This entry has no launch contract that guarantees a distinct window; ",
            "该条目没有能保证独立窗口的启动契约；");
        Add(entries, "A required launch resource was observed as missing.", "观测到必需的启动资源缺失。");
        Add(entries, "A required launch resource was observed as stale.", "观测到必需的启动资源已过期。");
        Add(entries, "A remembered choice for this workspace entry matches this composite identity.",
            "该工作区条目的一条记住的选择与此复合身份匹配。");
        Add(entries, "The user disabled this entry in the approved preview.",
            "用户在已批准的预览中禁用了该条目。");
        Add(entries, "The user explicitly selected this candidate in Restore Preview.",
            "用户在恢复预览中显式选择了该候选窗口。");
        Add(entries, "The user resolved the ambiguous match by selecting a specific live window.",
            "用户通过选择某个具体活动窗口解决了歧义匹配。");
        Add(entries, "The selected live window is the user-confirmed target for this entry.",
            "选中的活动窗口是该条目经用户确认的目标。");
        Add(entries, "The selected live window already satisfies this entry.", "选中的活动窗口已满足该条目。");
        Add(entries, "The entry already owns a revalidated live window.", "该条目已拥有一个经重新校验的活动窗口。");
        Add(entries, "The candidate met the configured evidence threshold.", "该候选达到了配置的证据阈值。");
        Add(entries, "No live window met the minimum evidence threshold.", "没有活动窗口达到最低证据阈值。");
        Add(entries, "The eligible window is responsive and its identity and bounds are stable.",
            "该符合条件的窗口可响应，且身份与边界稳定。");
        Add(entries, "No safe action is available: SuKe will not move one editor window twice or invent a path from its title.",
            "没有安全的操作可用：SuKe 不会把同一个编辑器窗口移动两次，也不会凭标题编造一个路径。");
        Add(entries, "No safe action is available: SuKe will not relaunch or wait for a background-only, tray-only, or already-assigned process.",
            "没有安全的操作可用：SuKe 不会为仅后台、仅托盘或已被分配的进程重新启动或等待。");
        Add(entries, "No successful action related to this entry could create an eligible live window.",
            "与该条目相关的操作均未能创建符合条件的活动窗口。");
        Add(entries, "No successful launch or browser action related to this entry required a reconciliation wait.",
            "与该条目相关的启动或浏览器操作均成功，无需对账等待。");
        Add(entries, "A pending resource action for the same executable is expected to start the application.",
            "针对同一可执行文件的待执行资源操作预计会启动该应用。");
        Add(entries, "Executable path and saved title prefix match.", "可执行文件路径与保存的标题前缀匹配。");
        Add(entries, "Executable path and window class form the generic fallback.",
            "可执行文件路径与窗口类名构成通用回退。");
        Add(entries, "Multiple same-executable windows were ranked by title similarity.",
            "多个同可执行文件窗口按标题相似度排序。");
        Add(entries, "Previous and live bounds provide weak geometric context.",
            "之前与当前边界提供较弱的几何上下文。");
        Add(entries, "Saved and live monitor identities provide weak context.",
            "已保存与当前的显示器身份提供较弱的上下文。");
        Add(entries, "The live title contains the saved document name.", "当前标题包含已保存的文档名称。");
        Add(entries, "The live title contains the saved project or workspace name.",
            "当前标题包含已保存的项目或工作区名称。");
        Add(entries, "The candidate score is below the {0}-point minimum.", "候选评分低于 {0} 分的最低要求。");
        Add(entries, "Title similarity is below the {0} minimum.", "标题相似度低于 {0} 的最低要求。");
        Add(entries, "The top {0} candidates are within the {1}-point safety margin.",
            "前 {0} 个候选处于 {1} 分的安全裕度之内。");
        Add(entries, "The candidate score is below the {policy.MinimumCandidateScore:0}-point minimum.",
            "候选评分低于最低分数线。");
        Add(entries, "Only an ambiguous entry or a prior user-selected match can accept a candidate selection.",
            "只有歧义条目或此前由用户选择的匹配才能接受候选选择。");
        Add(entries, "The requested window is not an eligible candidate for this entry.",
            "请求的窗口不是该条目的合格候选。");
        Add(entries, "The selected window is already assigned to another workspace entry.",
            "选中的窗口已被分配给另一个工作区条目。");
        Add(entries, "The approved plan was already cancelled.", "已批准的计划已被取消。");
        Add(entries, "The approved plan contains blocking errors.", "已批准的计划包含阻断错误。");
        Add(entries, "The approved window action is incomplete.", "已批准的窗口操作未完成。");
        Add(entries, "The approved File Explorer tab action is incomplete.", "已批准的文件资源管理器标签页操作未完成。");
        Add(entries, "The target File Explorer window did not become ready.", "目标文件资源管理器窗口未就绪。");
        Add(entries, "The approved launch action was requested successfully.", "已批准的启动操作已成功请求。");
        Add(entries, "The approved launch action failed.", "已批准的启动操作失败。");
        Add(entries, "The approved launch target is empty.", "已批准的启动目标为空。");
        Add(entries, "The approved URL is syntactically valid.", "已批准的 URL 语法有效。");
        Add(entries, "The action required no additional execution work.", "该操作不需要额外的执行工作。");
        Add(entries, "The browser-session action succeeded, so its approved fallback was not needed.",
            "浏览器会话操作成功，因此无需其已批准的回退方案。");
        Add(entries, "Browser-session restoration replaces a direct process launch.",
            "浏览器会话恢复替代了直接的进程启动。");
        Add(entries, "Browser-session restoration is unavailable; browser entries use ordinary matching and launch fallbacks.",
            "浏览器会话恢复不可用；浏览器条目改用普通匹配与启动回退。");
        Add(entries, "Browser-session restoration became unavailable; approved fallbacks remain eligible.",
            "浏览器会话恢复变为不可用；已批准的回退方案仍然有效。");
        Add(entries, "The browser-session connector became unavailable while the preview was open.",
            "预览打开期间，浏览器会话连接器变为不可用。");
        Add(entries, "The browser-session connector is expected to recreate this window.",
            "预期由浏览器会话连接器重建该窗口。");
        Add(entries, "The approved browser-session connector is no longer available.",
            "已批准的浏览器会话连接器不再可用。");
        Add(entries, "The {0} browser connector is not available.", "{0} 浏览器连接器不可用。");
        Add(entries, "Restore completed without reported failures.", "恢复已完成，未报告失败。");
        Add(entries, "Restore completed with {0} non-fatal warning(s)", "恢复已完成，有 {0} 条非致命警告");
        Add(entries, "Waiting for {0} applications", "正在等待 {0} 个应用");
        Add(entries, "Waiting for the browser connector.", "正在等待浏览器连接器。");
        Add(entries, "Matching Browser Tabs", "匹配浏览器标签页");
        Add(entries, "Yes reuses the existing tab. No opens a duplicate. Cancel leaves it unchanged.",
            "「是」复用已有标签页。「否」新开一个重复标签页。「取消」保持现状。");
        Add(entries, "A matching browser tab is available in this browser profile.",
            "该浏览器配置文件中存在匹配的标签页。");
        Add(entries, "Dedicated and ordinary browser windows cannot match each other.",
            "专用浏览器窗口与普通浏览器窗口不能互相匹配。");
        Add(entries, "An ordinary application entry cannot claim a PWA window.",
            "普通应用条目不能占用 PWA 窗口。");
        Add(entries, "The Windows Terminal alias will be resolved by Windows at launch.",
            "Windows 终端别名会在启动时由 Windows 解析。");
        Add(entries, "A saved Windows Terminal tab directory no longer exists.",
            "某个已保存的 Windows 终端标签页目录已不存在。");
        Add(entries, "Create shortcut… → Open as window", "创建快捷方式… → 以窗口方式打开");
        Add(entries, "Private virtual-desktop topology APIs are unsupported on this Windows build.",
            "此 Windows 版本不支持私有的虚拟桌面拓扑接口。");
        Add(entries, "Existing virtual desktops were mapped by saved order.",
            "既有虚拟桌面按保存顺序完成映射。");
        Add(entries, "Created {0} missing virtual desktop(s) and mapped the saved topology.",
            "已创建 {0} 个缺失的虚拟桌面并映射保存的拓扑。");
        Add(entries, "Creating any missing desktops before applications launch.",
            "在应用启动前先创建所有缺失的桌面。");
        Add(entries, "Preparing virtual desktops", "正在准备虚拟桌面");
        Add(entries, "Launching applications", "正在启动应用");
        Add(entries, "Verifying window placements", "正在校验窗口位置");
        Add(entries, "Verifying {0} window placement(s)", "正在校验 {0} 个窗口的位置");
        Add(entries, "Checking final bounds and window state.", "正在检查最终边界与窗口状态。");
        Add(entries, "Moved the matched window to its saved virtual desktop.",
            "已把匹配的窗口移到其保存的虚拟桌面。");
        Add(entries, "The matched window changed before its optional virtual-desktop association; it was left accessible.",
            "匹配的窗口在其可选的虚拟桌面关联之前发生了变化，它被留在了可访问状态。");
        Add(entries, "No existing or newly launched window was assigned for virtual-desktop placement.",
            "没有既有窗口或新启动的窗口被分配用于虚拟桌面放置。");
        Add(entries, "The saved virtual desktop move was unavailable; the window was left accessible. {0}",
            "已保存的虚拟桌面移动不可用；该窗口被留在可访问状态。{0}");
        Add(entries, "Cancellation interrupted File Explorer tab restoration.",
            "取消操作中断了文件资源管理器标签页恢复。");
        Add(entries, "Cancellation interrupted application readiness polling.",
            "取消操作中断了应用就绪轮询。");
        Add(entries, "Cancellation interrupted post-restore placement verification.",
            "取消操作中断了恢复后的位置校验。");
        Add(entries, "Cancellation was observed after initial window reconciliation.",
            "在初始窗口对账之后观测到取消。");
        Add(entries, "Cancellation was observed before final minimization.",
            "在最终最小化之前观测到取消。");
        Add(entries, "Restored {0} saved File Explorer tabs.", "已恢复 {0} 个保存的文件资源管理器标签页。");
        Add(entries, "Restored {0} File Explorer tabs; ", "已恢复 {0} 个文件资源管理器标签页；");
        Add(entries, "The approved File Explorer tab action is incomplete.", "已批准的文件资源管理器标签页操作未完成。");
        Add(entries, "File Explorer tab restoration was cancelled.", "文件资源管理器标签页恢复已取消。");
        Add(entries, "File Explorer tab capture was unavailable", "文件资源管理器标签页采集不可用");
        Add(entries, "File Explorer tab restoration failed", "文件资源管理器标签页恢复失败");
        Add(entries, "Browser browser session capture failed.", "浏览器会话采集失败。");
        Add(entries, "Browser session capture failed.", "浏览器会话采集失败。");
        Add(entries, "Browser extension capture was unavailable", "浏览器扩展采集不可用");
        Add(entries, "Browser extension capture timed out", "浏览器扩展采集超时");
        Add(entries, "Browser extension returned invalid capture data", "浏览器扩展返回了无效的采集数据");
        Add(entries, "Browser extension restore was unavailable", "浏览器扩展恢复不可用");
        Add(entries, "Browser extension protocol version is incompatible.", "浏览器扩展协议版本不兼容。");
        Add(entries, "Browser-session restoration was cancelled.", "浏览器会话恢复已取消。");
        Add(entries, "Browser host returned no response.", "浏览器 host 未返回响应。");
        Add(entries, "Browser-session restoration failed; approved fallbacks remain eligible.",
            "浏览器会话恢复失败；已批准的回退方案仍然有效。");
        Add(entries, "No supported browser windows were selected.", "未选择任何受支持的浏览器窗口。");
        Add(entries, "No supported generic fallback matched.", "没有匹配到受支持的通用回退。");
        Add(entries, "Executable paths match.", "可执行文件路径匹配。");
        Add(entries, "Executable paths differ.", "可执行文件路径不同。");
        Add(entries, "Process names match.", "进程名称匹配。");
        Add(entries, "Window classes match.", "窗口类名匹配。");
        Add(entries, "Folder identities match.", "文件夹身份匹配。");
        Add(entries, "Monitor identities match.", "显示器身份匹配。");
        Add(entries, "Browser families match.", "浏览器家族匹配。");
        Add(entries, "Dedicated browser site hosts match.", "专用浏览器站点 host 匹配。");
        Add(entries, "Dedicated browser site hosts differ.", "专用浏览器站点 host 不同。");
        Add(entries, "AppUserModelIDs match exactly.", "AppUserModelID 精确匹配。");
        Add(entries, "PWA AppUserModelIDs match exactly.", "PWA 的 AppUserModelID 精确匹配。");
        Add(entries, "PWA AppUserModelIDs differ.", "PWA 的 AppUserModelID 不同。");
        Add(entries, "Package families match.", "包族匹配。");
        Add(entries, "Packaged application identities match exactly.", "打包应用身份精确匹配。");
        Add(entries, "The packaged application identity is present.", "打包应用身份存在。");
        Add(entries, "The packaged application identity is missing.", "打包应用身份缺失。");
        Add(entries, "Legacy package family and executable name match across a Store update.",
            "跨商店更新时，旧版包族与可执行文件名称匹配。");
        Add(entries, "Resolved a missing versioned executable to the newest installed sibling",
            "已把缺失的版本化可执行文件解析到最新安装的同级版本");
        Add(entries, "Application readiness evaluation failed", "应用就绪评估失败");
        Add(entries, "Application readiness timed out", "应用就绪超时");
        Add(entries, "Readiness observation failed.", "就绪观测失败。");
        Add(entries, "Readiness polling was skipped because an earlier action did not succeed.",
            "由于此前的操作未成功，跳过了就绪轮询。");
        Add(entries, "Readiness strategy failed.", "就绪策略失败。");
        Add(entries, "Readiness timed out after {0} ms. ", "就绪等待在 {0} 毫秒后超时。");
        Add(entries, "The readiness strategy reported Ready without a safely matched window.",
            "就绪策略报告「就绪」，但没有安全匹配的窗口。");
        Add(entries, "No successful action related to this entry could create an eligible live window.",
            "与该条目相关的操作均未能创建符合条件的活动窗口。");
        Add(entries, "The application executable is unavailable.", "应用的可执行文件不可用。");
        Add(entries, "The saved entry has no executable path or alternate launch identity.",
            "该已保存条目既没有可执行文件路径，也没有备用启动身份。");
        Add(entries, "The approved file-system target can no longer be validated.",
            "已批准的文件系统目标已无法校验。");
        Add(entries, "The launch target was not probed before planning; the executor must validate it before use.",
            "启动目标在规划前未被探测；执行器必须在使用前校验它。");
        Add(entries, "The approved launch action failed.", "已批准的启动操作失败。");
        Add(entries, "Move call succeeded but the public API did not observe the temporary desktop.",
            "移动调用成功，但公开 API 未观测到临时桌面。");
        Add(entries, "Move-back call succeeded but the public API did not observe the original desktop.",
            "移回调用成功，但公开 API 未观测到原始桌面。");
        Add(entries, "The public Shell move failed with HRESULT {0}.", "公开 Shell 移动失败，HRESULT {0}。");
        Add(entries, "The requested desktop was not present in the Shell desktop collection.",
            "Shell 桌面集合中不存在所请求的桌面。");
        Add(entries, "Windows returned an invalid virtual desktop.", "Windows 返回了无效的虚拟桌面。");
        Add(entries, "Windows returned an empty temporary desktop ID.", "Windows 返回了空的临时桌面 ID。");
        Add(entries, "VirtualDesktopManager is unavailable.", "VirtualDesktopManager 不可用。");
        Add(entries, "Immersive Shell is unavailable.", "沉浸式 Shell 不可用。");
        Add(entries, "The public Shell move failed.", "公开 Shell 移动失败。");
        Add(entries, "The Shell application-view collection did not recognize the window ownership tree.",
            "Shell 应用视图集合未识别出该窗口归属树。");
        Add(entries, "Could not recreate the saved virtual desktop topology", "无法重建已保存的虚拟桌面拓扑");
        Add(entries, "The current topology must contain at least the saved desktop count.",
            "当前拓扑至少需包含已保存的桌面数量。");
        Add(entries, "Restoring File Explorer tabs", "正在恢复文件资源管理器标签页");
        Add(entries, "Restoring browser session", "正在恢复浏览器会话");
        Add(entries, "Launch the browser directly because browser-session ",
            "直接启动浏览器，因为浏览器会话");
        Add(entries, "Launch the browser directly only when browser-session restoration is unavailable.",
            "仅当浏览器会话恢复不可用时，才直接启动浏览器。");
        Add(entries, "Launch the browser with its session-restore flag.", "带会话恢复参数启动浏览器。");
        Add(entries, "Launch the saved application.", "启动已保存的应用。");
        Add(entries, "Open the saved resource through its registered handler.", "通过其注册的处理程序打开已保存的资源。");
        Add(entries, "Activate the packaged application through its AppUserModelID.",
            "通过其 AppUserModelID 激活打包应用。");
        Add(entries, "After reconciliation, minimize windows outside the final assigned HWND set.",
            "对账完成后，最小化不在最终已分配 HWND 集合内的窗口。");
        Add(entries, "Minimized windows outside the final approved assignment set.",
            "已最小化不在最终已批准分配集合内的窗口。");
        Add(entries, "Executed the approved launch action after resource revalidation.",
            "在资源重新校验之后执行了已批准的启动操作。");
        Add(entries, "Applied the approved placement to the revalidated live window.",
            "已把批准的位置应用到重新校验过的活动窗口。");
        Add(entries, "Assigned and positioned a responsive, stable eligible window.",
            "已分配并摆放了一个可响应、稳定的合格窗口。");
        Add(entries, "Verified the final window placement", "已校验最终窗口位置");
        Add(entries, "The revalidated live window already matches the approved target placement.",
            "重新校验后的活动窗口已符合已批准的目标位置。");
        Add(entries, "Virtual-desktop association was unavailable because the approved action was incomplete.",
            "由于已批准的操作未完成，虚拟桌面关联不可用。");
    }

    // ── Coordinator notifications and progress stages ─────────────────────────────────────────

    private static void AddCoordinatorMessages(Dictionary<string, string> entries)
    {
        Add(entries, "Restoring…", "正在恢复…");
        Add(entries, "Aligning…", "正在对齐…");
        Add(entries, "restoring windows", "恢复窗口");
        Add(entries, "positioning windows", "摆放窗口");
        Add(entries, "executing the reviewed plan", "执行已复核的计划");
        Add(entries, "restoring the previous desktop", "恢复上一个桌面");
        Add(entries, "closing unrelated windows", "关闭无关窗口");
        Add(entries, "“{0}” — creating a recovery checkpoint before {1}.",
            "“{0}” — 将在{1}之前创建恢复检查点。");
        Add(entries, "“{0}” — {1}; recovery checkpoints are disabled.",
            "“{0}” — {1}；恢复检查点已禁用。");
        Add(entries, "Fullscreen activity is active. Restore “{0}” from the SuKe tray when ready.",
            "当前有全屏活动。就绪后可从 SuKe 托盘恢复“{0}”。");
        Add(entries, "Restore “{0}” from the SuKe tray, or dismiss this recovery offer.",
            "从 SuKe 托盘恢复“{0}”，或忽略本次恢复邀请。");
        Add(entries, "“{0}” — {1} windows repositioned.", "“{0}” — 已重新摆放 {1} 个窗口。");
        Add(entries,
            "“{0}” restored — other open windows were left untouched. Use “Switch to Workspace” to close everything else first.",
            "“{0}”已恢复 — 其他已打开的窗口保持不变。如需先关闭其他所有内容，请使用「切换到工作区」。");
        Add(entries, "“{0}” — other windows were minimized, not closed.",
            "“{0}” — 其他窗口已被最小化，而不是关闭。");
        Add(entries, "“{0}” ({1}) — restored successfully.", "“{0}”（{1}） — 恢复成功。");
        Add(entries, "{0} window placement(s) could not be verified after bounded retries.",
            "{0} 个窗口的位置在有界重试后仍无法校验。");
        Add(entries, "“{0}” restored from the reviewed plan.", "已按复核过的计划恢复“{0}”。");
        Add(entries, "The reviewed workspace plan could not be completed.", "已复核的工作区计划无法完成。");
        Add(entries, "The previous desktop state was reconciled, including closing unrelated windows.",
            "上一个桌面状态已完成对账，包括关闭无关窗口。");
        Add(entries, "Switched to “{0}” — {1} entries processed.", "已切换到“{0}” — 处理了 {1} 个条目。");
        Add(entries, "Nothing to Undo", "没有可撤销的内容");
        Add(entries, "No recent recovery checkpoint is available.", "没有可用的近期恢复检查点。");
        Add(entries, "Restore Safely Blocked", "为安全起见已阻止恢复");
        Add(entries, "Waiting…", "正在等待…");
        Add(entries, "{0} window(s) still open — save your work to continue.",
            "仍有 {0} 个窗口未关闭 — 保存工作后可继续。");
        Add(entries, "Undo State Is Stale", "撤销状态已过期");
        Add(entries, "Switch Preview Is Stale", "切换预览已过期");
        Add(entries, "The desktop changed while restoring the recovery checkpoint. Try Undo Last Restore again.",
            "在恢复检查点期间桌面发生了变化。请重新执行「撤销上次恢复」。");
        Add(entries, "The desktop changed after review. Reopen the switch preview and try again.",
            "复核之后桌面发生了变化。请重新打开切换预览并重试。");
        Add(entries, "Restore Undone", "恢复已撤销");
        Add(entries, "Workspace Switched", "工作区已切换");
        Add(entries, "The previous desktop state could not be fully restored.",
            "上一个桌面状态无法完全恢复。");
        Add(entries, "Restore Needs Attention", "恢复需要处理");
        Add(entries, "Restore Completed", "恢复已完成");
        Add(entries, "Save Completed", "保存已完成");
        Add(entries, "Save Failed", "保存失败");
        Add(entries, "Restore Failed", "恢复失败");
        Add(entries, "Restore Cancelled", "恢复已取消");
        Add(entries, "Save Cancelled", "保存已取消");
        Add(entries, "Preparing", "正在准备");
        Add(entries, "Saving…", "正在保存…");
        Add(entries, "Cancelling…", "正在取消…");

        // Progress stage descriptions
        Add(entries, "Complete any visible save prompts to continue.",
            "请完成所有可见的保存提示以继续。");
        Add(entries, "Saving the workspace snapshot", "正在保存工作区快照");
        Add(entries, "Detecting files and folders for the selected windows",
            "正在为选中的窗口检测文件与文件夹");
        Add(entries, "Preparing the restore plan", "正在准备恢复计划");
        Add(entries, "Persisting the working set", "正在持久化工作集");
        Add(entries, "Creating the recovery checkpoint", "正在创建恢复检查点");
        Add(entries, "Restoring saved browser sessions", "正在恢复已保存的浏览器会话");
        Add(entries, "Launching missing applications", "正在启动缺失的应用");
        Add(entries, "Waiting for launched windows", "正在等待已启动的窗口");
        Add(entries, "Applying window placement", "正在应用窗口位置");
        Add(entries, "Verifying window placement", "正在校验窗口位置");
        Add(entries, "Minimizing unrelated windows", "正在最小化无关窗口");
        Add(entries, "Closing unrelated windows", "正在关闭无关窗口");
        Add(entries, "Finishing", "正在收尾");
        Add(entries, "Preparing the workspace capture", "正在准备工作区采集");
        Add(entries, "Saving the workspace", "正在保存工作区");
        Add(entries, "Restoring the workspace", "正在恢复工作区");
        Add(entries, "Reading the recovery checkpoint", "正在读取恢复检查点");
        Add(entries, "Restoring File Explorer tabs", "正在恢复文件资源管理器标签页");
        Add(entries, "Moving windows to their virtual desktops", "正在把窗口移到其虚拟桌面");
        Add(entries, "Applying the reviewed plan", "正在执行已复核的计划");
        Add(entries, "Waiting for the approved plan", "正在等待已批准的计划");
        Add(entries, "Executing the restore", "正在执行恢复");

        // Live progress messages and details
        Add(entries, "Identifying recovery resources for {0}", "正在为 {0} 识别恢复资源");
        Add(entries, "Finalizing recovery checkpoint", "正在完成恢复检查点");
        Add(entries, "Preparing recovery checkpoint", "正在准备恢复检查点");
        Add(entries, "Saving recovery checkpoint", "正在保存恢复检查点");
        Add(entries, "Writing the checkpoint atomically.", "正在原子化写入检查点。");
        Add(entries, "Launching {0}", "正在启动 {0}");
        Add(entries, "Associating virtual desktop ({0}/{1})", "正在关联虚拟桌面（{0}/{1}）");
        Add(entries, "Restoring File Explorer tabs ({0}/{1})", "正在恢复文件资源管理器标签页（{0}/{1}）");
        Add(entries, "{0} saved tabs", "{0} 个已保存标签页");
        Add(entries, "Checking open windows", "正在检查已打开的窗口");
        Add(entries, "{0} user window(s) inspected.", "已检查 {0} 个用户窗口。");
        Add(entries, "No unrelated windows need to close", "没有需要关闭的无关窗口");
        Add(entries, "Closing {0} unrelated window(s)", "正在关闭 {0} 个无关窗口");
        Add(entries, "Continuing with the restore.", "继续执行恢复。");
        Add(entries, "Waiting for {0} window(s) to close", "正在等待 {0} 个窗口关闭");
        Add(entries, "Searching files for {0}…", "正在为 {0} 搜索文件…");
        Add(entries, "Finalizing workspace…", "正在完成工作区…");
        Add(entries, "Capturing browser session…", "正在采集浏览器会话…");
        Add(entries, "{0} browser window(s)", "{0} 个浏览器窗口");
        Add(entries, "Assembling workspace snapshot…", "正在组装工作区快照…");
        Add(entries, "{0} window(s) still open. Workspace switch aborted.",
            "{0} 个窗口仍未关闭。工作区切换已中止。");
        Add(entries, "SuKe could not save a recovery checkpoint, so it made no desktop changes.",
            "SuKe 无法保存恢复检查点，因此没有对桌面做任何改动。");
        Add(entries, "Save any work if an application prompts you.", "如果应用弹出提示，请先保存你的工作。");
        Add(entries, "previous desktop", "上一个桌面");
        Add(entries, "{0}  ·  {1}", "{0}  ·  {1}");
    }


    // 三页壳层的文案统一沿用现有运行时本地化机制。
    private static void AddShell(Dictionary<string, string> entries)
    {
        Add(entries, "Activity", "活动");
        Add(entries, "Settings", "设置");
        Add(entries, "Saved Workspaces", "已保存的工作区");
        Add(entries, "Restore the whole desktop layout in one step", "一步恢复桌面布局");
        Add(entries, "Recent workspace activity", "最近的工作区活动");
        Add(entries, "Customise how workspaces are saved and restored", "自定义 SuKe 保存和恢复工作区的方式");
        Add(entries, "Select all", "全选");

        // Appearance and language. These are the only entries whose values must also be reversible
        // by the reverse map, so none of them may repeat another entry's Chinese text.
        Add(entries, "Appearance & Language", "外观与语言");
        Add(entries, "Interface language", "界面语言");
        Add(entries, "Applies to every SuKe window immediately, including the tray menu.", "立即对 SuKe 的所有窗口生效，包括托盘菜单。");
        Add(entries, "Simplified Chinese", "简体中文");
        Add(entries, "English", "英文");
        Add(entries, "Theme", "界面主题");
        Add(entries, "Follow the Windows app theme, or always use one palette.", "跟随 Windows 应用主题，或固定使用浅色／深色配色。");
        Add(entries, "Follow system", "跟随系统");
        Add(entries, "Light", "浅色");
        Add(entries, "Dark", "深色");

        // Saved screen previews. The preview windows and the card thumbnails resolve these through
        // Loc at render time, so they follow the language setting as well.
        Add(entries, "Saved Screen Preview", "保存时的屏幕预览");
        Add(entries, "View saved screen screenshots", "查看保存时的屏幕截图");
        Add(entries, "No screen screenshots are available for this workspace.", "此工作区没有可用的屏幕截图。");
        Add(entries, "No saved screen previews. Save this workspace again to capture its monitors.", "没有已保存的屏幕截图。重新保存该工作区即可采集各显示器的画面。");
        Add(entries, "Workspace saved, but some screen previews could not be captured.", "工作区已保存，但部分屏幕截图未能采集。");
        Add(entries, "Click a preview to enlarge it.", "点击截图即可放大查看。");
        Add(entries, "Actual size", "实际大小");
        Add(entries, "Fit to window", "适应窗口");
        Add(entries, "Zoom {0} percent", "缩放 {0}%");
        Add(entries, "Settings categories", "设置分类");
        Add(entries, "Ready  ·  {0} monitor connected", "就绪 · 已连接 {0} 台显示器");
        Add(entries, "Ready  ·  {0} monitors connected", "就绪 · 已连接 {0} 台显示器");
        Add(entries, "Restore", "恢复");
        Add(entries, "Screen preview", "屏幕预览");
        Add(entries, "More actions", "更多操作");
        Add(entries, "View the screenshots captured with this workspace", "查看保存此工作区时采集的屏幕截图");
        Add(entries, "{0} monitor(s)", "{0} 台显示器");
        Add(entries, "{0} window(s)", "{0} 个窗口");
        Add(entries, "{0} window(s) across {1} monitor(s)", "{1} 台显示器上的 {0} 个窗口");
        Add(entries, "{0} window(s) captured as a recovery point", "恢复检查点中记录了 {0} 个窗口");
        Add(entries, "Saved today", "今天已保存");
        Add(entries, "Saved yesterday", "昨天已保存");
        Add(entries, "Saved {0}", "保存于 {0}");
        Add(entries, "with files", "含文件");
        Add(entries, "{0} more", "另有 {0} 个应用");
        Add(entries, "All activity", "全部活动");
        Add(entries, "Restores", "恢复记录");
        Add(entries, "Saves", "保存记录");
        Add(entries, "System", "系统");
        Add(entries, "Deletions", "删除记录");
        Add(entries, "Deleted", "已删除");
        Add(entries, "Today", "今天");
        Add(entries, "Yesterday", "昨天");
        Add(entries, "Workspace saved", "工作区已保存");
        Add(entries, "Workspace saved: {0}", "工作区已保存：{0}");
        Add(entries, "Workspace deleted", "工作区已删除");
        Add(entries, "Workspace deleted: {0}", "工作区已删除：{0}");
        Add(entries, "Restore started", "恢复已启动");
        Add(entries, "Restore started: {0}", "恢复已启动：{0}");
        Add(entries, "Selected-monitor restore started", "选定显示器恢复已启动");
        Add(entries, "Selected-monitor restore started: {0}", "选定显示器恢复已启动：{0}");
        Add(entries, "Workspace switch started", "工作区切换已启动");
        Add(entries, "Workspace switch started: {0}", "工作区切换已启动：{0}");
        Add(entries, "Undo started", "撤销已启动");
        Add(entries, "Automatic display restore started", "显示器自动恢复已启动");
        Add(entries, "Align and minimize started", "对齐并最小化已启动");
        Add(entries, "Adaptive restore started", "自适应恢复已启动");
        Add(entries, "Checkpoint captured", "检查点已记录");
        Add(entries, "Saved", "保存");
        Add(entries, "No activity recorded yet", "暂无活动记录");
        Add(entries, "No activity in this filter", "此分类暂无活动");
        Add(entries, "Events appear here once you save a workspace or start a restore.", "保存工作区或生成恢复检查点后，活动会显示在这里。");
        Add(entries, "Switch back to All activity to see the events that are stored.", "切换到「全部活动」可查看已有记录。");
        Add(entries, "Activity details stay on this device and never include paths or window titles.", "活动详情仅保存在本地，不包含路径和窗口标题。");
        Add(entries, "Activity is based on saved workspaces and retained checkpoints. Restore results are not recorded. Details stay on this device and exclude paths and window titles.", "活动来自已保存工作区和保留的检查点，不代表恢复结果。详情仅在本地显示，不含路径和窗口标题。");
        Add(entries, "Show every durable event", "显示全部可读取的活动");
        Add(entries, "Operations that started a restore, switch, or undo", "已启动恢复、切换或撤销的记录");
        Add(entries, "Workspaces that were saved", "已保存工作区的记录");
        Add(entries, "Workspaces that were deleted", "已删除工作区的记录");
        Add(entries, "Display-driven and manual checkpoints", "显示器恢复及手动检查点记录");
        Add(entries, "Details", "详情");
        Add(entries, "Activity details", "活动详情");
        Add(entries, "Settings content", "设置内容");
        Add(entries, "Source: the current saved workspace. This is its latest save time, not a complete save history.", "来源：当前已保存的工作区。此处为最近保存时间，不是完整的保存历史。");
        Add(entries, "Source: a retained recovery checkpoint. It records the desktop before an operation; it does not prove that the restore completed. Older checkpoints may expire.", "来源：保留的恢复检查点。记录的是操作前的桌面状态，不能证明恢复已完成；较早的检查点可能过期。");

        // 工作区删除记录：活动流里唯一还能证明「这个工作区存在过」的一行。
        // 值不得与词典中任何其它条目的中文重复，否则反向映射会产生歧义。
        Add(entries, "This workspace was deleted. The summary below is its last saved content.", "此工作区已删除，下面列出的是它最后一次保存的内容。");
        Add(entries, "Last saved {0}", "最后保存于 {0}");
        Add(entries, "Deleted workspaces cannot be restored from this record; it is kept only as history.", "已删除的工作区无法通过这条记录恢复，保留它只作历史。");
        Add(entries, "Content of the deleted workspace", "被删工作区的内容");
        Add(entries, "Source: the deletion record written when the workspace was removed. It carries the last saved summary only, and contains no restore result.", "来源：删除工作区时写入的删除记录。它只携带最后一次保存的摘要，不含任何恢复结果。");
        Add(entries, "Save Workspace", "保存当前工作区");

        // 恢复结果记录：活动列表的摘要行、详情窗口的统计与原因。
        // 值不得与词典中任何其它条目的中文重复，否则反向映射会产生歧义。
        Add(entries, "All targets succeeded", "全部成功");
        Add(entries, "Partially completed", "部分完成");
        Add(entries, "Failed", "失败");
        Add(entries, "Plan became stale", "计划已失效");
        Add(entries, "Blocked before any change", "已拦下，未做改动");
        Add(entries, "Interrupted / result unknown", "中断／结果未知");
        Add(entries, "Result unknown", "结果未知");
        Add(entries, "Application windows", "应用窗口");
        Add(entries, "Browser tabs", "浏览器标签页");
        Add(entries, "Explorer tabs", "资源管理器标签页");
        Add(entries, "Terminal tabs", "终端标签页");
        Add(entries, "Whole operation", "整体操作");
        Add(entries, "Other items", "其他项目");
        Add(entries, "{0} result", "{0} 结果");
        Add(entries, "{0} result: {1}", "{0} 结果：{1}");
        Add(entries, "Selected-monitor restore", "选定显示器恢复");
        Add(entries, "Workspace switch", "工作区切换");
        Add(entries, "Undo", "撤销");
        Add(entries, "Automatic display restore", "显示器自动恢复");
        Add(entries, "Align and minimize", "对齐并最小化");
        Add(entries, "Adaptive restore", "自适应恢复");
        Add(entries, "Checkpoint capture", "检查点记录");
        Add(entries, "Planned {0}", "计划 {0}");
        Add(entries, "{0}: planned {1}, succeeded {2}", "{0}：计划 {1} 个，成功 {2} 个");
        Add(entries, ", failed {0}", "，失败 {0} 个");
        Add(entries, ", skipped {0}", "，跳过 {0} 个");
        Add(entries, ", cancelled {0}", "，取消 {0} 个");
        Add(entries, ", blocked {0}", "，受限 {0} 个");
        Add(entries, ", stale {0}", "，计划失效 {0} 个");
        Add(entries, ", unconfirmed {0}", "，未确认 {0} 个");
        Add(entries, "Nothing was planned for this operation", "本次操作没有计划任何内容");
        Add(entries, "Nothing was recorded for this operation.", "本次操作没有记录任何内容。");
        Add(entries, "Duration {0}", "耗时 {0}");
        Add(entries, "Started {0}", "开始时间 {0}");
        Add(entries, "Ended {0}", "结束时间 {0}");
        Add(entries, "Still in progress", "仍在进行");
        Add(entries, "Overall result: {0}", "总体结果：{0}");
        Add(entries, "Failed and incomplete items ({0})", "失败与未完成项（{0}）");
        Add(entries, "No failures or incomplete items were recorded.", "没有记录失败或未完成项。");
        Add(entries, "No restore result was saved for this record, so success or failure cannot be determined.", "这条记录没有保存恢复结果，无法判断成功或失败。");
        Add(entries, "Older records did not store a restore result, so no success or failure can be shown.", "旧记录没有保存恢复结果，因此无法显示成功或失败。");
        Add(entries, "This is a saved workspace. Open a restore entry to see window and tab results.", "这是一条工作区保存记录。打开恢复类记录的详情可查看窗口与标签页的结果。");
        Add(entries, "“Succeeded” only counts targets the executor confirmed finished. Requested launches that never appeared stay unconfirmed.", "「成功」只统计执行器确认完成的目标；仅发出启动请求而窗口始终未出现的，仍记为未确认。");
        Add(entries, "Source: the result recorded by the restore itself. Every number comes from the real execution result, not from the saved content.", "来源：恢复过程自身记录的结果。所有数字都来自真实执行结果，而不是已保存的内容。");
        Add(entries, "Activity is based on saved workspaces, retained checkpoints, and the recorded result of each restore. Details stay on this device and exclude paths, window titles, and URLs.", "活动来自已保存的工作区、保留的检查点，以及每次恢复记录的结果。详情仅保存在本地，不含路径、窗口标题和网址。");
        Add(entries, "The window moved, but its final placement could not be verified", "窗口已移动，但最终位置未通过验证");
        Add(entries, "An execution step for this window failed", "针对该窗口的执行步骤失败");
        Add(entries, "A precondition for this window was missing", "该窗口缺少前置条件");
        Add(entries, "This window was cancelled before it was restored", "该窗口在恢复前被取消");
        Add(entries, "The saved window or its resource changed after review", "复核后保存的窗口或其资源发生了变化");
        Add(entries, "The application was asked to start, but its window was not confirmed", "已请求启动应用，但未确认其窗口");
        Add(entries, "The application started, but no matching window appeared", "应用已启动，但没有出现匹配的窗口");
        Add(entries, "The executor did not reach a final state", "执行器未得到最终状态");
        Add(entries, "The executor recorded no result for this target", "执行器没有为该目标记录结果");
        Add(entries, "The saved File Explorer tabs could not be reopened", "无法重新打开保存的资源管理器标签页");
        Add(entries, "A saved File Explorer folder no longer exists", "保存的资源管理器文件夹已不存在");
        Add(entries, "The saved Windows Terminal tabs could not be reopened", "无法重新打开保存的 Windows 终端标签页");
        Add(entries, "A saved Windows Terminal directory no longer exists", "保存的 Windows 终端目录已不存在");
        Add(entries, "The browser did not confirm the tab-restore request", "浏览器未确认标签页恢复请求");
        Add(entries, "The browser session was no longer available", "浏览器会话已不可用");
        Add(entries, "Browser tabs were saved, but no tab-restore request ran", "保存了浏览器标签页，但没有执行标签页恢复请求");
        Add(entries, "The restore was blocked before it changed the desktop", "恢复在改动桌面之前被拦下");
        Add(entries, "The desktop changed after review, so no stale action was applied", "复核后桌面已变化，因此未套用任何过期动作");
    }

    private static void Add(Dictionary<string, string> entries, string english, string chinese)
    {
        entries[english] = chinese;
    }
}
