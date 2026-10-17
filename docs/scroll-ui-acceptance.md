> 工程名与路径已按当前命名归一化；本文件中的历史数字、产物和哈希不代表本轮交付。

# 滚轮、滚动手感与活动操作对齐验收

日期：2026-10-17。对应根目录 todo.md 的 S1～S4。

## 交付内容

| 修改前 | 修改后 | 作用 |
| --- | --- | --- |
| 滚轮刚产生位移就被自身异步通知取消；实测 72 DIP 的请求走到约 14 DIP 后回到 0 | 区分自身排队偏移、外部滚动及嵌套事件；取消动画时不回写旧基值 | 修复工作区滚轮失效与回弹 |
| 独立动画对象在快速连续重定向时可能停止更新 | 动画挂在真实 ScrollViewer 上，从当前实际位置衔接，以 160 ms 短缓出完成 | 连滚累积目标，反向立即重定向；小刻度不取整 |
| 系统滚轮行数为 0 时被当成按页滚动 | 0 表示禁用，负值表示按页滚动 | 遵从系统输入设置 |
| 活动类别标签在上、详情按钮在下 | 两者同一行等高居中，共享类别列及按钮列宽，间隔 12 DIP | 各条目操作区域对齐，长标题换行时位置稳定 |

滚动条、键盘与程序化滚动可接管；系统关闭动画时直接滚动；边界不吞滚轮事件；嵌套可滚动区域优先处理自身输入。只调整原本启用平滑滚动的正文容器。

实现依据：[WPF ScrollViewer 源码](https://source.dot.net/presentationframework/System/Windows/Controls/ScrollViewer.cs.html)中的 ScrollToVerticalOffset 使用 EnqueueCommand，位置通知并不发生在该方法的调用栈内。

## 修改文件与提交

- `src/SuKe/UI/Shell/SmoothScrollBehavior.cs`：滚轮与动画状态处理。
- `src/SuKe/UI/Shell/ActivityPage.xaml`：类别与详情操作布局。
- `tests/SuKe.Tests/SmoothScrollTests.cs`：滚轮系统设置换算回归。
- `tests/SuKe.Tests/SmoothScrollUiTests.cs`：真实 WPF 动画、位置通知、连滚、反向、嵌套、接管和重入。
- `tests/SuKe.Tests/ActivityLayoutUiTests.cs`：实际行模板及生产样式的几何位置验证。
- `todo.md`、`项目记忆.md`、本文：本轮任务状态、长期维护约束与验收证据。

已分功能提交：

- `23a2d43`：fix: 修复工作区滚轮动画被异步通知打断。
- `7d52db9`：fix: 优化连续滚动和反向滚动的衔接手感。
- `0b8d773`：fix: 对齐活动分类标签与详情按钮。

开始时已有大量未提交改动，均保留。todo.md 只暂存本轮新增段落和该段落的完成状态；未将前一轮任务记录一起提交。验收构建基于当前完整工作区，包含这些已有本地改动。

## 自动验证

| 验证 | 结果 | 报告（仓库根目录相对路径） |
| --- | --- | --- |
| 修复前滚轮复现 | 失败，预期72、实际0，采样约14→0 | `out/scroll-ui-acceptance/results/scroll-before.trx` |
| S1 滚轮修复定向检查 | 9通过 | `out/scroll-ui-acceptance/results/scroll-s1.trx` |
| S2 滚动换算与真实 WPF 滚动 | 13通过 | `out/scroll-ui-acceptance/results/scroll-s2-final.trx` |
| S3 真实模板布局 | 4通过 | `out/scroll-ui-acceptance/results/alignment-s3-final.trx` |
| 完整测试首次运行 | 475通过、1跳过、2失败 | `out/scroll-ui-acceptance/results/full.trx` |
| 完整测试最终运行 | **476通过、1跳过、1失败，合计478** | `out/scroll-ui-acceptance/results/full-serial-ui.trx` |
| Release应用构建 | **通过，0错误、1项NU1900警告** | 独立产物见下 |

首次完整测试的新增失败是快速连滚在其他 WPF 呈现测试并发时未到达最终位置。两个新增界面测试类改为独占运行后，同一断言在完整测试中通过；未删除断言或跳过该测试。首次失败报告保留。

最终唯一失败仍为原有的 `ShellUiTests.Real_wpf_shell_displays_live_screen_previews_and_preserves_workflows_in_isolated_storage`：测试组件 `IsolatedShellApp` 无法加载 `/SuKe;V1.7.0.0;component/app.xaml` 的 BAML。本轮未修改该既有测试及其宿主，不能把完整测试称为全部通过。

唯一跳过项为需显式启用的 `WorkspaceCaptureLiveTests.Controlled_windows_survive_occlusion_minimization_selection_and_virtual_desktop_capture`；本轮没有执行虚拟桌面采集验收。

NU1900 原因是 NuGet 漏洞数据服务 `https://api.nuget.org/v3/index.json` 不可达。依赖从本地缓存正常还原，未关闭漏洞检查、修改源或变更依赖版本。

布局测试覆盖可用宽度 640/1000、等效文字比例 80%/100%/150%、浅色与深色主题。宿主直接加载生产行模板和样式，移除页面所有者的 Click 绑定以独立测量；这部分不替代详情点击行为验收。渲染截图：`out/scroll-ui-acceptance/screenshots/activity-alignment.png`。

## 实际界面输入

使用 computer-use 打开独立验收宿主，加载当前工作区编译的真实 SettingsWindow、ActivityPage、样式和滚动行为。数据为24个专属测试工作区，存储位于本轮 out 下；没有运行 App.OnStartup、托盘初始化或真实桌面恢复。本轮只执行滚动、页面导航和详情操作，未点击保存、恢复或其他数据修改入口。

| 操作 | 实际观察 |
| --- | --- |
| 工作区正文向下滚轮输入600 | 滚动条从0到360，显示内容移到后续工作区 |
| 工作区正文反向输入-240 | 稳定后从360回到216 |
| 切换到活动页 | 保存标签、详情按钮同排等高，各条目右侧对齐 |
| 点击首条活动详情 | 真实详情窗口打开，显示该测试工作区的保存记录 |
| 关闭详情 | 对详情窗口自身发送Escape后关闭；最初对主窗口发送Escape未关闭模态窗，未将其记为通过 |
| 活动正文滚轮输入480 | 滚动条从0到288 |
| 切回工作区 | 工作区原位置216保留 |
| 退出验收 | 已关闭宿主，窗口列表中确认该宿主没有剩余窗口 |

输入后立即读取的位置可能仍是动画起点，均在再次读取稳定状态后确认数值。人工对“滑动是否舒服”的主观评价仍交用户验收。

本次实机使用独立宿主验证当前代码的页面交互，不代表完整 App 启动、托盘和打包安装流程已验收。未操作用户原先正在运行的 `suke.exe`。

## 验收程序

本次可运行应用：

```text
out/scroll-ui-acceptance/build/bin/SuKe/release/SuKe.exe
```

主应用文件身份：

```text
SuKe.exe SHA256 12A3417ADD0CAE41C6718C73804AE11E770E08C6A3E15866826A263B63B07082
SuKe.dll SHA256 B18B258D3D1828FCB0306523FB426AE329443EE90CE1BD07C1E8B29B597B0BA9
```

构建使用 `D:/env/dotnet-sdk/dotnet.exe`、Release 配置和独立 artifacts 目录。通过临时 CustomAfterMicrosoftCommonTargets 覆盖项目中的根目录复制与权限调整目标为空目标，避免覆盖当前运行程序或修改文件权限；该文件未加入产品配置。

用户验收时先退出当前程序，再从上述路径运行。建议依次检查：工作区单次滚轮、连续滚动、立即反向、鼠标放在卡片与按钮上滚动、切到活动并返回、活动标签与详情对齐、点击详情。此应用正常启动会使用用户原本的数据；无需为本轮 UI 验收执行桌面恢复。

测试报告、渲染截图和应用构建保留供核对；本次临时诊断日志、基线副本、暂存补丁、隔离宿主及其测试数据在结束后清理。
