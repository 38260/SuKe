# 浏览器集成

> 本文为 [docs/browser-integration.md](../browser-integration.md) 的中文翻译。标识符、路径与 URL 保持原样；若中英文出现歧义，以英文原文为准。

SuKe 提供两个彼此独立的浏览器连接器包：

- `browser-extension/` —— 面向 Chrome、Edge、Brave 与 Opera 的 Chromium Manifest V3 连接器。
- `firefox-extension/` —— 面向 Firefox Desktop 142 及更新版本的 Firefox 连接器。

二者都实现协议 v2，但分开打包，因为 Firefox 使用 Gecko 附加组件 ID、`allowed_extensions`、Mozilla 的注册表位置以及 `background.scripts`。Firefox 附加组件有意使用持久化的 Manifest V2 后台页：Firefox 的 MV3 事件页可能在 SuKe 等待发起桌面端请求时被卸载，从而关闭原生消息端口。Mozilla 目前仍支持 Manifest V2 扩展。

## 采集哪些数据

连接器只采集「普通浏览器窗口」，以及在 SuKe 保存对话框中选中的、受支持的 `http`、`https` 与已暴露的 `file` 标签页。它们传输标签页 URL、标题、顺序、激活与固定（pinned）状态、浏览器窗口边界/状态、标签组元数据、运行时窗口 ID，以及一个本地生成的不透明连接器配置文件密钥。SuKe 会分配一个稳定的已保存会话 ID，并且仅当标题与几何能唯一确定某个条目时才记录「桌面条目/显示器」关联。

无痕窗口与 Firefox 隐私窗口被排除。连接器不请求 Cookie、密码、常规浏览历史、页面正文、表单数据、下载记录、书签、浏览器账号身份或配置文件名称。Firefox 容器身份不会被请求或恢复。

Mozilla 把「传给原生应用的数据」视为已传出附加组件之外，即使它从未离开本机。因此 Firefox 清单声明了 `browsingActivity`、`websiteContent` 与 `personallyIdentifyingInfo`（涉及 URL、标题/分组标签与不透明配置文件密钥）。它不会虚假声明为 `none`。

## 通信设计

每个扩展都打开 `runtime.connectNative("com.suke.browser")`。原生 host 把 Chromium 与 Firefox 路由到两个独立的命名管道：

- `SuKe.BrowserBridge`（既有的 Chromium 端点）
- `SuKe.BrowserBridge.Firefox`

SuKe 只从「被选中桌面窗口所代表的连接器家族」采集，然后把它们的会话合并起来。恢复时按浏览器对会话分组，并把每组通过匹配的管道发回，因此 Chrome 与 Firefox 可以参与同一个工作区而不跨用原生 host。

原生消息把每条 UTF-8 JSON 消息用一个 32 位本机字节序的长度前缀进行分帧。host 把消息限制为 1 MiB，只向 stdout 写协议数据，诊断则使用日志/stderr。请求携带 `requestId` 与 `protocolVersion`；不兼容的响应会安全失败。某个浏览器家族缺失时会报告为不可用，而不会阻止无关的桌面应用恢复。

## 配置文件感知的恢复

`storage.local` 中保存一个作用域限定在「已安装连接器」的不透明随机密钥。它不是账号、配置文件目录名或显示标签。恢复绝不会跨不同密钥复用标签页。**Reuse matching tab** 聚焦于同一配置文件中精确匹配的 `http`/`https` URL；**Always reopen** 创建新标签页；**Ask before reuse** 返回一个结构化冲突交由 SuKe 解决。`file://` 与旧版「配置文件未知」的会话一律只做「新开」。

Firefox 的恢复按标签页隔离。一个 URL 失败只会产生部分浏览器结果，而其他有效标签页、浏览器窗口与工作区应用会继续。固定/激活状态、标签组，以及普通/最大化/最小化/全屏状态，在 Firefox 接受的前提下会被恢复。

## 恢复规划与回退

浏览器恢复参与不可变的恢复计划。规划阶段记录连接器可用性，并添加一个显式的浏览器会话动作，而不创建任何窗口。就在执行之前，`RestoreExecutor` 会校验能力仍与预览一致。若会话恢复不可用，则只有「计划中已声明的回退」才能启动一个普通浏览器。禁用关联的浏览器条目会移除其会话动作，并保护它免于被启动、放置与终端最小化行为影响。

浏览器工作使用与桌面恢复相同的检查点、进度、取消、就绪与结构化结果边界。

## 商店配置

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

桌面应用无法静默安装这两个扩展中的任何一个。安装控制权始终在浏览器/商店手中。

## Firefox 本地测试与 AMO 打包

1. 在 SuKe 中运行 **Set up Firefox**，或带 SuKe 可执行文件路径执行 `firefox-extension/register-native-host.ps1`。
2. 打开 `about:debugging#/runtime/this-firefox`，选择 **Load Temporary Add-on**，并选中 `firefox-extension/manifest.json`。
3. 在 `firefox-extension/` 下运行 `web-ext lint` 与 `web-ext build`。
4. 维护者通过 AMO 使用固定的附加组件身份与已检入的 `amo-metadata.json` 发布签名更新；GitHub Release 中的 ZIP 仍只是未签名的开发/评审产物。

固定 ID `suke-browser-connector@suke.app` 出现在附加组件清单、原生 host 允许列表、注册脚本与桌面端路由测试中。Firefox 启动原生 host 时会把清单路径与附加组件 ID 作为命令行参数传入；SuKe 只把那个确切的 ID 识别为自己的 Firefox 连接器。

## Chromium 本地测试

1. 发布 SuKe，并选择原生 host 所使用的可执行文件。
2. 带未打包扩展 ID 与可执行文件路径运行 `browser-extension/register-native-host.ps1`。
3. 从 `chrome://extensions` 或 `edge://extensions` 加载未打包的 `browser-extension/`。
4. 代码改动后重新加载扩展。

未打包的 Chromium 构建会生成随机 ID，因此必须用其确切 origin 替换模板值；`allowed_origins` 中不允许通配符。

## 限制

运行时浏览器窗口 ID 是会话作用域的元数据；`BrowserSessionId` 才是 SuKe 的稳定保存身份。浏览器内部/扩展 URL 会被跳过。Firefox 容器身份不会被保留，因为连接器有意不请求 contextual-identity 或 Cookie 访问权。只有当浏览器暴露 URL 时 `file://` 标签页才会被采集，并且它永远不会被当作已存在的匹配来复用。一份过期的已批准预览会被拒绝，而不是悄悄指向一个已变化的浏览器窗口。

## 官方参考资料

- Firefox 后台脚本：https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/manifest.json/background
- Firefox 原生消息：https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/Native_messaging
- Firefox 原生清单：https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/Native_manifests
- Firefox tabs API：https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/API/tabs
- Firefox tabGroups API：https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/API/tabGroups
- Firefox 数据同意：https://extensionworkshop.com/documentation/develop/firefox-builtin-data-consent/
- Firefox 签名：https://extensionworkshop.com/documentation/publish/signing-and-distribution-overview/
- `web-ext`：https://extensionworkshop.com/documentation/develop/getting-started-with-web-ext/
- Chrome 原生消息：https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
- Edge 原生消息：https://learn.microsoft.com/en-us/microsoft-edge/extensions-chromium/developer-guide/native-messaging
