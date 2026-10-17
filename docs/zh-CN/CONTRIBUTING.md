# 为 SuKe 贡献代码

> 本文为 [CONTRIBUTING.md](../../CONTRIBUTING.md) 的中文翻译。命令、路径与代码标识符保持原样；若中英文出现歧义，以英文原文为准。

感谢你有兴趣参与贡献！SuKe 是一个小而专注的实用工具，欢迎任何形式的贡献——修 bug、加功能，或改进文档。

---

## 入门

**前置条件：**

- Windows 10 或 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推荐），或安装了 C# Dev Kit 扩展的 VS Code

**克隆并构建：**

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

当前安装与启动行为见 [项目说明](../../README.md)；本项目连接器尚无商店发布或签名版本。

---

## 项目结构

```
src/SuKe/
├── App.xaml / App.xaml.cs        ← 入口、托盘图标接线、服务容器
├── Models/                       ← 纯数据类（无逻辑、无依赖）
├── Services/                     ← 全部业务逻辑
├── Native/                       ← P/Invoke 声明（Windows API 封装）
├── UI/                           ← WPF 窗口与对话框
└── Resources/                    ← 图标与静态资源
tests/SuKe.Tests/
├── Fixtures/                     ← 旧版、当前、损坏与未来版本的 JSON 样本
└── *.cs                          ← 服务层、规划器、预览与执行器测试/桩件
```

每一层的详细讲解见 [docs/architecture.md](../architecture.md)（中文版：[architecture.md](architecture.md)）。

---

## 编码规范

- **目标框架：** .NET 8、C# 12、WPF。
- **风格：** 遵循现有文件约定——局部变量用 `var`，单行方法用表达式体，相关赋值对齐。
- **XML 文档：** 每个 `public` 类型与成员都必须有 `<summary>`。在合适处使用 `<param>`、`<returns>`、`<remarks>`。可参考 [微软文档注释规范](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments)。
- **日志：** 使用结构化的 `AppLogger` 重载，提供稳定的事件 ID、不含敏感信息的消息，以及具名的 `LogField` 值。路径、URL、标题、工作区名、命令行、标识符与密钥都要用对应的敏感度辅助方法标记。绝不记录原生消息负载、浏览器页面内容、OAuth 令牌、Cookie 或其他凭据，也绝不使用 `Console.WriteLine` 或 `Debug.WriteLine`。
- **原生调用：** 所有 P/Invoke 声明都放在 `Native/NativeMethods.Window.cs` 或 `Native/NativeMethods.Display.cs`。不要在各处服务代码里散落 `[DllImport]`。
- **UI 线程：** `NotifyBalloon` 以及任何直接写 WPF 属性的操作，都必须通过 `Application.Current.Dispatcher` 派发。
- **恢复流水线：** 保持「观测、纯规划、用户审批、过期状态预检、变更执行」为彼此独立的边界。预览代码可以投影一个 `RestorePlan`，但不得枚举窗口、启动进程，或悄悄重建一个过期的计划。
- **不要**在未经讨论的情况下引入额外的 NuGet 包——依赖列表有意保持最小化。

---

## 提交改动

1. **Fork** 仓库并创建分支：`git checkout -b feat/my-feature`。
2. 按上述规范**编写**代码。
3. **运行自动化测试**：`dotnet test SuKe.sln --configuration Release` 必须通过。
4. **手动测试**：插拔扩展坞、分别在含文件与不含文件的情况下保存工作区、预览一次手动恢复、禁用某个条目，并验证「选择性恢复」与「自动恢复」。
5. **干净构建**：提交前 `dotnet build` 必须产生 0 error、0 warning。
6. **发起 PR**，附上简短的改动说明与原因。

---

## 报告问题

请包含：

- 你的 Windows 版本（`winver`）与显示器数量。
- 复现问题的步骤。
- 一份脱敏后的诊断导出。在 PowerShell 中运行：`./SuKe.exe --export-diagnostics "$env:USERPROFILE\Desktop\SuKe-diagnostics.jsonl"`。默认导出会移除敏感字段；请勿分享原始的 `app.log` 文件。

---

## 可以做什么

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

| 领域 | 说明 |
|---|---|
| **恢复策略** | 增加按条目的显式「移动、启动、浏览器、最小化、排除」意图 |
| **显示拓扑** | 在自动恢复前对短暂变化的显示器状态做防抖 |
| **恢复诊断** | 扩展逐条报告与确定性仿真装置 |
| **恢复体验** | 构建检查点历史、临时采集与引导式恢复流程 |
| **应用适配器** | 在不绕过共享安全边界的前提下增加专用策略 |
| **可移植性** | 增加路径别名、导入/导出、同步与跨设备显示器身份 |

规范化的构建、测试与发布命令见 [build.md](build.md)。
