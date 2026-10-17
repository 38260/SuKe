# 活动详情与工作区恢复选择验收

日期：2026-10-17。

## 功能与完成标准

| 编号 | 功能 | 完成标准 | 状态 |
| --- | --- | --- | --- |
| A1 | 活动详情 | 展示工作区、操作类型、时间、真实结果、快照内容与应用分布、显示器配置、记录标识；关闭按钮固定 | 定向18/18通过 |
| A2 | 恢复全选 | 全选/取消全选和单项三态同步；不改变不可选条目的限制 | 定向7/7通过 |
| A3 | 已运行项目跳过 | 可勾选，保持现状；一行提示；不启动、移动或更改其标签页 | 定向77/77通过 |
| A4 | 整体验收 | 定向与完整回归、Release 构建、启动入口检查、人工验收步骤 | 已执行；保留既有测试失败与人工验收边界 |

## 数据口径

- 保存活动展示当前工作区的最新快照，不声称提供完整保存历史。
- 恢复检查点里的内容标为“操作前的桌面快照”，不是目标工作区或已恢复的内容。
- 成功、失败、跳过等数量继续来自真实执行结果；历史结果缺失时显示未知。
- 内容摘要只携带计数、应用进程名、显示器信息，不携带路径、窗口标题、URL 或命令行。
- 原始快照缺失时明确说明，不使用当前工作区猜测历史内容。
- 普通继续恢复（Resume）和按显示器恢复（Selective）保留已确认匹配的运行窗口；仅同名应用运行而目标窗口未匹配时，仍允许恢复缺失窗口。匹配有歧义时仍需用户选择。
- 显式“修复位置”“移动现有窗口”“新开”“切换”“对齐并最小化”等模式继续按其明确操作执行。
- 已运行条目可选，计划里用 `KeepRunning` 标记并排除全部执行动作。关闭预览后直接恢复也应用同一规则；全部选中项目都已运行时允许确认，记录为“全部已跳过”。
- 已跳过窗口对应的浏览器会话（按 LinkedEntryId，旧会话按浏览器类别保守识别）、资源管理器会话和终端启动动作不执行；不再纳入标签页执行计划。已运行窗口仍计入窗口计划与跳过数量，不记成功。

## 验证环境

- .NET 8 / WPF / xUnit，SDK 使用项目已有路径。
- 测试构建输出到 `artifacts/activity-restore-acceptance`。临时 MSBuild 目标取消构建后的根目录复制和权限目标，避免测试覆盖正在使用的程序；正常启动入口仍执行正常 Release 构建。
- 初次写入旧 `out` 目录被权限拒绝，改为新的项目内 `artifacts` 目录。
- 测试输出的 `testhost.exe` 继承低完整性标签，无法监控正常父进程；仅移除本轮生成的宿主外壳，让测试平台经正常 `dotnet.exe` 加载同一 `testhost.dll`。未改被测逻辑、断言或系统配置。

## 定向结果

- A1：`WorkspaceActivityReaderTests`、`ActivityDetailsUiTests`、`RestoreOutcomeTextTests` 合计18通过、0失败。
- A2：`RestoreSelectionUiTests`、`RestorePlanPreviewTests` 合计7通过、0失败。覆盖全选/取消、部分选中、不可选条目、空列表、取消全部后禁用恢复。
- A3：运行跳过策略、真实恢复执行器、结果统计、会话排除、全选与模态审批等共77通过、0失败。外部桌面与进程由既有测试替身隔离，真实执行器与统计逻辑保持原样。
- 模态审批测试在 `Loaded` 中立即关闭会导致 WPF 自定义边框初始化异常；将测试操作排到 Dispatcher 空闲阶段后通过。生产审批逻辑未因测试调整。
- 真实 WPF 布局覆盖浅色100%、深色150%、最小窗口、长列表滚动、固定关闭按钮，以及旧结果缺失提示；摘要隐私和检查点来源单独断言。

## 整体结果

- 最终完整套件：**503通过、1跳过、1失败，共505项**。报告位于本地 `artifacts/activity-restore-acceptance/results/full-final.trx`。
- 唯一失败：`ShellUiTests.Real_wpf_shell_displays_live_screen_previews_and_preserves_workflows_in_isolated_storage`，`IsolatedShellApp` 无法加载 `App.xaml` BAML；与已有验收文档记录一致，该测试宿主本轮未改。不声明完整测试全部通过。
- 跳过：需要显式开启的真实桌面采集测试，本轮未开启。
- 首轮完整测试发现新增默认字段改变旧计划JSON指纹；`KeepRunning=false` 改为不序列化后，原有整对象指纹测试恢复通过，未修改旧断言或降低标准。
- Release测试工程构建0错误，2～3项 `NU1900` 警告为NuGet漏洞源不可达；源码、WPF与测试程序集成功生成。
- 根目录一键入口 `start-suke.bat --dry-run` 实际执行通过。旧实例运行时入口返回2并保留进程；用户从托盘退出后，实际运行 `start-suke.bat`，Release构建0警告0错误，入口退出码0，新进程PID2912实际路径为根目录 `SuKe.exe`。根目录EXE和DLL与标准Release输出分别逐一核对SHA256一致。
- 最终根目录 `SuKe.dll` SHA256：`37CF714B88738F0E3428392D17E5420AD7E48B8F62810EC50F1A18E2BE326AAF`；EXE SHA256：`5056B766E2F14C1F80785792EA7BAB8F13C1798A0BC9939FA4C6806C64856D37`。
- 临时 `BuildOnly.targets` 已清理；保留验收构建与TRX报告。未对用户真实工作区执行恢复，也未将真实桌面恢复链路记为通过；请按下方步骤验收。
- 已有 `.sentry-native/` 原样保留；`todo.md`、`项目记忆.md` 按现有约定仅本地维护。

## 人工验收

1. 正常退出已有 SuKe，从根目录 `start-suke.bat` 启动当前版本。
2. 在活动页打开保存活动详情，检查应用数量、标签页数量及显示器信息；缩小窗口后滚动，关闭按钮应始终可见。
3. 打开恢复活动，核对真实执行统计，并区分“操作前的桌面快照”；老记录没有结果时应明确显示未知。
4. 打开某一个工作区的恢复预览，操作里面应用/窗口条目的全选、取消全选和单项勾选，检查选择状态同步。本轮新增全选位于 `RestorePlanPreviewDialog` 内；外层工作区列表的全选保持原样。
5. 选择包含已运行窗口和待启动项目的工作区；已运行窗口应保持现状，提示显示跳过数量，待恢复项目正常执行。

## 修改文件

- 活动详情：`Models/WorkspaceActivity.cs`、`Services/Storage/WorkspaceActivityReader.cs`、`UI/Shell/ActivityPage.Details.cs`。
- 单个工作区内的恢复选择与执行计划：`UI/RestorePlanPreviewDialog.cs`、`UI/RestorePlanPreviewWorkflow.cs`、`Services/Restore/RestoreApprovalProjector.cs`、`Services/Restore/RestorePlan.cs`、`Services/Restore/RestorePlanPreview.cs`。
- 跳过结果与文案：`Models/RestoreOutcome.cs`、`Services/Restore/RestoreOutcomeProjector.cs`、`UI/Shell/RestoreOutcomeText.cs`、`Localization/LocalizationCatalog.Reference.cs`。
- 正式测试：`tests/SuKe.Tests/ActivityDetailsUiTests.cs`、`tests/SuKe.Tests/WorkspaceActivityReaderTests.cs`、`tests/SuKe.Tests/RestoreSelectionUiTests.cs`、`tests/SuKe.Tests/RunningRestoreTests.cs`。
- 上述产品源码路径均相对于 `src/SuKe`；本验收文档随代码提交，本地工程笔记按既有忽略约定维护。
