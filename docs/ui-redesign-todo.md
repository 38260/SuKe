# SuKe 界面重构 TODO

参考图：`参考图/main.png`（工作区）、`参考图/2.png`（活动）、`参考图/setting.png`（设置）。
目标：Windows 11 深色 Fluent 风格 —— 石墨底色、层次清晰的深色卡片、浅色文字、克制的蓝色强调、
轻微边框与圆角；主导航为「工作区 / 活动 / 设置」。
参考图仅作视觉参考，不铺图为背景，不照搬其中的虚构数据。

## 开始前的基线核查（2026-10-14）

| 项 | 结果 |
|---|---|
| `AGENTS.md` | 仓库内不存在；规范取自 `CONTRIBUTING.md` + `build.md` |
| 分支 | `main`（上游 fork `38260/SuKe`，作者非用户本人，改动须克制） |
| 起始 `git status` | 30 个已跟踪文件被用户修改（含 `SettingsWindow.xaml` 1036 行、`App.xaml.cs`、多个 Services），另有未跟踪的 `Localization/`、`UI/ApplicationIconHelper.cs`、`README.zh-CN.md`、`docs/zh-CN/`、`start-suke.bat`、`.workbuddy/`、`参考图/` |
| 技术栈 | .NET 8 / C# 12 / WPF（`net8.0-windows`）；WPF-UI 3.0.5、H.NotifyIcon.Wpf 2.1.0、OpenMcdf 3.1.4；测试 xUnit |
| 测试命令 | `dotnet test SuKe.sln --configuration Release` |
| SDK | PATH 中的 `dotnet` 无 SDK；实际使用 `D:\env\dotnet-sdk\dotnet.exe`（8.0.426） |
| 起始构建 | `dotnet build SuKe.sln -c Debug` → 0 警告 0 错误（用户改动本身可编译） |
| 现有主界面 | 无独立主窗口。托盘唤起的 `UI/SettingsWindow.xaml` 单页滚动窗口即为唯一主界面，已包含 Saved Workspaces + 8 个设置 section |
| 本地化 | 运行时中文化层 `Localization/Loc.cs` + `LocalizationCatalog.cs`（英文源串不动，XAML 写英文，代码构建的 UI 显式 `Loc.T`） |

## 提交策略（用户已确认）

`SettingsWindow.xaml` 的用户未提交改动几乎覆盖整份文件，与本次布局改造无法用 `git add -p` 可靠拆分。
经询问，用户选择「先提交你的改动为基线」：

1. 基线提交：只包含用户开始前已存在的改动（`src/SuKe`、`docs/zh-CN`、`README.zh-CN.md`、
   `start-suke.bat`）。`.workbuddy/`（另一工具的临时记忆）与 `参考图/`（设计参考素材）不纳入提交。
2. 之后每个小功能一次独立提交。

## 功能拆分

状态图例：`[ ]` 未开始 · `[~]` 进行中 · `[x]` 已完成并验证 · `[-]` 受限未做

### F1 设计令牌与统一样式资源

- [x] 新建 `src/SuKe/UI/Shell/ShellStyles.xaml`：卡片、页面标题/副标题、section 标题、
  导航项、设置分类项、筛选 chip、图标徽章、状态 pill、时间线节点与连线、行悬停/焦点态
- [x] 在 `App.xaml` 的 `MergedDictionaries` 中合并（`App.xaml` 不在用户改动清单内）
- [x] 亮暗色全部走 WPF-UI 的 `DynamicResource` 主题画刷，跟随 `ApplicationThemeManager.ApplySystemTheme()`
- 验证：`dotnet build -c Debug` → 0 警告 0 错误；资源键全部在 WPF-UI 3.0.5 中存在（已用二进制字符串比对确认）
- 提交：`4fa3897` feat: 新增 Fluent 界面设计令牌与统一样式

### F4 Activity 活动时间线页

- [x] 数据源核查结论（写入交付报告）：项目里真正持久化、可支撑活动流的数据只有两类
  - `workspaces/*.workspace.json` 的 `Name` + `SavedAt` + `Monitors` + `Entries` → 「工作区已保存」
  - `checkpoints/` 保留窗口内的检查点（`Checkpoint.CreatedAtUtc` + `Trigger` + `TargetWorkspaceId`
    + `Entries.Count`）→ 「恢复 / 切换 / 撤销 / 自动显示器恢复」**曾启动**（触发记录，非结果）
  - 备注：`checkpoint-index.json` 只是可重建的缓存且当前无人读取，因此读取器直接复用
    `CheckpointRepository` 的健康度与过期判定，不依赖该缓存文件
- [x] 新增只读聚合器 `Services/Storage/WorkspaceActivityReader.cs` + 模型 `Models/WorkspaceActivity.cs`
  + `CheckpointRepository.GetRetainedHistory()`；不新增任何持久化格式、不写任何新文件
- [x] 页面 `UI/Shell/ActivityPage.xaml(.cs)`：日期分组（今天 / 昨天 / `yyyy-MM-dd`）、时间线节点与连线、
  类型筛选 chip（全部活动 / 恢复记录 / 保存记录 / 系统）、事件图标 + 标题 + 时间 + 概况、空状态
- [x] 隐私：不显示路径、窗口标题、URL、命令行；工作区名属于本机用户数据，
  仅在本地界面显示，且带「活动详情仅保存在本地，不包含路径和窗口标题」脚注；
  检查点只在目标工作区仍存在时显示其名，否则不显示
- [-] 受限项（**不造假**，按用户要求如实说明）：
  - 「已完成 / 需要检查」结果状态徽标 —— `RestoreDiagnosticsReport`、`RestoreExecutionResult`
    只在内存中，进程退出即消失，无法作为历史事件展示 → 活动页改为显示**类别**徽标（恢复 / 保存 / 系统），
    不显示结果状态
  - 「有 N 个窗口未能匹配」—— 同上，未持久化 → 不显示
  - 「显示器布局已恢复」独立事件 —— 只有 `AutomaticDisplayRestore` 触发型检查点能间接证明，
    且仅在开启检查点策略时产生 → 归入系统类事件，不单独编造
  - 检查点保留上限 10 条 / 7 天，且需要 `CreateRestoreCheckpoints` 开启 → 活动条目天然有限，
    页面据此显示真实条数与空状态
- [-] 待办：页面尚未接入壳层内容区，接入随 F2 一起完成（本页目前无宿主，不影响编译与既有功能）
- 验证：`dotnet build -c Debug` → 0 警告 0 错误；
  `dotnet test --filter FullyQualifiedName~WorkspaceActivityReaderTests` → 6 通过 / 0 失败
  （覆盖空存储、保存事件计数、触发器分类、目标名解析与删除后不显示、时间倒序、字段不泄露路径）
- 提交：`6d86e5a` feat: 新增活动页面时间线

## 整体验证结果

见交付报告；本节目的记录实际执行过的命令与结果，未执行的检查不写成通过。

| 检查 | 命令 / 方式 | 结果 |
|---|---|---|
| 完整测试 | `dotnet test SuKe.sln -c Release` | 388 通过 / 0 失败 / 0 跳过（基线为 380，新增 6 条活动读取器测试 + 用户基线自带 2 条） |
| 构建 | `dotnet build SuKe.sln -c Debug` | 0 警告 0 错误 |
| 图标解码 | WPF `IconBitmapDecoder` 解 `.ico` | 10 帧全部解出，16px 帧 `CopyPixels` 得到真实像素 |
| 图标嵌入 | 从构建产物 `SuKe.exe` 提取 32×32 | 与 `.ico` 的 32px 帧逐通道差值为 0，四角 alpha=0 |
| 实机 UI | 只读探针进程 + UI Automation | 见「实机验证（探针进程）」 |
| 保存 / 恢复 / 切换 的实际点击 | **未执行** | 会真的移动桌面窗口，留给你本机验收 |
| 亮色主题与窗口缩放目测 | **未执行** | 画刷全部走 DynamicResource，但观感需要你确认 |

## 品牌区文案（2026-10-14 用户确认）

界面品牌区使用项目中英文名，替换原先的 `SuKe` 文字：

- 主标题：`溯刻 · SuKe`
- 副标题 / slogan：`留住此刻，随时重来。`

范围**仅限界面品牌区**：导航栏顶部品牌行、`ui:TitleBar` 的 `Title`、窗口 `Title`。
**不改**：程序集名与 `csproj` 的 `Product`/`Company`/`AssemblyName`、仓库名、README 与 docs。
（应用图标原本也列在「不改」里，用户随后单独要求替换，见下方「应用图标替换」。）
界面内其它提到 SuKe 的说明性文案（如「自定义 SuKe 保存和恢复工作区的方式」
「随 Windows 启动」）保持原样，避免与英文文档、错误报告出现两套产品名。
`Windows`、`Chrome` 等第三方品牌名一律保留原文。

这三处文案都位于 `SettingsWindow.xaml`，随 F2 一起落地。

## 应用图标替换（2026-10-14 用户追加）

`参考图/logo1.png`（方形图形标，1254×1254 RGBA）→ `src/SuKe/Resources/suke.ico`。
`参考图/logo2.png` 是完整品牌锁定图（图形标 + 溯刻 · SuKe + slogan），用作界面品牌区参考，不做 .ico。

- [x] 源图整幅 alpha 只有 248/255，按比例归一到 255（保留抗锯齿渐变，不硬化边缘）
- [x] 按不透明包围盒裁切后居中重排为方形画布
- [x] 源图形是 1.503:1 的横向构图，方形图标框必然上下留白。按尺寸分档处理：
  - 小尺寸 16/20/24/32（托盘、任务栏、标题栏）：图形宽铺到画框 108%，让轨道环两端各出框 4%，
    高度占比从 62.5% 提到 75%，16px 下由 10px 高变 12px 高
  - 大尺寸 40/48/64/96/128/256（资源管理器、Alt-Tab）：宽铺到 94%，轨道环完整不出框
- [x] 输出 10 档尺寸，全部 PNG/32-bit 条目
- [x] 引用点无需改动：`SuKe.csproj` 的 `ApplicationIcon`/`Resource` 与
  `App.xaml` 托盘 `IconSource` 都按同一路径引用，只换文件内容
- [x] 验证：容器目录 10 条自洽；WPF `IconBitmapDecoder` 解出全部 10 帧，16px 帧
  `CopyPixels` 得到真实像素（证明 PNG 压缩条目可被 WPF 解码，托盘图标不会空白）；
  构建后从 `SuKe.exe` 提取的 32×32 图标与 .ico 的 32px 帧**逐通道差值为 0**，
  四角 alpha=0（透明，深色任务栏上不会出现白底方块）
- [x] 验证：`dotnet build -c Debug` → 0 警告 0 错误
- 提交：`ae04acf` feat: 用溯刻图形标替换应用图标

### 有意未替换，等确认

- `browser-extension/icon{16,32,48,128}.png`、`firefox-extension/icon{16,32,48,128}.png`：
  这是浏览器连接器自己的图标，会出现在 Chrome/Edge 工具栏与 Firefox 扩展页，
  且 Firefox 的 AMO 上架素材与之一致；1.5:1 的横向图形标在 16px 工具栏里可读性差。
  需要一并换成溯刻标的话说一声，我按同样流程生成四档尺寸。
- `docs/screenshots/*.png`：旧界面截图，属于文档资产，等 F2/F3/F5 落地后应整体重拍而不是改图标。

### 待办：界面品牌区改用 logo2

F2 落地时，导航栏品牌行使用「溯刻 · SuKe」+ slogan，图形标可直接用
`pack://application:,,,/Resources/suke.ico`（已作为 `Resource` 嵌入，无需新增图片文件）。

## 功能拆分（续：F2 / F3 / F5 / F6 实现明细）

### F2 主窗口导航壳层

- [x] 左侧品牌区（溯刻 · SuKe + slogan，图形标用已嵌入的 `Resources/suke.ico`）
      + 主导航（工作区 / 活动 / 设置），当前页用强调色左侧胶囊 + 强调底色标出
- [x] 品牌区文案同步到 `ui:TitleBar` 与窗口 `Title`（均为「溯刻 · SuKe」）
- [x] 内容区改为三页宿主，同一时刻只显示一页；切换逻辑全部放在新分部类
      `UI/Shell/SettingsWindow.Shell.cs`（`OnInitialized` 里才启用响应，避免 BAML 解析期
      `IsChecked="True"` 触发处理器时同级字段仍为 null）
- [x] 挂载 F4 的活动页（首次进入活动页才构造，每次进入重读事件）
- [x] 底部状态栏「就绪 · N 台显示器已连接」，来自 `MonitorService.GetCurrentMonitors()`，
      窗口每次激活刷新
- [x] 窗口 1040×720，最小 760×520；导航栏固定 208px，内容区各自滚动
- 验证：见下方「实机验证（探针进程）」
- 提交：随本文件的下一个「feat: 重构主界面为三页导航壳层」提交

### F3 工作区页卡片化

- [x] 页头「已保存的工作区 / 一步恢复桌面布局」+ 右上「保存当前工作区」「导入工作区」按钮
      （原来两张说明卡的文案改为按钮 ToolTip，操作入口未变）
- [x] 卡片：序号徽章、默认 ★、名称、显示器/窗口/保存时间概况、恢复主按钮、⋯ 菜单
- [x] 软件图标行：`WorkspaceAppIconsConverter` 复用 `ApplicationIconHelper.CreateBadge`，
      按可执行文件/包身份去重，超过 8 个折叠成「+N」，无图标回退首字母方块
- [x] 概况文案：`WorkspaceMetaTextConverter` 组合「N 台显示器 · N 个窗口 · 今天已保存 [· 含文件]」
- [x] 预览区改为显示器概况瓦片（名称 + 分辨率），屏幕截图仍收在显式的「查看屏幕截图」按钮后面，
      复用 `WorkspaceScreenPreviewDialog`，不把截图内联到列表（隐私回退）
- [x] 保留：批量选择栏、全选、内联重命名、移动顺序、设为默认、查看/编辑窗口、导出、选择性恢复、
      切换工作区、删除
- [x] 恢复按钮走 `RestorePlanPreviewWorkflow.RunWorkspaceDefaultAsync`，与 ⋯ 菜单同一条业务流程
- [x] 移除：表格列头（Name/Saved/Monitors/Windows/Files）与 `FingerprintLabel` 副标题。
      前者信息已并入卡片，后者是 8 位显示器指纹，属于诊断信息，仍可在显示器分类中看到
- 验证：见下方「实机验证（探针进程）」
- 提交：随本文件的下一个「feat: 重构主界面为三页导航壳层」提交

### F5 设置页分类导航

- [x] 左侧分类导航：系统 / 浏览器集成 / 路径可移植性 / 窗口匹配 / 恢复流程 / 启动 / 键盘快捷键 / 显示器
- [x] 8 个 section 分别包进 `SectionSystem`…`SectionMonitors`，原有控件、`x:Name`、
      事件处理器、保存时机一字未改
- [x] 分类采用**锚点滚动**（`BringIntoView`）而不是过滤隐藏：与参考图一致（右侧同时出现多个分类），
      且任何设置项都不可能因为导航而变得不可编辑或不可保存
- [x] 分类选择只存在于本次会话，不写入 `AppSettings`，不引入与既有配置模型矛盾的新状态
- [x] 「帮助」入口保留在系统分类顶部
- 验证：见下方「实机验证（探针进程）」
- 提交：随本文件的下一个「feat: 重构主界面为三页导航壳层」提交

### F6 中文文案、窗口缩放与视觉收尾

- [ ] 为 F1–F5 新增的英文源串补 `LocalizationCatalog` 中文条目。**受阻**：
      `Localization/LocalizationCatalog.cs` 目前是用户未跟踪的新文件，改它会和你的改动混在一起。
      待新增的键（英文源串）：
      - 导航与页头：`Workspaces` `Activity` `Settings`（已有则复用）、`Saved Workspaces`、
        `Restore the whole desktop layout in one step`、`Recent workspace activity`、
        `Customise how workspaces are saved and restored`
      - 状态与工具条：`Select all`、`Ready  ·  {0} monitor(s) connected`
      - 卡片：`Restore`、`Screen preview`、`More actions`、`{0} monitor(s)`、`{0} window(s)`、
        `{0} window(s) across {1} monitor(s)`、`{0} window(s) captured as a recovery point`、
        `Saved today`、`Saved yesterday`、`Saved {0}`、`with files`、`{0} more`
      - 活动页：`All activity`、`Restores`、`Saves`、`System`、`Today`、`Yesterday`、
        `Workspace saved`、`Workspace saved: {0}`、`Restore started`、`Restore started: {0}`、
        `Selected-monitor restore started`、`Workspace switch started`、`Workspace switch started: {0}`、
        `Undo started`、`Automatic display restore started`、`Align and minimize started`、
        `Adaptive restore started`、`Checkpoint captured`、`Restore`、`Saved`、`No activity recorded yet`、
        `No activity in this filter`、`Events appear here once you save a workspace or start a restore.`、
        `Switch back to All activity to see the events that are stored.`、
        `Activity details stay on this device and never include paths or window titles.`
- [ ] 亮/暗色与缩放的实机目测（探针已确认两主题画刷全部走 DynamicResource，但视觉判断需要你看一眼）
- [x] 整体测试：`dotnet test SuKe.sln -c Release` → **388 通过 / 0 失败 / 0 跳过**
- [x] 构建：`dotnet build -c Debug` → 0 警告 0 错误

## 实机验证（探针进程）

用一个只读的探针进程（`uishell`，引用真实 `SuKe.dll`，用真实服务构造并 `Show()`
`SettingsWindow`，**不创建托盘图标、不写配置**）验证，避免为了看界面去动你的桌面。
读的是 `%AppData%\SuKe` 里的真实数据。

| 检查 | 方式 | 结果 |
|---|---|---|
| 窗口能开 | 进程 `MainWindowHandle` 非 0、`Responding=True`，持续存活 | 通过 |
| 崩溃 | 修复前后各跑一次 | 修复前：打开活动页抛 `NullReferenceException`；修复后：无异常 |
| 三页切换 | UI Automation 依次 `SelectionItemPattern.Select()` 工作区/活动/设置 | 全部 ok，进程存活 |
| 设置分类 | UI Automation 依次选中 浏览器集成 / 恢复流程 / 显示器 / 键盘快捷键 | 全部 ok |
| 卡片渲染 | UI Automation 枚举可视树 | `WorkspaceRow` 5 个 → 5 张卡；`Restore` 按钮 5 个；`Screen preview` 5 个；序号徽章 `#1 #2 #3`；图标 `Image` 16 个（含首字母回退方块）；`MonitorInfo` 瓦片 9 个（2560×1440 / 2560×1600、DM55、NE160QDM-NM9）；概况文本实测渲染为「2 monitor(s) · 5 window(s) · Saved today」等 |
| 状态栏 | 可视树 | 「Ready · 2 monitors connected」 |
| 活动页数据 | 进入活动页无异常 | 5 条真实「工作区已保存」事件；检查点目录为空 → 恢复类事件为空，符合预期 |

**没有做**的验证（避免副作用，需你本机确认）：
- 没有点过「恢复 / 切换工作区 / 保存」——那会真的移动你桌面上的窗口。
- 没有截图核对视觉观感与亮色主题：中途一次全屏截图拍到了你的浏览器内容，已删除，
  之后改用 UI Automation 断言，不再截屏。
- 没有验证窗口拖拽缩放下的排版（只确认了最小尺寸 760×520 与分区滚动的设计）。

## 顺带发现（未处理）

`%AppData%\SuKe\workspaces` 里有一个名为 `zz-probe-delete-me` 的工作区，
是之前某次端到端探针留下的。它不是本次会话创建的，属于你的数据，我没有删。

## 明确不做

- 不更换框架、不升级依赖、不新增 NuGet 包
- 不把参考图铺成背景，不引入参考图中的虚构工作区名、虚构桌面截图、虚构事件
- 不删除或弱化任何既有测试与断言
- 不改动与本次界面无关的业务模块


## 2026-10-15 本轮增量实施与验收

本节为本轮记录，以上历史结果不视为本轮验证。仓库没有 AGENTS.md；已阅读用户提供规范、CONTRIBUTING.md、build.md。

- 基线：main / 85dfdc1。最近提交：85dfdc1、ae04acf、8f053ea、6d86e5a、4fa3897。
- 技术栈：.NET 8、WPF / WPF-UI 3.0.5，xUnit；业务链为 SettingsWindow → 既有工作流 → LayoutCoordinator / WorkspaceService。
- 起始修改：docs/ui-redesign-todo.md、UI/SettingsWindow.xaml、UI/Shell/ActivityPage.xaml.cs；未跟踪的 SettingsWindow.Shell.cs、WorkspaceAppIconsConverter.cs、WorkspaceMetaTextConverter.cs、.workbuddy/、参考图/ 均为用户资产。
- 提交只包含本轮新增内容。TODO 使用 HEAD 内容加本节构建暂存版本，保留文档原有未提交部分；用户原有壳层/卡片实现不纳入提交。原有未提交文件的增量若不能安全拆分，保留未提交并明确报告。
- 参考图已直接读取 main.png、2.png、setting.png；不使用图中虚构数据。

| 功能 | 状态 | 针对性验证 |
|---|---|---|
| R1 主导航、深色层次、窗口与键盘可达性 | 已实现 | 隔离 Release 构建通过，0 错误；NU1900 审计网络警告。窄窗图标导航保留中文提示和无障碍名称；实机验证留待整体阶段 |
| R2 工作区卡片、软件图标与预览入口收尾 | 已实现 | 4 条宽度边界测试通过；WPF 编译通过。保存主按钮、窄窗工具条换行、名称截断提示、多显示器瓦片换行；保留既有软件图标与显式屏幕预览。XAML 与用户原有三页布局无法独立暂存，本轮增量暂留工作区 |
| R3 活动筛选、真实详情和数据边界说明 | 已实现 | 6 条已有活动读取器测试通过；WPF 编译通过。详情显示真实日期/数量及来源限制，筛选不可取消为零选中，筛选换行与脚注自适应。页面继续复用用户原有初始化保护 |
| R4 设置分类滚动与窄窗口可达性 | 已实现 | 设置与恢复预览策略 19 条测试通过，WPF 编译通过；分类栏独立滚动，右侧保留全部原控件/事件/保存时机，窄窗提供横向滚动而非截断。设置保存重开留待隔离 UI 验证 |
| R5 中文文案与整体回归 | 未完成：按三次重试限制停止 | 全套 393 条：392 通过、1 失败、0 跳过；失败为刷新后卡片按钮仍为英文。Release 构建 0 错误、2 条 NU1900 警告。未声明交付通过 |

活动限制：只读取已保存工作区和保留检查点，不能推断恢复结果；详情不展示路径、窗口标题、URL、命令行。未执行的桌面恢复、浏览器真实授权和显示器热插拔不记为通过。


### 本轮提交与最终状态

- `6dc4fed` feat: 完善深色主导航与窗口缩放入口
- `5d9ecc1` feat: 完善工作区卡片的紧凑布局适配
- `b3892b3` feat: 补充活动详情与可靠的事件筛选
- `dd6b6f4` feat: 完善设置分类与窄窗口编辑区域
- 完整命令与限制：见 `docs/ui-redesign-acceptance.md`。本轮 R5 未完成，不把上方历史通过记录当作当前通过。
- 起始 TODO 原文完整保留（比较规范化换行后的文本前缀）；3 个用户原有未跟踪辅助类逐字节未变；Activity 原有改动加本轮两处增量精确匹配；原有 XAML 的所有命名控件与 Click/Checked/Unchecked/KeyDown 入口均仍存在。
- 未提交：用户原有改动继续未提交；本轮 SettingsWindow.xaml 增量因依赖用户原有布局无法独立暂存而保留。R5 本地化和主题收尾改动未提交，需继续修复与复验。
- 已停止新增代码修复；需要用户决定是否继续针对模板本地化进行下一轮诊断。


## 2026-10-15 参考图逐区对齐（用户纠正后的新一轮）

上一轮仅做到相似 Fluent 风格，不符合参考图的布局和视觉比例。本轮以 main.png、2.png、setting.png 逐区对照，按真实 WPF 渲染验收。
起始 HEAD：4cfbd66 / main。所有起始未提交内容已记录为基线，保留且不夹带提交。

| 功能 | 状态 | 验证 |
|---|---|---|
| V1 宽导航、标题栏、石墨材质和字号比例 | 已实现 | Release 编译与真实 WPF 导航/缩放用例通过；新增模板期本地化解决前轮中文按钮断言。卡片、活动页、设置页已由后续区块完成 |
| V2 大工作区卡片、应用图标和显示器预览 | 已实现 | WPF 实机与预览存储 3 条测试通过；已查看 1584 宽渲染。大序号/恢复按钮/36px 图标/双显示器预览；导入与全选保留在标题栏更多菜单；有有效截图时默认显示，用户可隐藏 |
| V3 参考图活动时间线 | 已实现 | 真实 WPF 与 6 条活动读取测试共 7 条通过；大蓝色事件图标、104px 卡片、连续时间线、日期分组和较大筛选按钮。只显示事件类别与详情，不虚构恢复结果 |
| V4 参考图设置分类与分组卡片 | 已实现 | 真实 WPF 与设置服务共 14 条通过；系统/恢复流程/浏览器/显示器按参考顺序重组，分类导航独立卡片，原高级设置保留在展开区或后续分类，保存重开断言通过 |
| V5 中文模板修复、交互与整体测试 | 已完成验证 | 全套 393 通过 / 0 失败 / 0 跳过；Release 构建 0 错误、2 条 NU1900。中文白字、宽窗布局、菜单与全选、真实截图默认显示/隐藏、保存与恢复预览打开后取消均通过 |

限定：不使用参考图中的虚构数据；有有效附件时默认显示已保存屏幕截图，用户可隐藏；没有附件时给出重新采集提示。无历史结果时不虚构成功/失败徽标。品牌沿用已有「溯刻 · SuKe」。


### 参考图对齐轮交付记录

- V1 `8a86214` feat: 按参考图重建宽导航与石墨视觉比例
- V2 `53a9933` feat: 按参考图重构大工作区卡片与显示器预览
- V3 `58d9b01` feat: 按参考图重排活动时间线与事件卡片
- V4 `63ebdf5` feat: 按参考图组织设置分类与分组卡片
- V5 `cd83540` fix: 完成参考界面中文细节与交互回归
- 当前验收报告：`docs/ui-redesign-acceptance.md`。前轮本地化失败已修复，测试生成的 PNG 已按用户要求删除。
- 工作区组合验证通过；原有未跟踪辅助类、Loc.cs、ApplicationIconHelper.cs、ActivityPage 两份代码逐字节未变，XAML 原有命名控件与事件入口全部保留。
- 例外：SettingsWindow.xaml 原有布局与本轮重排不可独立暂存，未夹带提交，界面主标记保留工作区；提交依赖原有未提交壳层，不能单独检出后当作完整发布。此轮没有提交用户原有翻译或初始化保护。
- 未做：真实桌面恢复执行、浏览器实际连接操作、启动注册更改、托盘热键和显示器热插拔验收、安装包发布。它们不能由上述通过数替代。
- 构建、TRX 与基线快照保留用于验收/后续安全拆分；测试渲染 PNG 已删除。真实截图用例只把采集图写入自动清理的测试目录。

## 2026-10-15 后续：去掉测试截图并改用真实预览

- [x] 删除旧轮次生成的界面验收 PNG 以及空的测试输出目录；未删除用户提供的 `参考图/` 或仓库原有图标/截图。
- [x] 移除 WPF 用例中的透明 16×9 占位图，不再绘制黑色桌面/窗口框。卡片默认显示 `WorkspacePreviewStore` 中与该工作区 `SavedAt` 匹配的真实截图；没有有效截图时明确提示新建工作区采集。
- [x] WPF 回归不写 PNG 文件；真实采集结果只进 `TestDirectory` 并随测试清理。
- [x] 只读检查本机当前数据：5 个工作区，0 个含 `SavedAt` 匹配的有效屏幕预览附件。
- [x] 真实双显示器截图质量：在交互 Windows 会话中连续 3 轮定向 WPF 测试 + 1 轮全套测试，均采集 2/2 显示器并在卡片和详情弹窗显示真实图片。低分辨率抽样分别观察到 161 与 549/555 种颜色，非黑白占位。
- [x] 全套测试：393 通过 / 0 失败 / 0 跳过；Release 构建 0 错误、2 条 NU1900 网络警告。
- [x] 测试图片：删除之前 8 张 WPF 验收截图和透明测试图。后续真实截图只进入自动清理的 `TestDirectory`，未输出到文件或回复。
- [—] 真实用户数据：只读检查的 5 个旧工作区均无当前有效截图附件；它们会显示空预览提示。需要新建工作区以生成截图；没有替旧工作区写入时间不匹配的画面。

真实截图采集仅在获准的交互 Windows 会话中执行；沙箱内 GDI 无效句柄问题未用于绕过或伪造图像。


## 2026-10-15 新增：放大预览翻页与默认亮色

起始：main / 5e9d02f。仓库无 AGENTS.md；遵循用户规范、CONTRIBUTING.md 和 build.md。
已有未提交的查看器、主题服务、调色板及恢复逻辑均作为用户资产；本轮仅增量实现下列两项。

| 小功能 | 状态 | 验证与提交 |
|---|---|---|
| N1 放大/最大化图片翻页 | 已完成并验证 | 5 条导航边界测试 + 1 条真实双屏 WPF 用例通过；卡片/详情/启动选择框选中页正确，最大化保留，按钮与 ←/→、Page Up/Page Down、首尾禁用和缩放模式均检查；启动预览不触发恢复 |
| N2 默认亮色 | 已完成并验证 | 4 条默认/旧配置/已保存选择测试 + 1 条真实 WPF 用例通过；首次亮色、深浅切换和对比度通过。默认值在用户已有 AppSettings.cs / ThemeService.cs 中增量修改，未整体暂存这些文件 |
| 整体回归 | 已完成 | 完整 Release 测试 406 通过 / 0 失败 / 0 跳过；Release 构建 0 错误、2 条 NU1900 审计网络警告；diff 检查通过 |

提交边界：用户已有未跟踪文件与混合改动不整体暂存；本轮新增模块、测试和 TODO 增量逐功能提交。必要的调用接入若无法安全拆分，保留工作区并在交付时标注。


本轮已提交：`c0aae12` 放大翻页模块与导航测试；`9c090a8` 默认亮色兼容测试。启动选择框接入也已通过真实双屏 WPF 检查。
本轮必须保留未提交的接入：原有 ScreenPreviewViewerWindow.cs、WorkspaceScreenPreviewDialog.cs、WorkspacePreviewControl.cs、StartupWorkspaceDialog.cs、AppSettings.cs、ThemeService.cs、ShellUiTests.cs 都含起始用户内容；仅对其增量修改，没有整体暂存。

补充提交：`1c46661` 覆盖启动截图的最大化翻页。交付记录见 `docs/preview-paging-light-acceptance.md`。
默认亮色保留既有手动主题选择，未修改真实用户配置。测试使用真实采集的双屏图像，未生成或留存 PNG。
