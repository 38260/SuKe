# 溯刻设置导航与项目身份验收

日期：2026-10-17。对应根目录todo的R1～R5。

## 完成内容

| 修改前 | 修改后 | 用途 |
| --- | --- | --- |
| 托盘设置只唤起主窗口，可能停在工作区或活动页 | 设置入口、设置快捷键及帮助中的设置请求统一选择设置页；管理工作区单独选择工作区页 | 各入口进入对应页面；普通主窗口入口保留当前页 |
| 设置分类调用整块区域的BringIntoView | 只计算正文纵向目标，160ms短缓出，可连续改选；键盘直接定位 | 避免横向位置被带动、跳到整块内容边界 |
| 快速替换动画时钟时存在停更，结束帧可能略短于目标 | 使用视图所属Dispatcher短时定时器，结束帧精确写入目标 | 保留连滚、反向、页面重入和外部接管 |
| 工程与产品身份混杂 | 溯刻 · SuKe，工程、命名空间、程序集、资源与连接器统一 | 程序、文档与开发入口一致 |
| 数据目录固定到已有标识 | 新安装使用当前目录；存在旧数据时原地沿用，当前数据优先 | 不移动、覆盖或删除已有配置、工作区和截图 |
| 文档沿用继承的下载与商店发布结论 | README说明项目来源，其余文档使用本项目名称和实际分发状态 | 不宣称本项目已有商店或签名版本 |

内部旧标识只保留在AppDataPaths的兼容常量中，也供旧开机启动值和终端记录兼容使用；MIT许可及其版权声明保留。未修改Git历史或远程配置。

## 关键修改位置

- `src/SuKe/App.MainWindow.cs`、`App.xaml.cs`：设置页路由及窗口激活。
- `src/SuKe/UI/Shell/SettingsSectionNavigator.cs`、`SettingsWindow.SettingsPresentation.cs`、`SmoothScrollBehavior.cs`：分类定位与滚动。
- `src/SuKe/Services/System/AppDataPaths.cs`、`SettingsService.cs`、`AutostartService.cs`、`Storage/StorageService.cs`：数据路径和兼容；SettingsService跟随显式存储目录。
- `src/SuKe/Services/Browser/BrowserIntegrationService.cs`、两个连接器目录：本项目原生通信主机及扩展身份。
- `SuKe.sln`、`src/SuKe/SuKe.csproj`、`tests/SuKe.Tests/SuKe.Tests.csproj`、启动脚本及Release工作流：工程身份。
- README、build、静态HTML、帮助和历史文档：品牌与分发状态。
- `tests/SuKe.Tests/SettingsNavigationUiTests.cs`、`AppDataCompatibilityTests.cs`、`BrowserIntegrationServiceTests.cs`：功能回归。

## 自动检查

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 设置入口与启动策略 | 9通过 | `out/settings-suke-acceptance/results/tray-settings-final.trx` |
| 设置与管理工作区入口分流 | 10通过 | `out/settings-suke-acceptance/results/entry-routing-final.trx` |
| 分类导航、键盘、横向偏移及滚动回归 | 17通过 | `out/settings-suke-acceptance/results/settings-navigation-complete.trx` |
| 更名后品牌、数据兼容、连接器身份与导航 | 14通过 | `out/settings-suke-acceptance/results/suke-brand-final.trx` |
| Firefox连接器 | 4通过 | `node --test firefox-extension/tests/background.test.cjs` |
| 完整测试（补充入口分流后） | **485通过、1跳过、1失败，共487** | `out/settings-suke-acceptance/results/full-suke-final.trx` |
| Release应用构建 | **通过，0错误、1项NU1900警告** | 更名后的独立产物 |
| 实际EXE仿真入口 | **退出0**，黄金断言匹配，输出脱敏JSON | `out/settings-suke-acceptance/results/executable-simulation-final.json` |
| 产品元信息 | ProductName为溯刻 · SuKe，CompanyName为SuKe，程序集SuKe | 编译后的应用VersionInfo与程序集断言 |
| 品牌扫描 | 只剩README来源行与内部兼容常量 | 版本管理内容及当前源码扫描 |
| Markdown格式 | 代码块配对检查通过，Git差异检查通过 | 文档与暂存差异检查 |

完整测试唯一失败仍为既有的 `ShellUiTests.Real_wpf_shell_displays_live_screen_previews_and_preserves_workflows_in_isolated_storage`：派生测试宿主无法加载App.xaml的BAML。本轮未修改其断言或跳过该测试，不能声称完整测试全部通过。实际应用EXE的仿真入口正常加载并执行。

唯一跳过项为需要显式启用的桌面采集测试 `WorkspaceCaptureLiveTests.Controlled_windows_survive_occlusion_minimization_selection_and_virtual_desktop_capture`。

NU1900来自NuGet漏洞数据服务不可达。依赖使用现有缓存还原，未关闭安全检查、更换依赖版本或修改全局源。

分类导航检查包含快速改选、键盘即时定位与横向位置保持。此前发现的滚动停更和末帧精度失败报告保留在results中；最终检查保留原断言并通过。

## 隔离界面检查

宿主使用与本次交付完全相同的SuKe.dll，显式注入本轮out目录中的存储和设置。使用真实SettingsWindow、真实App设置处理器和MenuItem.Click事件，跳过真实App启动中的托盘注册、热键与恢复初始化。

以下五个设置场景均打开设置页且窗口可见、未最小化：

- 工作区进入设置。
- 活动页进入设置。
- 隐藏窗口进入设置。
- 最小化窗口进入设置。
- 尚无主窗口时，通过实际工厂创建窗口并进入设置。

另验证“管理工作区”的真实导航方法独立进入工作区页。最终六项结果为 `out/settings-suke-acceptance/settings-entry-smoke-final.json`。最终宿主与交付程序的SuKe.dll哈希相同，验证结束后自动关闭。界面中的主标题与品牌也已观察核对。

合成鼠标分类检查期间检测到窗口输入状态变化，刷新后该验收窗口已关闭，未继续输入或将观感检查记为通过。分类定位已有真实WPF定向测试；最终手感交用户验收。

本轮未实际点击Windows通知区域中的图标，未做普通启动后的真实托盘生命周期、真实浏览器连接、签名包或桌面恢复验收。

## 保护已有工作

开始时存在大量未提交内容。31个已有脏文件逐字节比对，除项目名称替换、明确的图标说明文案与管理入口处理器一行增量外，其余内容保留；旧todo任务段落仅替换项目名称。原有未跟踪源码随目录更名保留，未代为提交。

脏文件的改名提交以原索引内容为基础生成；本轮设置入口、帮助文案和todo更新仅暂存可区分的增量。构建和验证基于完整工作区，包含用户已有本地内容，不代表干净检出或发布包验收。

临时宿主、测试数据、基线副本和辅助脚本结束后清理；测试报告、扫描摘要和新应用构建保留。

## 提交与验收路径

- `b6d1ee6`：fix: 托盘设置入口直接切换到设置页。
- `ce86740`：fix: 优化设置分类定位与连续滚动衔接。
- `8c402e1`：refactor: 统一溯刻品牌与SuKe工程身份并兼容旧数据。
- `8f2800f`：docs: 更新溯刻项目说明并清理旧品牌与发布信息。
- `0d11d5e`：fix: 区分托盘设置与管理工作区的导航目标。

本次程序位于 `out/settings-suke-acceptance/rebranded/bin/SuKe/release/SuKe.exe`，请先退出当前运行的程序，再从该完整目录启动验收。

```text
SuKe.exe SHA256 A08667B07BA9F64F8348775FCB50E705868AC5348E8104075C4570FFC6FA5DDD
SuKe.dll SHA256 23B9EEBD09B6258EBA8E6049D51B5FF2C40E8A801279325D11407690DAC982D1
```

建议先在工作区/活动页分别点击托盘设置，再检查连续切换设置分类、键盘导航、字号与主题。浏览器连接器需重新加载本项目版本；如使用可选终端集成，需将profile中脚本路径更新为SuKe.Terminal.ps1。应用不会修改全局profile。
