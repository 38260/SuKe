# SuKe — 架构

> 本文为 [docs/architecture.md](../architecture.md) 的中文翻译，面向贡献者与维护者。
> 类型名、方法名、字段名与路径标识符保持英文原文，以便与代码对照。若中英文出现歧义，以英文原文为准。

本文档描述 SuKe 当前的开发架构。最新已发布的基线是 v1.7.0；实现状态记录在 [implementation-status.md](implementation-status.md)。

---

## 原则：每层只承担一项职责

```
┌─────────────────────────────────────────────────────────────────┐
│  UI 层            App.xaml.cs · SettingsWindow · 各类对话框      │
│  (WPF, 托盘)      不持有业务逻辑。调用 Coordinator。             │
├─────────────────────────────────────────────────────────────────┤
│  Coordinator      LayoutCoordinator · 预览工作流                 │
│                   选择手动/自动恢复的入口点，                    │
│                   并持有面向用户的恢复通知。                     │
├─────────────────────────────────────────────────────────────────┤
│  Services         WorkspaceService · RestorePlanner/Executor    │
│                   WindowService · Monitor/Storage 等服务         │
│                   纯决策 + 显式的 I/O 边界。                     │
├─────────────────────────────────────────────────────────────────┤
│  Models           WorkspaceSnapshot · WorkspaceEntry            │
│                   RestorePlan · 执行/预览结果                    │
│                   不可变意图与纯持久化数据。                     │
├─────────────────────────────────────────────────────────────────┤
│  Native           NativeMethods.Window · NativeMethods.Display  │
│                   全部 P/Invoke 声明。无逻辑。                   │
└─────────────────────────────────────────────────────────────────┘
```

服务端源码按「归属」组织，而不是塞在一个扁平目录里：

| 目录 | 职责 |
|---|---|
| `Services/AppAdapters` | 应用专属的采集、身份识别、启动、就绪与校验策略。 |
| `Services/Browser` | Chromium/Firefox 连接器、浏览器家族路由、URL、PWA 与原生消息集成。 |
| `Services/Capture` | 快照构建、资源发现、标题解析与 Jump List。 |
| `Services/Desktop` | 显示器拓扑、窗口清单/变更、放置几何与救援。 |
| `Services/Restore` | 纯规划、匹配、执行阶段、诊断投影与仿真。 |
| `Services/Storage` | 原子持久化、仓储、迁移、别名、传输与同步传输层。 |
| `Services/System` | 设置、日志、启动、热键、新手引导与可执行文件重绑定。 |
| `Services/Workspace` | 采集/恢复门面、检查点、变体、切换与协调。 |

---

## 各服务职责

### `MonitorService`

负责与物理显示器相关的一切。

- **`GetCurrentMonitorFingerprint()`** —— 调用 `QueryDisplayConfig` 枚举活动显示路径，为每台显示器提取 EDID 厂商 ID + 产品代码 + 连接器实例，排序、拼接后返回 SHA-256 哈希的前 8 个十六进制字符。该哈希是稳定的：分辨率或刷新率变化时不变，只有物理显示器集合变化时才变。
- **`GetCurrentMonitors()`** —— 返回 `List<MonitorInfo>`，每个条目包含稳定的 `MonitorId`（与指纹相同的 EDID 格式）、来自 `DisplayConfigGetDeviceInfo` 的友好名称、来自 `EnumDisplayMonitors` 的完整虚拟桌面边界与「考虑任务栏的工作区」、有效 DPI，以及是否为主显示器。
- **`GetMonitorForWindow(hWnd, monitors)`** —— 静态辅助方法，调用 `MonitorFromWindow` 把活动 HWND 映射到传入列表中的某台显示器。

### `WindowInventory` 与窗口策略

`WindowInventory` 调用 `EnumWindows`，返回不含策略的 `ObservedWindow` 事实：HWND、PID、所有者、可见性、类名、标题、边界、可执行文件/进程身份，以及 AUMID。枚举本身不判断某个窗口是否「可以安全采集或变更」。

`WindowPolicyEvaluator` 在每个消费方边界上施加**一条**具名策略：

- `CaptureCandidate` —— 保留历史遗留的保存排除规则：不可见、被拥有（owned）、Shell chrome、空标题，以及过小的工具窗口。
- `RestoreMatchCandidate` —— 为匹配筛选出同样「具备布局形状」的窗口。
- `SwitchCloseCandidate` 与 `MinimizeCandidate` —— 保留历史布局排除规则，并额外排除 SuKe 自身进程。
- `SwitchRiskCandidate` —— 有意更宽泛：安全切换预检可以检查被拥有的对话框与小的/无标题的临时窗口，但不允许这些窗口进入已保存的工作区。

策略层不包含任何第三方应用、进程或标题黑名单；只有已知的 Windows Shell chrome 类名保留为平台级排除项。`WindowInventory` 会观测扩展样式、DWM cloaking、归属关系、根归属关系，以及当前可见的所有者链代表窗口。随后采集、恢复匹配、关闭与最小化都只选取「可见、未被 cloak、可独立管理的任务窗口」：排除 `WS_EX_TOOLWINDOW`；排除 `WS_EX_NOACTIVATE`，除非 `WS_EX_APPWINDOW` 显式把该窗口纳入任务栏行为；排除普通的 owned/transient 窗口。该「代表窗口」是诊断证据而非硬性闸门，因为微软将 Alt+Tab 的确切算法明确定义为实现细节；因此当临时模态弹窗打开时，独立的根窗口仍会被采集。`SwitchRiskCandidate` 保持更宽泛，从而让普通的 owned 对话框依然能保护未保存的工作。

进程清单独立于「过滤后的窗口清单」。因此，若某条历史条目对应的进程已在运行，但没有暴露任何合格、未被分配的面向用户窗口，则该条目会以「已解释并排除」的方式呈现，而不是被重新启动或占用一个就绪超时。窗口多重性在采集时被保留——没有针对特定产品的去重——而恢复会话的「每条目一个 HWND」不变式负责安全分配。托管窗口只有在同一包族内暴露相同 AppUserModelID 时才允许跨进程边界；匹配绝不会使用「仅凭标题」的宿主例外。

### `WindowService`

对原始清单施加显式策略，并负责活动窗口的富化/变更。

- **`SnapshotWindows(policy, monitors?)`** —— 用具名策略过滤原始观测，为选中的窗口补充放置/浏览器/文件夹数据，并可选地分配显示器。
- **`GetWindowsWithPids(policy)`** —— 返回按策略筛选后的 `HWND → (PID, WindowRecord)` 字典，供恢复匹配使用。
- **`InspectUserWindows(SwitchRiskCandidate)`** —— 提供原始的安全切换预检风险，包括会被采集排除的 owned 保存对话框。
- **`RequestCloseUserWindowsExcept(SwitchCloseCandidate, keep)`** —— 只向无关的布局候选窗口投递 `WM_CLOSE`，并返回切换引擎必须追踪的确切 HWND 集合。宽泛的 `SwitchRiskCandidate` 观测仅作为预检诊断，永不控制存活判定。
- **`MinimizeUserWindowsExcept(MinimizeCandidate, keep)`** —— 保持变更策略显式。
- **`RestoreWindow(hWnd, record)`** —— 先调用 `SetWindowPlacement`，对最大化的窗口再补一次 `ShowWindow(SW_MAXIMIZE)`，以确保最大化状态应用在正确的显示器上。
- **`IsWindowAlive(hWnd)`** —— 在恢复会话释放某个「HWND 已从用户窗口清单中消失」的分配之前，执行一次不过滤的原生存活检查。
- **`RescueForegroundWindow(...)`** —— 通过原生边界读取当前活动 HWND，仅当其恢复后边界中「可见于任一当前工作区」的比例低于配置阈值时，才施加 `OffScreenWindowRescueGeometry`。最大化状态会被保留。

### 应用适配器（Application adapters）

`IAppAdapter` 把应用专属行为挡在共享的规划器/执行器策略之外。注册表会向适配器询问：采集富化、稳定身份证据、启动决策、就绪策略与放置校验策略；而匹配安全性、HWND 归属、资源校验与变更仍属于共享边界。

- `ChromiumWebAppAdapter` —— 采集已安装 PWA 的快捷方式/回退元数据，并规划其启动。
- `DedicatedBrowserWindowAdapter` —— 把保存的 URL 规划为一个显式的新浏览器窗口。
- `ExplorerFolderAdapter` —— 在启用文件/文件夹采集时，采集活动文件夹以及有序的多标签页会话。`ExplorerTabSessionService` 在一条专用 STA 线程上执行 Shell 自动化，并且只在被分配的 Explorer HWND 就绪后才补齐缺失的标签页。
- `GenericWindowsAppAdapter` —— 当没有专用适配器接受该条目时，保留通用的可执行文件、文档/工作区、MSIX 激活与普通 Win32 回退行为。

注册表由内置适配器构成。WA-016 不加载任意的第三方程序集。

### 窗口身份与匹配

`WindowIdentityExtractor` 是唯一的翻译边界：把持久化的 `WorkspaceEntry` 与运行时的 `WindowRecord` 数据转换为 `SavedWindowIdentity` 与 `LiveWindowIdentity`。已保存身份只包含稳定证据；HWND 与 PID 只存在于活动模型上。

`WindowMatcher` 是覆盖纯内部 `WindowMatchResolver` 的稳定公开门面，把每个活动窗口返回为一个带评分的 `WindowMatchCandidate`，并附机器可读的 `WindowMatchEvidence`。匹配从精确的稳定身份开始（PWA 或打包应用的 AUMID、专用浏览器站点），依次经过文档/项目证据、通用可执行文件/类名/标题信号，最后才是弱的显示器/几何上下文。结果按「是否合格、评分降序、HWND 升序」排序，因此并列候选依然可见，同时呈现保持确定性。

`WindowMatchPolicy` 持有最低标题/几何/评分阈值、强置信度边界、第一名与第二名的歧义裕度，以及学习提示的加成。`WindowMatcher.Resolve` 返回 `Exact`、`Strong`、`Probable`、`Ambiguous` 或 `Missing`；歧义结果有意不包含任何选中候选。兼容性检查与启动后对账使用同一个解析器，因此二者都无法退化为按 HWND 排序。

学习到的选择（自设置 schema v3 引入）以稳定的工作区与条目 GUID 为键。其 `WindowIdentityHint` 把可执行文件/类名或更强的 AUMID、包、PWA、浏览器站点或文件夹证据，与次级标题词元组合起来。HWND/PID 永不进入持久化模型，标题也不能被单独保存为身份。匹配提示贡献显式证据与评分；它不会绕过「种类、可执行文件或最低合格性」检查。

`RestoreAssignmentPlanner` 持有一次规划会话的「已消耗 HWND」集合。它应用学习提示、把候选与证据投影进恢复计划、只提交无歧义的选择，并保护处于未决歧义裕度内的每个候选。任何执行器阶段都不会独立地重新分配一个已被复核的活动窗口。

### 纯恢复规划

`RestorePlanner` 从四个显式输入构建一个不可变的 `RestorePlan`：已保存的 `WorkspaceSnapshot`、专门构建的 `RestoreLiveInventory`、已观测得到的 `RestoreMonitorTopology`，以及一个 `RestoreMode`。它不执行任何窗口、进程、浏览器、文件系统或持久化操作。

每条已保存条目都会作为 `RestorePlanEntry` 保留下来——包括被选择性恢复排除的条目，或被取消操作中断的条目。每个条目记录：带评分的候选及其身份证据、确定性的选中匹配、DPI 感知的目标放置、启动需求、未来的 `RestoreAction` 描述、警告、阻断错误，以及一个有解释的最终结果。候选 HWND 在整个构建过程中被消耗，因此两个条目无法认领同一个活动窗口。

缺失与过期的资源以「观测」的形式传入，并转化为阻断性结果而非异常。浏览器会话不可用会转化为显式警告并附带普通启动回退。`RestorePlan.Redact()` 与 `ToRedactedJson()` 生成深度脱敏的投影，供诊断或预览使用。

`RestorePolicyResolver` 在规划期间「恰好一次」地把一个显式工作区模式与持久化的 `WorkspaceEntry.RestorePolicy` 组合起来。Resume 是兼容性默认值：复用匹配到的窗口，并启动缺失的条目。Repair 只为「已存在但保存的精确几何不一致」的窗口发出放置动作（自适应几何会被保守地修复）；Move Existing 从不启动；Launch Fresh 优先使用一个不同的窗口；Exact Switch 使用 Resume 的条目行为，外加协调器的关闭阶段；Preview Only 保留描述性动作，但在检查点或变更之前就会被拒绝。选择性恢复与 Align/Minimize 仍是 Resume 行为的兼容性包装。

按条目的覆盖可以强制：复用、缺失则启动、总是启动新窗口、从不启动、从不关闭，或切换期间忽略。只有具备显式「独立新窗口启动契约」的条目才能保证全新启动；目前这指的是以 `--new-window` 启动的专用浏览器 URL。不受支持的请求会产生 `UnsupportedAlwaysLaunchNew`，并安全地复用最佳已有匹配。对于受支持的全新启动，计划会记录既有的合格 HWND，使就绪阶段不会把旧窗口误认为新请求的窗口。

`PackagedAppResolver` 把 `WindowsApps` 可执行文件视为「带版本的观测」而非持久的启动身份。它从保存的包全名推导出包族、找到当前已注册的包、从 `AppxManifest.xml` 读取匹配的应用 ID，并提供 `PackageFamilyName!ApplicationId` 作为可用的打包应用资源。如此，即使某个旧工作区保存的可执行文件目录已被替换，它仍可通过 `shell:AppsFolder` 激活更新后的包。当打包应用的子进程不直接暴露 AUMID 时，采集也使用同一个解析器。

`VersionedExecutableResolver` 处理对应的经典桌面更新场景，且不接受来自工作区数据的通配路径。当某个精确可执行文件缺失时，它只识别「包含 `Update.exe` 的 Squirrel 安装根」，要求保存的父目录是 `app-<version>` 形式，只枚举同级的直接版本目录，并选出「包含完全相同可执行文件名」的最高解析版本。解析出的具体路径会被嵌入不可变计划，并在启动前立即重新校验。

### 语义化与相对显示器的放置

工作区 schema v7 保留了历史遗留的绝对普通矩形与独立的 `ShowCmd`、源显示器边界/工作区/DPI、`NormalizedWindowLayout`、按拓扑区分的布局变体，以及显式的 VS Code 工作区种类。采集会推导相对于工作区的 X/Y/W/H、水平与垂直锚点，并识别出可辨识的「整屏、左/右半屏、上/下半屏、三分、居中或自定义」布局。

`RestoreMonitorTopology.IsExactMatch` 要求稳定 ID 的顺序、虚拟边界、工作区与 DPI 全部相同。纯规划器只在该情形下保留精确像素。任何几何、工作区、DPI、方向或显示器集合的变化，都会在映射到的显示器上选择语义化/归一化适配。没有归一化数据的旧快照保留 DPI 缩放，但同样经过工作区钳制，因此适配不可能产生完全离屏的矩形。`ShowCmd` 在普通边界之后应用，并与最大化/最小化状态保持独立。

对普通窗口，采集还会观测 `DWMWA_EXTENDED_FRAME_BOUNDS`，并使用这些可见边界作为归一化/语义几何。精确拓扑下仍保留原始 `WINDOWPLACEMENT` 矩形。适配后的语义目标以「可见边界」表达；原生变更边界在调用 `SetWindowPlacement` 之前会先减去/加上活动窗口不可见的调整边框内衬，校验时则比较得到的可见边框。这样可避免常见的 Windows 边框宽度在显示器边缘变成可见缝隙。

### 恢复计划的执行与过期检测

`RestoreExecutor` 是唯一把已批准的 `RestorePlan` 变为进程、浏览器或窗口变更的边界。它只执行该计划中已存在的动作，且全部经过可注入的「进程启动、浏览器会话、窗口清单/变更、资源校验、就绪探测与时钟」边界。浏览器恢复本身是一个显式的计划动作；它的普通浏览器回退也是显式的，并以「该动作变为不可用」为条件。

执行器门面按顺序调用固定的内部阶段对象：

1. `RestorePreflightPhase` —— 在变更之前校验候选清单、选中的 HWND、启动资源与浏览器能力。
2. `RestoreBrowserAndLaunchPhase` —— 执行已批准的浏览器动作、定位已存在的窗口、执行已批准的启动，并负责最终的最小化。
3. `RestoreReadinessPhase` —— 只把「成功且相关的活动」与新观测到的稳定、可响应窗口关联起来。
4. `RestorePlacementVerificationPhase` —— 重新观测分配结果并执行有界重试。
5. `RestoreResultAggregator` —— 从共享的 `RestoreExecutionContext` 生成有序的公开条目/动作结果。

所有阶段共享同一份「已分配 HWND」集合与同一个 `RestoreWindowRevalidator`。它们不跨阶段边界缓存原生清单，也不启动并行变更。

在每次放置之前，执行器会再次枚举合格窗口，并校验已批准的 HWND、PID 与已保存身份。在每次启动之前，它会重新校验已批准的文件、文件夹、URL、可执行文件或打包应用身份。已关闭/被复用的 HWND 与已变化的资源会产生结构化的 `RestoreExecutionResult`「计划过期」结果，且不会收到任何变更。已批准的分配在整个会话范围内有效。在已批准的启动之后，执行器使用下述就绪引擎；不再存在固定的恢复休眠。

候选集合的过期检测是有方向性的：新出现的合格 HWND 未被复核过，因此使计划失效；而未选中候选的消失是安全的。后者是工作区切换所必需的，因为切换自身的已批准关闭阶段会有意在目标计划执行前移除无关候选。而**被选中**的 HWND 消失或被另一个 PID 复用，仍然会使计划失效。

### 虚拟桌面归属

虚拟桌面归属是一个显式的、默认关闭的 Windows 10/11 偏好项。采集时，`VirtualDesktopAssociationService` 只调用有文档的 Shell 方法 `IVirtualDesktopManager.GetWindowDesktopId(HWND)` 与 `IsWindowOnCurrentVirtualDesktop(HWND)`。常规策略依然拒绝每一个被 DWM cloak 的界面；只有一条狭窄的 opt-in 路径会接纳某个窗口，当且仅当 Windows 报告它不在当前桌面、DWM 确认属于 Shell cloaking，并且在移除 cloak 后同一套归属/样式/标题/边界任务窗口检查依然通过。被应用 cloak 的、继承的、工具的、Shell chrome 的与非可激活的界面仍被排除。Save 对话框按观测到的桌面 GUID 与显示器对结果窗口分组；由于受支持的接口不暴露名称或顺序，它会把这些组标记为「当前桌面」或「确定性的非活动桌面」，而不会去猜 Windows 界面上的编号。

同一套狭窄的清单路径也用于恢复匹配，因此非活动桌面上的窗口会被复用而不是被重复打开。规划时，一个有效的 GUID 只为「被安全选中的活动匹配」产生一个 `MoveWindowToVirtualDesktop` 动作。一个显式的恢复预览匹配会保留该 GUID 并收到同一个动作。

执行时，在调用有文档的 `IVirtualDesktopManager.MoveWindowToDesktop(HWND, GUID)` 之前会再次校验 HWND。该服务从不调用未文档化的接口，也没有任何创建、枚举、命名或切换桌面的代码路径。GUID 缺失、被拒绝、不受支持或不可用时，产生的是动作级的 `Skipped` 结果；它不会让恢复失败，也不会隐藏或关闭窗口。这一边界遵循微软的指引：应用不应自动在桌面之间切换用户。

### 应用就绪

`SystemAppReadinessProbe` 每次轮询采集一份共享的只读观测：合格的活动窗口、正在运行的进程名，以及 HWND 的响应性。它有意避开 `WaitForInputIdle`，因为该方法对 UWP/MSIX 与多进程浏览器应用并不可靠。

`AppReadinessEngine` 把这些事实与既有的「歧义安全」`WindowMatcher` 以及逐条目的稳定性记忆结合起来。通用策略报告 `NotStarted`、`ProcessStarted`、`WindowFound`、`Ready`、`TimedOut` 或 `Failed`。一个窗口只有在「可被安全分配、可响应，并且在配置的观测次数内保持了相同的 HWND/PID/标题/类名/边界签名」时才变为通用就绪。

`AppReadinessStrategyRegistry` 选择第一个「认领该已保存身份」的注入式 `IAppReadinessStrategy`，否则回退到 `GenericAppReadinessStrategy`。适配器可以替换通用就绪规则，但不能选择不同或歧义的窗口：匹配仍由 `WindowMatcher` 持有，且「`Ready` 却没有选中候选」会被判为 `Failed`。

执行器以 250 ms 间隔轮询共享观测中的每个待定条目，使用 45 秒的单调墙钟超时，并在每次等待时支持取消。在轮询之前，每次等待都必须与「同一 条目/稳定应用身份 的一次成功启动」或「其成功的浏览器会话动作」相关联；无关的启动不能启动被动超时。探测/枚举工作计入该时间预算，而不是只计入请求的延时。条目被独立追踪：一个就绪的条目会被立即重新校验并定位，而较慢的条目继续轮询。超时与失败状态被携带在结构化的动作/条目结果中；隐私安全的诊断事件记录稳定的条目 ID、策略与超时，而不含窗口标题或路径。

### 恢复后的放置校验

在每个「初始匹配成功」或「新近就绪」的窗口都完成定位之后，`RestoreExecutor` 运行一个有界的校验阶段。`SystemWindowPlacementProbe` 重新读取被分配的确切 HWND 的普通边界、可见 DWM 边框、`ShowCmd` 与 DPI。`WindowPlacementVerifier` 把「不可变目标所使用的坐标表达」（精确放置用外边界，语义适配用可见边框）与一个「按目标 DPI 缩放、默认 8 像素」的容差相比较，并把观测分类为 `Applied`、`Settling`、`Rejected`、`MovedByApp` 或 `WindowGone`。

所有已被分配的窗口共享同一个等待阶段，因此校验只是增加一个短暂的稳定间隔，而不是为每个窗口各加一次延时。不匹配会针对原始 PID 与已保存身份重新校验，然后同一个已分配 HWND 最多接受两次纠正尝试。已关闭、被复用或不再合格的 HWND 绝不会被悄悄替换。变更后的即时观测能区分「应用事后移动了窗口」与「应用从未接受该放置」。

`IWindowPlacementVerificationStrategy` 可以为已知的应用家族覆盖延时、容差与重试次数；匹配与 HWND 归属仍留在适配器之外。校验随恢复调用一并结束，永不成为一个后台布局守卫，因此用户之后的移动不会与之对抗。条目/动作的执行结果携带最终状态、重试次数、策略与容差；校验失败会把整体结果变为 `CompletedWithFailures`，并汇总到面向用户的恢复警告与隐私安全的结构化日志中。

### 文件资源管理器标签页对账

Windows 11 通过 `Shell.Application` 自动化暴露资源管理器标签页，但没有公开受支持的会话恢复 API。`ExplorerTabSessionService` 按顶层 `CabinetWClass` HWND 对逐标签页的 Shell browser 对象分组，通过 `IShellBrowser` 取得每个 `ShellTabWindowClass` 句柄，并记录所有文件夹位置以及激活标签页。采集与恢复的 COM 工作都在同一条后台 STA 线程上串行化，因此 UI、测试与 worker 调用方观测到相同的标签页句柄。

在启动就绪与放置校验之后，执行器重新校验被分配的 Explorer HWND。它把匹配的位置当作多重集合来消耗，只打开缺失的出现次数，通过各标签页自己的 Shell browser 对象导航，并重新选中保存时的激活项。它不会关闭无关标签页。文件夹缺失或 HWND 改变/被替换会产生一个带类型的部分失败，而不是全局键盘输入或破坏性对账。标签页路径只在用户启用文件/文件夹采集时才被包含，在诊断计划中会被脱敏，并在「可移植-脱敏」导出中被移除。

### 恢复预览与审批

`RestorePlanPreviewBuilder` 把不可变计划投影成「精确、适配、歧义、缺失、就绪、跳过、取消」等条目状态，以及「移动、启动、浏览器、等待、最小化、无变化」等动作标签。它从不读取活动状态。`RestorePlanPreviewDialog` 只渲染这份投影，使用可聚焦的条目复选框与自动化标签，并区分阻断错误与破坏性的最小化动作。预览中会显示所选模式与解析后的逐条目策略。Preview Only 总会打开该对话框，其唯一的终止动作是关闭预览；执行器会独立拒绝「仅预览」计划。在切换模式下，对话框还会说明：已批准的目标 HWND 会被保留，而无关窗口在批准后会收到正常关闭请求。

对于歧义条目，投影还只携带关闭候选集合，包含标题、进程/类名、显示器、边界、置信度、评分与有解释的证据。单选按钮选择会调用 `RestorePlanner.ResolveAmbiguousMatch`，它校验会话级的 HWND 归属并推导出一份新计划，而不观测桌面。可选的「记住此选择」数据只在已批准的执行成功完成后才持久化；Settings 可以清除全部学习到的选择。

取消勾选某个条目会调用 `RestorePlanner.DeriveApprovedPlan`。推导出的计划保留原有候选证据与放置，但移除该条目的动作与阻断错误，且不改变预览对象。它选中的 HWND 会免于对齐/最小化。禁用某个「普通浏览器」条目还会移除全局浏览器会话动作，并使已启用的浏览器回退变为显式，从而防止连接器重建一个已被禁用的条目。

托盘、Settings 与热键的手动恢复都经过 `RestorePreviewPolicy`。当 `AppSettings.ShowRestorePreview` 启用时，它们照常显示复核对话框。禁用时，可执行的计划直接继续；受阻或歧义的计划仍会打开对话框，因为它需要用户禁用一个条目或选择一个候选。已批准的计划会传给 `ExecuteApprovedRestorePlanAsync`。
在任何变更之前，执行器会把当前的合格候选清单与预览相比较，并重新校验每个被引用的资源。状态变化会返回 `StalePlan`、显示清晰的「请重试」提示，并且绝不会被悄悄重新规划。切换命令在进入关闭阶段前使用同一个预览。启动与显示变化触发的恢复保留其「一键」兼容路径。

### 单飞（Single-flight）工作区切换

`WorkspaceSwitchEngine` 负责切换的取消与串行化。一次新的已批准切换会取消上一次调用并等待其租约，防止轮询/通知循环重叠。该引擎保留「被已批准 `RestoreExistingWindow` 动作引用的 HWND」，以及「受 never-close、ignore-during-switch、禁用条目或歧义策略保护的句柄」；向其他关闭候选投递 `WM_CLOSE`，并且只对返回的「请求句柄集合」执行不过滤的 HWND 存活轮询。两分钟上限由 `Stopwatch` 墙钟时间计量。数量变化留在结构化日志中，而用户等待通知被限流为每 30 秒最多一条。当请求集合清空后，执行那份被精确复核过的计划；owned/transient 风险窗口绝不会仅仅因为对预检可见就让切换一直挂起。

`PersistentApplicationPolicy` 增加一个全局保护来源，而不引入另一套恢复或放置模型。Settings 存储归一化的可执行文件名和/或 AppUserModelID。匹配到的活动身份会跨工作区继承既有的 never-close 行为，被移出「无关窗口风险计数」，且绝不按标题匹配。

`LayoutCoordinator` 把整个切换——包括关闭阶段——包在共享的恢复事务闸门中。Exact Switch 总会先把当前桌面变为持久化状态，之后 `WorkspaceSwitchEngine` 才能发出第一个 `WM_CLOSE`；常规的非破坏性操作则遵循检查点偏好。同一个闸门即使在常规采集被禁用时也能防止变更重叠。一次取而代之的切换会取消更早的事务令牌，无论更早的请求仍在采集中还是已在等待窗口关闭。

手动恢复/切换工作流在一个非模态、可取消的进度窗口中显示 `RestoreProgressReport`。检查点采集、浏览器工作、启动、就绪、关闭轮询与放置校验会报告不同的阶段；服务/执行器通过 `IProgress<RestoreProgressReport>` 保持与 UI 无关。UI 侧的定时器在两次报告之间推进已用时间显示，因此一次漫长的同步观测或资源读取仍会可见地计时，而不是看似卡在 `0:00`。

### 事务化检查点与撤销

每个可执行的面向用户恢复计划都会进入 `WorkspaceService` 的单一事务闸门。`AppSettings.CreateRestoreCheckpoints` 控制这个串行化操作是否为常规的 Repair、Move Existing、Resume 与自适应恢复执行检查点采集。默认是禁用。Exact Switch 与 Undo 总是需要检查点，因为它们会关闭或替换桌面状态。被跳过的常规检查点会记录一个 `Disabled` 结果，并在同一个单飞闸门下继续，没有采集延迟，也不产生新的撤销点。
`CaptureWorkspaceAsync` 记录当前窗口、放置/状态、启动资源元数据、显示器拓扑与可选的浏览器会话元数据；它绝不采集截图或文档内容。随后 `CheckpointRepository.Save` 是持久化边界。除非检查点负载已被原子提交，否则不会调用已批准的执行器（或切换的关闭阶段）。

检查点采集使用「快速恢复档」：标题解析、资源管理器文件夹路径、PWA 身份与浏览器元数据仍可用，但跳过所有 Jump List 解析以及递归的 Documents/Desktop/Downloads/OneDrive 回退。具名工作区保存保留完整的 Jump List 发现，但所有 Tier-3 文件夹遍历共享一个 5 秒的累计预算并支持取消。这避免检查点创建因为花几十秒扫描无关或锁定的文件，而让一份已被复核的 HWND 计划变过期。

`RestoreCheckpointOutcome` 被附着到结构化的执行结果上。持久化失败会产生一个被拒绝的结果，且零原生/进程/浏览器变更；提交前的取消产生一个已取消结果，且零变更。执行器随后的失败会留下已提交的检查点可用。

“Undo Last Restore” 选择最新的健康、未过期检查点，用 Exact Switch 策略构建同一份恢复计划，并让它走工作区切换的对账路径。因此不在检查点中的窗口会被正常关闭而不是被留下，而保存的窗口使用同一套过期检测、就绪、放置与校验阶段。Undo 使用 `Undo` 触发器，并在其第一次关闭请求之前提交「正被替换的状态」，从而使「撤销的撤销」保持可能。由于 Undo 是破坏性对账，其撤销前检查点是强制的，即使常规检查点采集被禁用。

### Settings UI 的归属

`SettingsWindow.xaml` 保持为单一可视化树，从而使 WPF 名称作用域、路由事件、键盘焦点、主题资源与 DPI 行为都不改变。其后台代码是一个按功能拆分的分部类：根生命周期/浏览器/通知界面、启动/默认工作区行为、热键录制、恢复工作流偏好、显示器别名，以及工作区列表操作。绑定的行模型仍在 `SettingsRows.cs` 中，托盘与 Settings 的保存命令都继续经过 `SaveWorkspaceWorkflow`。

`HelpReferenceWindow` 是一个独立、可复用的 WPF 界面，而不是第二棵设置树。一次全新的交互式安装会打开它一次，并提供直接的 Save Workspace、Settings 与 Dismiss 动作。`FirstRunOnboardingPolicy` 会在「最小化启动、设置不可读、已包含工作区的安装」这些情形下抑制它；而原生消息启动会在交互式服务或 UI 被创建之前就退出。完成标记在呈现之前提交，以防止重复启动刷屏。同一个窗口仍可从托盘与 Settings 到达，其中包含关于恢复模式、逐条目策略、预览/检查点设置、匹配、隐私与已知应用限制的「面向用户契约」。

### `WorkspaceService`

主采集/恢复编排服务。由 `LayoutCoordinator` 调用，也被 UI 代码直接调用。快照构建与持久化被有意设计为彼此分离的边界。

- **`CaptureWorkspaceAsync(...)`** —— 可复用的采集操作，服务于具名工作区、检查点与临时采集。`WorkspaceSnapshotBuilder` 获取当前拓扑、枚举窗口、过滤选中的显示器，并通过 `CapturedWindowEntryFactory` 与 `CaptureResourceResolver` 构建条目，期间不持久化。随后 `WorkspaceCaptureBuilder` 请求可选的浏览器元数据、附上返回的会话，并返回一个 `WorkspaceCaptureResult`。浏览器状态是显式的：`Captured`、`Unavailable`、`TimedOut`、`Skipped` 或 `Failed`。
- **`PersistCapture(result, destination, policy)`** —— 一次采集唯一的最终提交。调用方选择具体的仓储类型，并显式允许「仅窗口的部分保存」或要求「完整的浏览器采集」。采集在浏览器采集完成之前绝不持久化。
- **`RestoreWorkspaceWithExecutionResultAsync(snapshot, mode, token)`** —— 自动恢复流水线：
    1. 在不做变更的前提下观测活动窗口、显示器拓扑、资源与浏览器会话能力，然后构建不可变的 `RestorePlan`。
    2. 串行化恢复操作，并在配置允许时原子地持久化一份完整的「变更前检查点」。若必需的检查点失败，则返回结构化拒绝且不调用执行器。
    3. 把该计划交给 `RestoreExecutor`，它会在执行每个已批准动作之前立即重新校验所有外部目标。
    4. 轮询已启动的条目，检查安全身份、响应性与稳定的标题/类名/边界；在每个条目就绪时立即定位，同时保持一对一的 HWND 分配。
    5. 返回结构化的逐动作与逐条目结果。
- **`CreateRestorePlan(snapshot, mode)`** —— 为预览或显式审批工作流执行只读观测与纯规划阶段。
- **`ExecuteApprovedRestorePlanAsync(snapshot, plan, token)`** —— 在事务闸门之后执行一份已批准计划，可选地先做检查点，并在过期前置条件出现时报告，而不悄悄重新规划。
- **`UndoLastRestoreAsync(token)`** —— 服务级恢复最新可用的检查点；托盘里的「整桌面撤销」命令使用 `LayoutCoordinator`，因此无关窗口也会被对账。新的「撤销前安全检查点」仅在启用时创建。

已被废弃的同步采集包装器、非结构化的恢复结果适配器，以及仅测试用的匹配/会话兼容类型，在它们的断言迁移到这些活动边界之后已被移除。

`WorkspaceService` 组合聚焦的协作者，而不是把它们的策略内联持有：

- `WorkspaceCheckpointService` 负责检查点准入、采集档、持久化与结果。
- `WorkspaceRestoreDiagnosticsService` 记录阶段耗时并发布最新的带类型报告。
- `WorkspaceLayoutVariantService` 选择/应用按拓扑区分的放置变体。
- `RestoreObservationBuilder`、`WorkspaceSnapshotBuilder` 与 `WorkspaceCaptureBuilder` 在门面之后负责观测与采集构建。

### `StorageService`

原子化、带版本号的 JSON 持久化与迁移。工作区 schema v10 组合了稳定身份、语义布局、Resume 兼容的模式/逐条目策略、按拓扑区分的布局变体、持久化的 VS Code 工作区目标种类、稳定的浏览器会话元数据、逐窗口的资源管理器标签页会话，以及显式确认过的 Windows 终端标签页配置/目录；v2–v9 与旧版 profile 文档可在不虚构不可用几何、不改变恢复行为的前提下迁移。
当一次同名重采集替换某个具名工作区时，其默认模式被保留，稳定 ID 与条目策略只对「在两个快照中都唯一」的身份进行跨版本传递。`StorageService` 仍是面向应用的兼容门面；带类型的仓储防止「永久、恢复与短生命周期」产物被混在一起。

- **存储路径**（位于 `%AppData%\SuKe\` 下）：
    - `workspaces/{workspaceId}.workspace.json` —— 永久具名工作区。
    - `checkpoints/{workspaceId}.checkpoint.json` —— 恢复检查点。
    - `checkpoints/checkpoint-index.json` —— 带版本号、可重建的恢复元数据索引。
    - `temporary-captures/{workspaceId}.temporary.json` —— 短生命周期采集。
    - `settings.json` —— 带版本号的应用设置（schema v8），包含基于 ID 的工作区引用、复合的学习式窗口匹配提示、预览/检查点偏好、持久化应用身份、逻辑路径别名、救援阈值、浏览器标签页恢复偏好，以及首次运行完成标记。v4→v5 迁移会把既有安装标记为「已完成」，从而升级永远不会被误呈现为首次启动。
    - `last_fingerprint.txt` —— 跨重启持久化最近一次已知指纹。
    - `.migrated_v2` —— 仅用于旧版显示器配置导入的完成标记。
- `NamedWorkspaceRepository`、`CheckpointRepository` 与 `TemporaryCaptureRepository` 只枚举各自的目录与扩展名。因此检查点或过期清理无法发现或删除永久工作区。
- `CheckpointRepository` 会盖印恢复 schema 版本、稳定检查点 ID、触发器、UTC 创建/过期时间、源拓扑指纹与目标工作区 ID。默认最多保留十个健康检查点、保留 7 天；遇到损坏的同伴文档时，最新的健康文档仍然可用。保留与索引维护只在检查点目录内运行。
- 文档在被原子写入器创建为同级临时文件、刷盘、然后替换/移动就位之前，会先被序列化并校验。被中断的提交会留下前一份最终文档完整无损；不完整的 `.tmp` 文件永远不会被当作产物枚举。
- 工作区显示名只是元数据。稳定的 `WorkspaceId` 才是文件名/存储身份，因此「消毒后落到同一文件名」的名称可以共存。
- 工作区 schema 迁移在反序列化之前按顺序运行。当前文档携带稳定的 `WorkspaceId`；每个条目携带 `EntryId`。
- 设置迁移会把旧版的工作区名称引用替换为稳定的工作区 ID。
- 仓储加载返回健康文档，以及结构化的「损坏、校验失败、不受支持版本、I/O 问题」。一个坏文件不会阻止健康文档加载。
- 未来版本的 schema 与非法文档会被报告并保持原样不动。
- 旧版 `profiles/*.profile.json` 文件以确定性 ID 导入。只有当每个旧版来源都成功时才写入完成标记。
- 旧版「按名称寻址」的工作区文件只会在迁移时被发现。以 ID 寻址的副本会先被提交，然后才移除旧版来源。
- `LogicalPathAliasResolver` 在每个本地路径旁边记录最具体的 `${ALIAS}` 形式。恢复会先观测精确的已保存路径，再观测当前设备映射，最后才是既有的修复行为；映射不可用时绝不会改写工作区。
- `WorkspaceTransferService` 负责仅本地的 WA-033 传输：按类别控制的「精确」与「可移植/脱敏」导出使用原子写入器；分阶段导入会校验大小/schema、迁移 transfer v1 与受支持的工作区负载、预览稳定 ID/名称冲突、检测「预览之后文件被修改」，并总是导入为一个独立副本。它绝不传输应用设置、凭据、Cookie、文档内容或浏览器原生 host 数据。
- `ISyncProvider` 与 `FolderSyncProvider` 是 WA-034 的**部分**传输层地基。提供者写入是原子的且与传输方式无关，但分阶段的本地迁移/校验、清单、设备 ID、冲突副本、重试、排除项、编排与 UI 有意未被声称已完成。

### `LayoutCoordinator`

响应从 `App.xaml.cs` 转发来的 `WM_DISPLAYCHANGE` 事件。

- **`HandleDisplayChangeAsync()`** —— 取消一次被取代的事件，并通过 `DisplayTopologyStabilizer` 等待，直到完整拓扑签名在稳定间隔内保持不变。`TemporaryDisplayRecoveryTracker` 记录一次「离开」，并且只在那个完全相同的前置签名回归时才行动；在异常期间它不会重新定位窗口。默认模式从托盘提供一次「恢复已保存布局」的选项，而禁用与自动模式持久化在 Settings 中。自动恢复使用强制的 `AutomaticDisplayRestore` 检查点触发器，并且当前台窗口覆盖其所在显示器时会被转换为托盘提示。启动就绪使用同一个稳定器。
- **`UndoLastRestoreAsync()`** —— 通过「先关闭后恢复」的切换引擎对账最新检查点，并报告检查点闸门或恢复失败，而不把它们当作成功。
- 通过私有 `NotifyBalloon` 辅助方法持有所有通知气泡调用，该方法会封送到 UI 线程。

SuKe 的托盘通知由 `AppSettings.NotificationsEnabled` 控制。最终的 `App.ShowBalloon` 显示方法会检查该设置，因此由启动、保存、恢复、切换与显示变化流程发起的通知都遵循同一偏好。该设置只抑制 SuKe 的消息；它不会改变 Windows 通知或「专注助手」状态。

### `JumpListService`

使用 OpenMcdf 库读取 `%AppData%\Microsoft\Windows\Recent\AutomaticDestinations\` 下的 Windows Jump-List AutoDestList 二进制文件，以提取每个应用最近打开的文件路径。

### `TitleParser`

无状态工具类。`ExtractFilePath(processName, titleSnippet)` 对窗口标题施加一组正则表达式来提取文件路径，并返回 `(path, confidence)` 元组。

### 恢复仿真

`RestoreSimulationRunner` 加载带版本号、人类可读的合成快照、拓扑、活动身份、资源、浏览器、提示与持久化应用观测，并且只调用 `RestorePlanner`。`--simulate-restore <fixture.json>` 打印一份确定性的脱敏计划以及「黄金期望」失败项，且永不构造 Win32 清单或变更边界。命令与完整的装置矩阵见 [restore-simulation.md](restore-simulation.md)。

---

## 数据流：保存

```
用户点击 "Save Workspace"
    → SaveWorkspaceDialog 收集名称 + 显示器选择
    → WorkspaceService.CaptureWorkspaceAsync(...)
        → WorkspaceSnapshotBuilder 构建窗口条目（不持久化）
            → MonitorService.GetCurrentMonitors()
            → WindowService.SnapshotWindows(CaptureCandidate, monitors)
            → CaptureResourceResolver 使用 TitleParser + 有界的 Jump List/文件夹发现
            → CapturedWindowEntryFactory 创建 WorkspaceEntry 变体
        → IBrowserSessionConnector 采集可选的浏览器会话、不透明配置文件身份、
          运行时窗口 ID，以及安全的唯一桌面条目/显示器关联
        → WorkspaceCaptureResult 记录浏览器结果与完整快照
    → WorkspaceService.PersistCapture(result, NamedWorkspace, SavePartialWorkspace)
        → 一次原子化的具名工作区提交
```

## 数据流：恢复

```
手动托盘/Settings/热键请求
    → WorkspaceService.CreateRestorePlan(snapshot, mode)
        → 观测清单、资源、浏览器能力与显示器拓扑
        → RestorePlanner.Build(...) 返回不可变意图
    → RestorePreviewPolicy 查询设置与计划的可执行性
    → 可选：RestorePlanPreviewDialog 收集选择与被禁用的条目 ID
    → RestorePlanner.DeriveApprovedPlan(original, disabled IDs)
    → WorkspaceService.ExecuteApprovedRestorePlanAsync(snapshot, approved plan)
        → 可选：CaptureWorkspaceAsync(当前桌面)
        → 启用时，CheckpointRepository 原子提交 + 限制恢复历史
        → RestoreExecutor
            → 预检已批准的外部引用
            → 按配置文件作用域的浏览器标签页复用，或重复/冲突处理，
              然后执行「已有窗口/启动」变更
            → 相关联的就会绪
            → 放置校验与有界重试
            → 最终最小化与有序结果聚合

启动请求
    → LayoutCoordinator 自动恢复路径
        → WorkspaceService.RestoreWorkspaceWithExecutionResultAsync(snapshot, mode)
            → 构建同一份不可变计划
            → 先检查点，然后走同一套预检与变更边界执行
            → 在不打开预览 UI 的前提下保留一键行为

显示变化请求
    → DisplayTopologyStabilizer 等待签名稳定
    → TemporaryDisplayRecoveryTracker 记录离开，然后只识别它的回归
    → 禁用：不提供；先询问：托盘提供 Restore/Dismiss；自动：带检查点的恢复
    → 全屏前台活动会把自动恢复转换为同一种托盘提示

Undo Last Restore
    → CheckpointRepository.GetLatest() 隔离损坏/过期文档
    → RestorePlanner.Build(checkpoint, 当前清单, 拓扑, ExactSwitch)
    → 采集 + 提交一份新的、强制性的 Undo 安全检查点
    → WorkspaceSwitchEngine 关闭无关窗口
    → RestoreExecutor 执行并校验检查点计划
```

---

## 模型

| 类型 | 用途 |
|---|---|
| `WorkspaceSnapshot` | 顶层保存产物。包含一个 `MonitorInfo` 列表与一个 `WorkspaceEntry` 列表。 |
| `WorkspaceEntry` | 一条已保存的窗口记录：应用身份、可选文件路径、窗口位置与显示器分配。 |
| `LayoutVariant` | 一个逻辑工作区在特定拓扑下的显示器与放置集合。 |
| `MonitorInfo` | 物理显示器元数据：稳定 ID、友好名称、几何、索引、主显示器标记。 |
| `WindowRecord` | 活动窗口的已采集状态：DPI 感知的归一化矩形、类名、标题片段、进程名、可执行文件路径。 |
| `RestorePlan` | 不可变的恢复意图，包含候选、放置、动作、警告、阻断项与全局动作。 |
| `RestoreExecutionResult` | 结构化的「计划过期」、逐动作与逐条目执行结果。 |
| `WorkspaceCheckpointMetadata` | 带版本号的恢复身份、触发器、生命周期、源拓扑与目标工作区引用。 |
| `RestoreCheckpointOutcome` | 附着在恢复执行上的「已创建/已禁用/失败/已取消」闸门结果。 |
| `RestorePlanPreview` | 对某份计划的 UI 安全投影；它包含显示状态，但不执行观测或变更。 |

---

## 如何新增一个服务

1. 在上表对应的归属目录下创建文件，例如 `Services/Restore/MyRestorePolicy.cs`，并沿用既定的 `SuKe.Services` 命名空间，除非已有更窄的命名空间存在。
2. 为类与所有 public 成员加上 `<summary>` XML 文档注释。
3. 在 `App.xaml.cs` 中与现有服务一起把它注册为单例。
4. 通过构造函数注入到任何需要它的服务中。
5. 所有诊断输出使用结构化的 `AppLogger` 重载——绝不使用 `Debug.WriteLine`。

## 诊断日志与隐私

`AppLogger` 写出 JSON-lines 事件，含稳定事件 ID、严重级别、静态消息、具名字段与可选的异常元数据。敏感值必须通过 `LogField` 实例携带，以便集中式的 `LogRedactor` 施加正确策略：

- 路径在可分享导出中只保留其扩展名。
- URL 只保留其 origin；凭据、path、query 与 fragment 均被移除。
- 标题与工作区名被替换为类别标记。
- 稳定标识符变为确定性假名，从而保留跨事件的相关性。
- 命令行会先清除凭据开关，再对其中嵌入的路径与 URL 做脱敏。
- 即使在本机诊断日志中，密钥也会被移除。

本机最低严重级别来自 `AppSettings.DiagnosticLogLevel`。`AppLogger.ExportDiagnostics` 默认使用 `LogRedactionMode.Redacted`；命令行选项 `--export-diagnostics [destination]` 直接暴露这个安全默认值，无需手动编辑日志。旧版非结构化消息会被排除在可分享导出之外，因为它们的敏感度无法被可靠分类。
