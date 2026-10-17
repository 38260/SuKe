# SuKe Firefox Connector

> 本文为 [firefox-extension/README.md](../../firefox-extension/README.md) 的中文翻译。注册表路径、附加组件 ID 与命令保持原样；若中英文出现歧义，以英文原文为准。

这是 SuKe 的 Firefox 专用配套附加组件。它使用 Firefox 的 WebExtension API 与原生消息来采集和恢复普通 Firefox 窗口、受支持的标签页 URL、固定与激活状态、标签组以及窗口边界。

Firefox 包有意与 Chromium 包分离。Firefox 使用 `background.scripts`，通过 Gecko 附加组件 ID 识别原生消息调用方，用 `allowed_extensions` 把它们加入允许列表，并在 Mozilla 的 Windows 注册表键下发现 host。

## 要求

- Firefox Desktop 142 或更新版本
- Windows 版 SuKe
- 已为附加组件 ID `suke-browser-connector@suke.app` 注册原生 host

不支持 Firefox for Android，因为它不提供原生消息能力。

## 安装

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。

## 本地测试配置

1. 构建或发布 `SuKe.exe`。
2. 在 SuKe 中选择 **Settings > Browser Integration > Set up Firefox**。这会在 `HKCU\Software\Mozilla\NativeMessagingHosts\com.suke.browser` 下注册 `%LOCALAPPDATA%\SuKe\native-host-manifest-firefox.json`。
3. 打开 `about:debugging#/runtime/this-firefox`，选择 **Load Temporary Add-on**，并选中本目录的 `manifest.json`。
4. 保存一个包含一个或多个普通 Firefox 窗口的工作区，然后恢复它。

手动注册：

```powershell
.\register-native-host.ps1 -SuKePath 'C:\path\to\SuKe.exe'
```

`manifest.json` 中固定的 Gecko 附加组件 ID 必须与 `allowed_extensions` 中的 ID 完全一致。Firefox 会把 host 清单路径与附加组件 ID 作为原生 host 的命令行参数传入；SuKe 使用这个确切的附加组件 ID 来选择 Firefox 桥。

## 校验与打包

安装 Mozilla 的 `web-ext`，然后在本目录运行以下命令：

```powershell
node --test .\tests\background.test.cjs
web-ext lint
web-ext build
```

`web-ext-config.mjs` 会把开发用与仅用于提交的文件排除在生成的 ZIP 之外。Firefox Release 与 Beta 要求 Mozilla 签名，因此未签名的 ZIP 只是提交或开发产物。

如需发布签名的 AMO 更新，请在 AMO Developer Hub 创建 API 凭据并运行：

```powershell
web-ext sign --channel=listed --amo-metadata=amo-metadata.json `
  --api-key=$env:AMO_JWT_ISSUER --api-secret=$env:AMO_JWT_SECRET
```

已检入的元数据提供上架摘要与 Firefox 分类。发布更新之前，请在 AMO 中复核上架文案、截图、支持联系方式与隐私政策 URL。桌面应用的 Firefox 配置动作必须持续指向上面那个确切的公开页面。

## 隐私与恢复边界

该附加组件声明了 Mozilla 的 `browsingActivity`、`websiteContent` 与 `personallyIdentifyingInfo` 数据类别，因为原生消息会把标签页 URL、标签页标题、标签组元数据以及一个不透明连接器配置文件密钥发送给本地 SuKe 应用。除非用户导出或同步其 SuKe 数据，这些数据都留在用户电脑上。详见 [PRIVACY.md](../../firefox-extension/PRIVACY.md)。

隐私窗口与标签页对附加组件不可见。它不请求也不读取 Cookie、密码、常规浏览历史、页面正文、下载记录、书签或 contextual identities。因此 Firefox 容器身份不会被恢复。`about:` 与扩展页面这类内部 URL 会被跳过。受支持的 URL 为 `http://`、`https://` 与 `file://`；精确匹配永远不会复用 `file://` 标签页。

恢复是按标签页尽力而为的。某个 URL 失败只会针对该浏览器窗口被报告，不会阻止其他有效标签页或其他工作区应用恢复。
