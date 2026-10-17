> 工程名与路径已按当前命名归一化；本文件中的历史数字、产物和哈希不代表本轮交付。

# 工作区窗口采集完整性验收

日期：2026-10-15。

## 交付状态

本轮采集修复及新增验证完成。全项目发布验收尚未通过：完整测试保留起始即存在的 `ShellUiTests` BAML 测试宿主失败，未修改或跳过这个失败用例。

## 查明的原因

1. 枚举本身使用 EnumWindows，没有“必须前台”的条件；遮挡不是直接筛选项。
2. 旧尺寸筛选使用当前 GetWindowRect；实机专属窗口最小化后仅为 **199×34**，正常恢复尺寸为 **600×400**，会命中旧的 100px 高度过滤。现在使用正常恢复宽高，保存 showCmd=2，采集后仍保持最小化。
3. 旧逻辑直接丢弃空标题。实机中同进程、无标题、独立任务窗口现可正常采集和匹配；菜单、提示框、工具窗口等继续排除。
4. Shell-cloaked 的任务窗口原先必须成功证明其不在当前桌面。现在仅对任务身份明确、cloaking 精确为 Shell 的窗口保留未知结果，界面明确标注“桌面未识别”。应用主动 cloaking 继续排除。
5. 原分组只遍历已识别显示器，无法匹配的窗口会消失。现在使用 Monitor=null 的“显示器未识别”分组，不创建虚假物理显示器。
6. 旧读取失败直接返回 null，保存选择缺少来源复核。现在读取受限候选可见且不能误选；窗口关闭、句柄/PID/身份变化、再次读取失败都会阻止虚假保存成功。

最小化正常边界只用来判断宽高，沿用现有保存坐标约定。WINDOWPLACEMENT 工作区坐标与屏幕坐标的区别参见 [Microsoft 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowplacement)。

## 主要文件

- `Services/Desktop/Native/WindowInventory.cs`、`WindowCapturePolicy.cs`：观测与采集/匹配筛选。
- `Services/Desktop/Native/WindowService.cs`：保留受限候选、记录来源及诊断。
- `Services/Capture/SelectedWindowValidation.cs`、`WorkspaceSnapshotBuilder.cs`：采集前后和落盘前复核。
- `Services/Workspace/WorkspaceService.cs`、`UI/SaveWorkspaceDialog.*`、`SaveWorkspaceWorkflow.cs`：未知分组、选择一致性和数量说明。
- `Services/Capture/WindowCaptureDiagnostics.cs`、`UI/WindowCaptureText.cs`：数量、原因与限制说明；新增日志不含窗口标题、路径、URL 或命令行。
- `Models/WindowRecord.cs`：来源句柄、PID、可采集状态和限制说明均使用 JsonIgnore，不写入工作区。
- 新增 `CaptureCompletenessTests`、`WindowPreviewGroupingTests`、`SelectedWindowCaptureTests`、`WindowCaptureDiagnosticsTests`、`WorkspaceCaptureLiveTests`。

## 验证证据

| 验证 | 结果 |
|---|---|
| 本轮新增非实机回归 | 21 条通过，涵盖任务身份、最小化尺寸、危险操作边界、未知归属、限制诊断、保存一致性 |
| 定向集合（含现有工作区、日志检查） | 61/61 通过 |
| 独立启用真实桌面验收 | 1/1 通过，实际执行 3 秒左右 |
| 完整测试 | **435 通过 / 1 失败 / 1 跳过** |
| Release 构建 | 0 错误；2 条 NU1900，NuGet 审计服务不可达，未关闭审计 |
| git diff --check | 通过 |

完整测试失败与基线相同：`IsolatedShellApp` 无法加载绑定到 App 类型的 BAML。实机项目在默认测试中显式跳过，单独设置开关后已执行通过，没有将未执行的实机检查算为通过。

TRX 位于 `tests/SuKe.Tests/TestResults/`：`capture-baseline.trx`、`capture-t2.trx`、`capture-t3.trx`、`capture-t4.trx`、`capture-t5.trx`、`capture-t6-verified.trx`、`capture-t7-verified.trx`、`capture-enumeration-check.trx`、`capture-live-final.trx`、`capture-full-final.trx`。

实机实际验证了：

- 两个同进程独立窗口同时存在，第二个完全遮挡第一个，且第二个初始标题为空；两个均被真实枚举、采集。
- 第一个最小化后，仍在采集与恢复匹配集合内，原始最小化状态保留。
- 真实列表分组 → 勾选集合 → 采集 → TestDirectory 落盘，2 个选中、2 条保存。
- 创建临时虚拟桌面，仅移动第二个测试窗口；真实观察到 cloaked 与非当前桌面状态，采集保留真实 GUID，恢复匹配仍可找到；随后移回并删除临时桌面。
- 测试 finally 关闭专属窗口，TestDirectory 自动删除。没有运行用户工作区恢复、关闭用户窗口或写入真实工作区；应用原有日志机制会追加本地诊断日志。

## 提交

| 提交 | 内容 |
|---|---|
| 782d998 | 明确范围、TODO、基线契约 |
| 32f5ada | 最小化正常恢复尺寸 |
| 51906d6 | 无标题主窗口和名称回退 |
| 9f504f1 | 桌面识别受限候选 |
| ae00fcd | 未识别显示器分组及调用方 |
| b20d99c | 保存来源与存活复核 |
| 1cecd91 | 受限状态与脱敏诊断 |
| 5e6985e | 原生枚举失败检测 |

实机回归、长期工程说明和本验收文档随 T8 一起提交。

本轮暂存辅助脚本和比较临时文件已清理；初始基线快照、TRX 与 Release 产物保留供验收。对混合修改的 WindowService 额外进行了三方重建核对，原有修改与本轮提交叠加后和当前文件完全一致。

## 验收运行

可运行文件：`out/workspace-capture/artifacts/bin/SuKe/release/SuKe.exe`，保留同目录依赖。该版本基于完整工作区，包含开始时已有的未提交主题、查看器和恢复代码，功能提交不能脱离这些原有文件独立交付。

建议人工检查：保存工作区时，让一个应用被遮挡、另一个最小化、同一应用打开两个窗口，并在其他虚拟桌面保留一个应用；确认列表完整、分组明确，所选窗口数与保存数一致。读取受限候选应显示限制，不能勾选；保存过程中关闭一个选中窗口应明确报错。

## 仍存在的边界

- 没有顶层任务窗口的后台进程不会被伪造成窗口。
- 隐藏到托盘且不满足可见任务窗口身份的窗口仍过滤，不主动激活它们。
- 系统窗口、工具窗口、菜单、提示框和应用主动 cloaking 的辅助表面仍排除。
- 无法读取几何或有效身份的候选只作为受限项显示，不能保存；仅缺少可执行路径时会提示重新启动受限。
- 桌面或显示器未知可以保留并保存，但无法保证恢复到原始未知归属。
- 权限、特定应用框架及更多 Windows 构建仍需相应实机覆盖；不能据两个专属窗口推断所有应用均可完整采集。
