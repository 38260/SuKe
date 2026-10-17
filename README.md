# 溯刻 · SuKe

溯刻是一款 Windows 桌面工作区管理工具，用于保存窗口布局，并恢复受支持的应用、文件、浏览器会话和标签页。

本项目基于 [WindowAnchor](https://github.com/marvintrvl/WindowAnchor) 改造，现以独立项目「溯刻 · SuKe」维护。改造起点为上游提交 [`f6cb104`](https://github.com/marvintrvl/WindowAnchor/commit/f6cb104)（2026-09-30）。

## 使用

1. 在源码仓库根目录运行 `start-suke.bat`，它会检查环境、构建并启动最新版本；发布目录也可直接运行 `SuKe.exe`。
2. 在「工作区」选择「保存当前工作区」，勾选需要保存的窗口。
3. 通过工作区卡片执行恢复，在「活动」查看记录与已记录的恢复结果。
4. 托盘中的「设置」直接进入设置页；设置左侧分类定位到对应内容。
5. 关闭主窗口后仍可从托盘打开。登录后台启动使用 `--minimized`。

## 主要能力

- 多显示器窗口布局、屏幕预览与虚拟桌面归属。
- 支持的文件、文件夹、浏览器会话、资源管理器与终端标签页恢复。
- 恢复计划预览、匹配复核、恢复检查点和撤销。
- 活动详情区分成功、失败、跳过和未确认，旧记录不推测结果。
- 浅色/深色主题、文字比例、快捷键与开机启动设置。

不同应用对启动、窗口位置和会话恢复的支持存在差异；发出启动请求不等于恢复成功。

## 源码构建

需要 Windows、.NET 8 SDK。构建前请退出正在运行的程序，避免输出文件被占用。

```powershell
dotnet build SuKe.sln -c Release
dotnet test SuKe.sln -c Release
```

构建输出为 `src/SuKe/bin/Release/net8.0-windows/SuKe.exe`。本机已配置的SDK也可通过 `D:/env/dotnet-sdk/dotnet.exe` 调用。

发布、独立构建和验证方式见 [build.md](build.md)。最新任务见 [todo.md](todo.md)，工程约束见 [项目记忆.md](项目记忆.md)。历史验收记录不代表当前版本已经全部通过。

## 浏览器连接器

本项目连接器使用自己的名称、原生通信主机与扩展身份，当前没有本项目的商店发布链接。

- Chrome / Edge：按 [Chromium连接器说明](browser-extension/README.md) 加载本项目目录，再在应用的浏览器集成设置中注册原生通信主机。
- Firefox：按 [Firefox连接器说明](firefox-extension/README.md) 使用开发调试安装；普通分发需要签名，当前没有已签名版本。

扩展需要用户自行安装并授予权限。替换连接器后需重新连接；旧浏览器配置的精确标签复用可能需要重新保存工作区。

## Windows Terminal 集成

如需记录 PowerShell 标签页的当前目录和配置文件，在你使用的 PowerShell profile 末尾手动引入脚本，应用不会修改全局 profile：

```powershell
. 'C:/path/to/SuKe/scripts/SuKe.Terminal.ps1'
```

重开终端标签页后再保存工作区。脚本不恢复命令历史、正在运行的程序或分屏状态；未跟踪的标签可在保存时手动确认。

## 本地数据

新安装使用 `%AppData%/SuKe`。已有安装自动沿用原数据目录，不搬动、不覆盖、不删除原配置、截图或工作区；代码中仅保留内部兼容标识。

工作区JSON与本机截图附件分别存储。诊断导出默认脱敏；浏览器连接器不读取页面正文、Cookie或密码，也不采集隐私窗口。

## 许可证

保留 [MIT许可证](LICENSE) 及其版权声明。
