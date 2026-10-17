# 恢复仿真装置（Fixtures）

> 本文为 [docs/restore-simulation.md](../restore-simulation.md) 的中文翻译。命令与路径保持原样；若中英文出现歧义，以英文原文为准。

`SuKe.exe --simulate-restore <fixture.json>` 只使用装置中提供的「已保存快照、合成拓扑、活动窗口记录与已观测资源」来运行纯恢复规划器。它不会枚举显示器、读取进程、启动应用，也不会改动任何窗口。

装置使用 schema 版本 `1`。合法装置包含对每个已规划条目的黄金断言（结果、选中的句柄、匹配置信度、显示器映射、放置策略、DPI/钳制）、有序的动作种类、警告、阻断错误与可执行性。一旦不匹配，会打印一份脱敏的、确定性的报告并以退出码 `2` 结束，使 CI 失败可以被 diff。该报告会脱敏路径、标题、URL、令牌、工作区名称与应用 ID。

装置位于 `tests/SuKe.Tests/Fixtures/restore-simulations/`。它们是完整的合成观测：运行器不会查询 Win32、文件系统、进程或网络。

在仓库根目录运行某个装置：

```powershell
dotnet .\src\SuKe\bin\Release\net8.0-windows\SuKe.dll `
  --simulate-restore .\tests\SuKe.Tests\Fixtures\restore-simulations\single-monitor.json
```

退出码 `0` 表示装置已加载且所有提供的期望都匹配。退出码 `2` 表示规划器运行了但至少有一条黄金期望不一致。非法输入或不受支持的装置 schema 会被报告为错误，且绝不会落入正常的应用启动流程。

## 当前覆盖范围

- `single-monitor.json` —— 覆盖精确拓扑上的一个已存在通用应用。
- `dual-monitor-negative-origin.json` —— 覆盖两台坐标为负的显示器。
- `laptop-to-dual-mixed-dpi.json` —— 覆盖把笔记本快照恢复到混合 DPI 的双显示器。
- `missing-and-identical-monitors.json` —— 覆盖不可用、完全相同以及回退显示器映射。
- `browser-pwa-and-missing-resource.json` —— 覆盖多个浏览器窗口、一个 PWA，以及一个阻断性的缺失资源。
- `vs-code-projects.json` —— 覆盖同可执行文件的 VS Code 项目身份。
- `path-alias-remap.json` —— 覆盖一次已观测到的逻辑路径别名重映射。
- `adversarial-topology.json` —— 覆盖拓扑变化、歧义、不可用资源、常驻应用与隐私安全输出。
- `corrupt-input.json` —— 有意使用不受支持的 schema，以证明在规划之前就被拒绝。

仿真的边界仅限规划器。它证明的是「观测 → 计划」的确定性行为；它不校验 Win32 放置、应用启动、WPF 交互或真实的多显示器桌面。
