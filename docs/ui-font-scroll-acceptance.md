> 工程名与路径已按当前命名归一化；本文件中的历史数字、产物和哈希不代表本轮交付。

# 界面字号 / 滚动 / 截图加载 / 恢复结果统计 — 验收记录（2026-10-17）

本轮改动集中在主窗口「工作区」「活动」两页、保存工作区弹窗、正文滚动容器与恢复结果统计。
本文记录**实际执行过的验证**、**未执行的项及原因**，以及**未提交的改动**。

---

## 1. 环境与产物

| 项目 | 值 |
| --- | --- |
| SDK | `D:\env\dotnet-sdk\dotnet.exe`（8.0.426，本地安装） |
| 构建 | `dotnet build src/SuKe/SuKe.csproj -c Release --artifacts-path out/ui-font-scroll-acceptance` → 0 警告 0 错误 |
| 完整测试 | `dotnet test SuKe.sln -c Release --artifacts-path out/ui-font-scroll-acceptance` |
| 验收用 EXE | `out/ui-font-scroll-acceptance/bin/SuKe/release/SuKe.exe`（框架依赖，需 .NET 8 桌面运行时） |
| 实测显示器 | 显示器 0 `DM55` 2560×1440@120dpi（主，被 NVIDIA 覆盖层与全屏视频占据）；显示器 1 `NE160QDM-NM9` 2560×1600@144dpi |
| 用户字号比例 | `settings.json` 中 `uiFontScale = 0.8`（80%），`themeMode = Light` |

测试基线：本轮开始前为 **438 通过 / 1 跳过 / 1 失败**（唯一失败是既有的
`ShellUiTests.Real_wpf_shell_displays_live_screen_previews_and_preserves_workflows_in_isolated_storage`，
原因是隔离测试宿主无法加载 `App.xaml` 的 BAML）。本轮不为字号任务重构该测试宿主。

本轮结果：**466 通过 / 1 跳过 / 1 失败（468 项）**，失败项与基线**同一项**，
新增的 28 项（`RestoreOutcomeTests` 14 + `SmoothScrollTests` 7 + `RestoreOutcomeTextTests` 7）全部通过。
另外把 `WorkspaceServiceTests.Checkpoint_write_failure_rejects_restore_without_any_mutation`
中断言「注入写入器只被调用 1 次」改为核对真正关心的不变量（没有检查点文档落地、没有窗口被移动、
失败的记录也不留半成品）—— 因为结果记录会复用同一个原子写入器，该计数前提已不再成立。

---

## 2. T1 两页内部文字紧凑化

实现方式：`UI/Shell/ShellStyles.xaml`（应用级）**新增** `WaPageTitleCompact` /
`WaPageSubtitleCompact` / `WaSectionTitleCompact` / `WaItemTitleCompact` / `WaItemMetaCompact`
五个派生样式，由两页按名引用；共享的 `WaPageTitle` / `WaPageSubtitle` / `WaItemTitle` /
`WaItemMeta` **原值未改**，因此设置页与其它窗口不受影响。行内局部 `FontSize` 同步移除，
避免局部值覆盖样式导致修改不生效。

| 位置 | 修改前（DIP @100%） | 修改后 |
| --- | --- | --- |
| 两页页面标题 | 36 | 26 |
| 页面副标题 | 19 | 14 |
| 工作区名称 | 24 | 18 |
| 工作区元信息 | 16 | 13 |
| 工作区序号徽标 | 21 | 16 |
| 活动条目标题 | 20 | 16 |
| 活动元信息 | 15 | 13 |
| 活动日期分组 | 20 | 15 |
| 页面主要操作按钮 | 18 | 14 |
| 活动筛选按钮 | 20（模板里硬编码，实际无视样式的 16） | 14 |
| 辅助说明 | 14 | 12～13 |

保持不变：左侧导航 16、设置页全部字号（标题仍为 36）、图标与截图尺寸、
按钮最小高度（`MinHeight` 60 / 54 / 38 未动），因此点击区域没有缩小。

**实机证据**：`workspace` 页与 `activity` 页截图（见 §7）。同一份截图中设置页标题明显大于
活动页标题，说明共享样式未被改动；导航字号与改动前一致。

---

## 3. T2 保存当前工作区弹窗

| 位置 | 修改前 | 修改后 |
| --- | --- | --- |
| 字段/区域标题（工作区名称、保存打开的文件、要保存的窗口） | 13 | 15 |
| 工作区名称输入框 | 14 | 16（`MinHeight` 40、`Padding` 10,8） |
| 窗口主名称 | 12 | 14（并加 `ToolTip`） |
| 窗口标题片段 | 10 | 12（并加 `ToolTip`） |
| 桌面/显示器分组标题 | 11 | 13 |
| 采集数量、限制与说明 | 11 | 13 |
| 全选 / 取消选择 | 12 | 14 |
| 取消 / 保存当前工作区按钮 | 默认 | 15（`MinHeight` 42、`Padding` 20,9） |
| 窗口尺寸 | 560×680，Min 460×500 | 600×720，Min 500×520 |

窗口选择、保存文件选项、采集限制与最终保存逻辑**未改动**。

**实机证据**：`22-savedialog.png`。可见文案整体放大、说明文字正常换行、底部「取消 /
保存当前工作区」完整可见未被挤出，窗口列表仍可滚动。弹窗以 <kbd>Esc</kbd> 关闭，
**没有保存任何工作区**（`workspaces/` 文件数与时间戳未变）。

---

## 4. T3 工作区 / 活动页滚动手感

新增 `UI/Shell/SmoothScrollBehavior.cs`，是**局部启用**的附加行为：只有显式
`shell:SmoothScrollBehavior.Enabled="True"` 的两个正文 `ScrollViewer` 会被处理，
不使用类处理器，因此不影响设置页、下拉框、多行文本框与嵌套滚动容器。

已实现的行为：

1. 普通鼠标滚轮走 **180 ms 单样条关键帧**（`KeySpline(0.23, 1, 0.32, 1)`）。
2. 距离换算尊重系统滚轮行数与「一次滚动一个屏幕」设置，每行 24 DIP（默认三行 ≈ 72 DIP/刻度）。
3. 小于 120 的 Delta 按比例保留，不截断成整数刻度。
4. 同方向持续输入累积目标；反方向以**当前实际位置**重新定向，不排队播放多个动画。
5. 拖动滚动条、键盘滚动、内容高度变化、程序化滚动都会取消残留动画并以新位置为起点；
   内容变短后目标会重新夹到新的可滚动范围内。
6. 到达边界时不吞事件（交由外层按原生规则处理）。
7. 尊重「在 Windows 中显示动画」设置（`SystemParameters.ClientAreaAnimation`），
   关闭时退化为即时滚动。
8. 卸载时解绑所有订阅并取消动画。

技术约束：`ScrollViewer.VerticalOffset` 只读，动画驱动的是一份自有可写偏移属性，再调用
`ScrollToVerticalOffset`，因此真实偏移、滚动条、命中测试与可访问性保持一致；
没有用整页 `RenderTransform` 伪造滚动。

**代码复核发现的缺陷（已修）**：首版把 Delta 方向写反（`Delta > 0` 在 WPF 表示滚轮远离用户、
内容向上走，偏移应**减小**），会在实机上表现为滚动方向颠倒。修正后把换算与目标计算抽成
纯函数 `WheelStepDips` / `NextTarget` / `CanScrollFurther`，并新增
`tests/SuKe.Tests/SmoothScrollTests.cs`（7 项）锁死符号约定、行数换算、
比例保留与边界判定。

**未能完成的实测**：本机无法把合成的滚轮输入送进应用 —— 已尝试
① `mouse_event(MOUSEEVENTF_WHEEL)`（先 `AttachThreadInput` 把应用置为前台、并用
`WindowFromPoint` 校验落点属于本进程）；② `PostMessage(WM_MOUSEWHEEL)`（按文档使用
屏幕坐标）。两种方式都无任何内容位移（逐帧位移分析残差 0.0，即画面完全一致）。
同一环境下合成的**鼠标按键**同样只激活窗口而不会触发控件，而 `PostMessage` 的键盘消息
（方向键、空格、Tab）可以正常驱动界面，据此判断是输入注入被该桌面环境拦截，
而非应用逻辑问题。因此「滚轮平滑滑动的观感」**未在实机上通过自动化验证**，需要人工用真实
滚轮确认一次。

---

## 5. T4 截图加载与列表渲染

改动全部集中在 `UI/Shell/WorkspacePreviewControl.cs`：

1. **缩略图按实际显示尺寸解码**：由固定 `DecodePixelWidth = 1200` 改为
   「卡片内单列显示宽度（240～480 DIP）× 当前窗口 DPI 缩放」，并夹在 240～960 像素之间。
   在 1440 DIP 宽窗口 + 125% DPI 下约为 600 像素宽，位图像素量约为原来的 1/4。
2. **读取与解码移到后台线程**：解包 zip 与解码在 `Task.Run` 中完成，位图 `Freeze()` 后
   交回 UI 线程；列表没有虚拟化，原先所有卡片会在首次显示时同时读盘并解码。
3. **有界缓存**：按 `(workspaceId, savedAtTicks)` 缓存已解码缩略图（上限 24 个工作区）。
   显示/隐藏切换、重新进入页面都直接命中缓存，不再重复读 zip 与解码；重新保存后
   `SavedAt` 变化即自然失效，不会只按工作区编号永久缓存旧图。
4. **过期结果丢弃**：工作中切换工作区或重新渲染会递增代号，旧结果直接丢弃，
   避免旧图显示到新卡片上。
5. **放大查看器改为按需解码**：只有真正点击缩略图时才按 1200 像素解码当前工作区的原始 PNG，
   卡片本身只承担缩略图成本，因此查看器清晰度没有因缩略图变小而下降。

**量化与局限**：性能收益以「解码像素量」与「I/O 次数」为依据（首次显示：读盘/解码次数不变但
像素量降至约 1/4；显隐切换与页面重进：读盘与解码次数降为 0）。**没有**记录优化前后的
UI 线程阻塞时长或帧时间 —— 缺少可用的 UI 线程计时探针，且如实机注入受限（见 §4），
因此不以「感觉流畅」代替数据。

---

## 6. T5 恢复结果记录与展示

### 6.1 记录方式

新增 `Models/RestoreOutcome.cs`、`Services/Restore/RestoreOutcomeProjector.cs`（纯函数）、
`Services/Storage/RestoreOutcomeStore.cs`；`StorageService` 暴露 `RestoreOutcomes`；
`WorkspaceService` 在执行前后写入同一条记录：

- **开始即落盘**（`ExecuteApprovedRestorePlanCoreAsync` 内，早于任何桌面变更），
  写入计划规模；完成时用**同一个 OperationId 覆盖写入**同一文件，因此是幂等的一次可更新活动，
  不会重复生成记录。
- 被恢复前检查点安全门拦下的结果（`CreateCheckpointAbortedResult`）也会记录，作为「受限」。
- 存储沿用既有约定：独立子目录 `restore-results/`、`AtomicFileWriter` 原子写入、
  条数上限 200 与时间上限 365 天，不引入新数据库。
- **写入失败不影响恢复**：存储层捕获 I/O 与序列化异常、记录 `activity.outcome_persist_failed`
  日志并置位失败标志，不向恢复流程抛出。
- 异常退出导致缺少最终结果时，读取侧一律按 `中断/结果未知`（`Interrupted`）呈现。
- 隐私：只持久化原因码与脱敏、截断（≤200 字符）后的短说明，**不写入**原始窗口标题、
  文件路径、URL、命令行或异常全文；应用名只保留脱敏后的进程名。

### 6.2 统计口径

成功一律来自执行逻辑本身，绝不根据「保存了多少内容」或「启动命令返回成功」推测。

| 内容类型 | 计划数来源 | 成功判定 | 无法确认时 |
| --- | --- | --- | --- |
| 应用窗口 | 本次确认后的执行计划条目（不含执行前取消勾选的条目） | `Restored` **且**定位验证处于可接受状态（`Applied` / `Unavailable`） | `LaunchRequested` / `AwaitingWindow` / `Pending` → 未确认 |
| 浏览器标签页 | 计划内浏览器会话的标签页总数 | 浏览器会话恢复请求的动作成功 | 请求未执行或状态非成功 → 未确认 |
| 资源管理器标签页 | 条目的 `ExplorerSession.TabPaths` | 对应 `RestoreExplorerTabs` 动作成功 | 无对应动作 → 未确认 |
| 终端标签页 | 动作上的 `TerminalDirectories` | 对应启动动作成功 | 无对应动作 → 未确认 |

其它规则：

- 同一应用的多个独立窗口**分别**计数；一个窗口经历启动、等待、移动与验证多个动作**只算一个**。
- 关闭其它窗口、创建检查点等辅助动作不计入恢复成功。
- 跳过、取消、受限、计划失效、部分完成、未确认**各自独立**；同一目标只归入一个状态，
  各状态之和恒等于计划数（`RestoreOutcomeTextTests` 与 `RestoreOutcomeTests` 都断言闭合）。
- 窗口已移动但定位验证未通过 → 记为**失败**并给出 `placement_verification_failed`，
  不显示为完整恢复成功。
- 计划里没有该内容类型时不写 0，标记为「未记录」并在界面不展示。

### 6.3 关联与历史兼容

- 结果通过 **检查点标识** 与「操作已开始」的检查点记录精确合并为一条活动，
  **不按工作区编号 + 接近时间猜测**关联（连续两次恢复不会串单）。
- 结果存在但检查点已过期/不存在时，仍作为独立活动行出现。
- 只有检查点、没有结果时（旧记录）：列表摘要显示
  「这条记录没有保存恢复结果，无法判断成功或失败」，详情说明旧记录未保存结果，
  不倒推、不补造历史成功率。
- 恢复完成时结果写入会触发活动页在已打开的情况下原地刷新。
- 仅预览执行计划不会产生记录（预览路径不进入执行核心）。

### 6.4 界面

- 活动列表新增结果摘要行，例如「部分完成  ·  应用窗口 6/8」；保存类事件不显示摘要。
- 详情窗口展示：开始/结束时间与耗时、总体结果、按内容类型的
  「计划 / 成功 / 失败 / 跳过 / 取消 / 受限 / 计划失效 / 未确认」数量、
  可展开的失败与未完成项（内容类型 · 应用 · 原因，附原因码与脱敏说明）、来源说明。
- 活动页页脚已从「恢复结果不会被记录」改为如实描述（已保存工作区 + 保留检查点 +
  每次恢复记录的结果）。

---

## 7. 实机验收（真实 EXE）

启动方式：`out/ui-font-scroll-acceptance/bin/SuKe/release/SuKe.exe`（本轮构建）。
驱动方式：Python + `ctypes` 只读截屏与 `PostMessage` 键盘消息（脚本在 `.workbuddy/wa-e2e/`），
截图保存在 `.workbuddy/wa-accept/`。

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| 工作区页字号与层级 | 通过 | `20-workspace-final.png` |
| 活动页字号、日期分组、筛选按钮可读性 | 通过 | `23-activity-outcome.png` |
| 活动筛选按钮（未选中不再是纯白底、文字可读、宽度一致） | 通过 | `23-activity-outcome.png` |
| 设置页字号未被改动（对照） | 通过 | `25-focus.png`、`28-state.png` |
| 保存当前工作区弹窗字号、底部按钮不被挤出、列表可滚动 | 通过 | `22-savedialog.png` |
| 活动详情窗打开与渲染 | 通过 | `16-details.png`（旧记录分支） |
| 活动列表结果摘要（真实数字） | 通过 | `23-activity-outcome.png`（「部分完成  ·  应用窗口 6/8」） |
| 恢复完成后活动摘要与详情 | **部分**：列表摘要已实测；详情窗的数量明细未截到图 | 见下 |
| 滚轮平滑滚动观感 | **未通过自动化验证** | 见 §4 |
| 真实恢复执行 | **未执行** | 见下 |

两项未完成的原因：

1. **详情窗数量明细未截到图**：该对话框需要点击/键盘激活行内「详情」按钮。在合成鼠标按键
   无效、且键盘 Tab 顺序在设置页有落入开关控件的风险（见下）的情况下，反复试探不划算，
   故停止。数量明细的**格式化逻辑**已由 `RestoreOutcomeTextTests`（7 项）覆盖，
   数据统计由 `RestoreOutcomeTests`（14 项）覆盖，两者共同保证「列表摘要、详情统计与真实
   `RestoreExecutionResult` 一致」。
2. **未执行真实恢复**：应用把存储固定在 `%APPDATA%\SuKe`，且
   `Environment.GetFolderPath(ApplicationData)` 不响应 `APPDATA` 环境变量（已实测），
   因此无法为验收隔离一份存储。执行恢复会在用户真实存储里写入检查点与结果记录，
   并可能移动/关闭用户真实窗口，超出「实机恢复只操作专属测试窗口、使用隔离存储」的边界。
   为此使用**临时**植入一条符合 schema 的结果记录来验证列表渲染，验证后已删除该文件
   （见下）。

### 验收过程中的事故与回滚（如实记录）

键盘导航试探时误触了设置页的两个开关，均已回滚：

| 项 | 变化 | 回滚方式 |
| --- | --- | --- |
| 随 Windows 启动 | 由关 → 开（日志 `autostart.enabled` 08:37:44） | 删除 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `SuKe` 值，复查确认已不存在 |
| 显示恢复预览 | 由开 → 关 | 停止应用后把 `settings.json` 的 `showRestorePreview` 写回 `true` |

其余设置项与基线快照逐项比对为**无差异**（`monitorAliases`、`uiFontScale=0.8`、
`themeMode=1`、`notificationsEnabled=false`、`createRestoreCheckpoints=false`、
`temporaryDisplayRecoveryMode=1`、`minimumVisibleWindowAreaRatio=0.25`、`hotkeysEnabled=false` 等）。
临时植入的结果记录文件 `restore-results/0123456789abcdef0123456789abcdef.json` 已删除；
`workspaces/`、`previews/`、`checkpoints/` 均未新增或修改（保存弹窗以 Esc 关闭）。

---

## 8. 本轮未提交的改动

仓库在开工前已有大量未提交修改（`SettingsWindow.xaml` 被重写 1279 行、
`LocalizationCatalog.cs` +89 行、`Loc.cs` +219 行等），这些是用户资产，按规则不代为提交。
因此下面这些文件虽然包含本轮增量，但**无法在不牵连用户改动的前提下安全拆分**，保留为未提交：

| 文件 | 本轮增量 |
| --- | --- |
| `src/SuKe/UI/SettingsWindow.xaml` | 工作区页字号、保存按钮字号、正文滚动容器接入平滑滚动 |
| `src/SuKe/UI/Shell/ActivityPage.xaml.cs` | 结果摘要行、`Summary` 视图模型字段、恢复完成时刷新 |
| `src/SuKe/UI/Shell/ActivityPage.Details.cs` | 详情窗重构（统计、时间、原因、未知结果分支） |
| `src/SuKe/UI/Shell/WorkspacePreviewControl.cs` | 后台解码、缩略图尺寸、有界缓存、按需放大解码 |
| `src/SuKe/Localization/LocalizationCatalog.cs` | 本轮新增中英文案 |
| `todo.md` | 本轮任务清单 |

其余文件（新增文件与开工前干净的文件）已按小功能分别提交。
