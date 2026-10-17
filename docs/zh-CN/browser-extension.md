# SuKe Browser Connector（Chromium）

> 本文为 [browser-extension/README.md](../../browser-extension/README.md) 的中文翻译。注册表路径、权限名与命令保持原样；若中英文出现歧义，以英文原文为准。

这是面向 Issue #5 的 Manifest V3 Chromium 扩展。它采集普通浏览器窗口以及可恢复的 `http`、`https` 与 `file` 标签页，包含标签页顺序、激活状态、固定（pinned）状态与标签组。无痕窗口与标签页被排除。

## Chrome 安装

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

## 本地开发

1. 构建或发布 SuKe，使 `SuKe.exe` 位于 `native-host-manifest.json` 中给出的路径。
2. 在 `chrome://extensions` 或 `edge://extensions` 中开启开发者模式并加载本目录（未打包）。浏览器会在扩展卡片上显示生成的开发扩展 ID；复制该 ID。
3. 把 `native-host-manifest.template.json` 复制为 `native-host-manifest.json`，并把 `REPLACE_WITH_PUBLISHED_EXTENSION_ID` 替换为该扩展 ID。
4. 为每个正在测试的浏览器注册该清单。随附的 `register-native-host.ps1` 会为 Chrome 与 Edge 写入清单文件与当前用户注册表键：
   `powershell -ExecutionPolicy Bypass -File .\register-native-host.ps1 -ExtensionId <id> -SuKePath <path-to-exe>`
   Chrome 使用：
   `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.suke.browser`
   Edge 使用：
   `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.suke.browser`
   注册表默认值为 `native-host-manifest.json` 的绝对路径。
5. 源码改动后重新加载扩展。在浏览器的扩展页查看错误，在 `%AppData%\SuKe\app.log` 查看原生 host 诊断。

如果 host 未注册，扩展会报告一条提示信息并保持空闲。Chrome 的 `Specified native messaging host not found` 表示第 4 步尚未针对当前扩展 ID 完成；它并不是 SuKe 应用崩溃。

当前安装与启动行为见 [项目说明](../../README.md)；本项目连接器尚无商店发布或签名版本。

该扩展需要 `tabs`、`tabGroups`、`storage` 与 `nativeMessaging` 权限。本地扩展存储保存一个不透明的随机配置文件密钥，使会话永不在不同浏览器配置文件之间被去重；它不读取配置文件名称、浏览器账号、Cookie、密码、页面内容或浏览历史。

## 支持的协议消息

持久化原生端口使用带 `protocolVersion: 2` 的请求/响应消息：

- `capture`：返回浏览器会话。
- `restore`：恢复受支持的标签页与标签组；在所选策略允许时，只复用同一不透明配置文件中精确匹配的普通标签页。`file://` URL 与旧版「配置文件未知」的会话一律单独打开。`Ask` 冲突会被返回给 SuKe，由它做出显式的「复用/新开/跳过」决定。
- `ping`：检查 host 连通性。

SuKe 通过 `SuKe.BrowserBridge` 命名管道把 Chromium 请求发给 host。Firefox 使用独立的管道与包，因此两个家族可以同时连接而不争抢同一个本地端点。扩展 ID 必须在原生 host 清单中被显式加入允许列表；生产环境不接受通配来源。

桌面应用把浏览器恢复表示为不可变恢复计划中的一个显式动作。手动恢复会在批准之前显示该动作，在变更之前重新检查连接器可用性，并且只有在该回退已包含在已批准计划中时才使用「普通浏览器回退」。禁用对应条目会同时阻止会话动作及其回退。

桌面端恢复会在发出浏览器恢复请求之前提交一份恢复检查点，并在可取消的恢复进度窗口中报告连接器工作。就绪等待与「同一已保存条目的成功浏览器动作」相关联，而不是与无关的应用活动相关联。
