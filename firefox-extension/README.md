# SuKe Firefox 浏览器连接器

用于溯刻的Firefox专用连接器，与Chromium使用独立的通信管道。扩展标识为 `suke-browser-connector@suke.app`，原生通信主机为 `com.suke.browser`。

## 开发安装

当前没有本项目的AMO商店发布或已签名版本。开发时可按 [Firefox临时安装说明](https://extensionworkshop.com/documentation/develop/temporary-installation-in-firefox/) 在 `about:debugging` 的「此Firefox」中临时加载本目录的 `manifest.json`。临时安装不等价于签名分发，浏览器重启后需要重新加载。

普通分发需要完成 [Mozilla签名流程](https://extensionworkshop.com/documentation/publish/signing-and-distribution-overview/)，请勿关闭浏览器签名验证。

在应用的「设置 → 浏览器集成」选择Firefox可注册当前用户的原生通信主机并打开 `about:addons`。也可使用本目录的 `register-native-host.ps1`，参数为 `-SuKePath 'C:/path/to/SuKe.exe'`。

## 本地验证

```powershell
node --test tests/background.test.cjs
```

连接器使用 `SuKe.BrowserBridge.Firefox` 管道。捕获和恢复会遵守标签的配置标识、受支持URL与普通窗口边界；单元测试不代替真实Firefox连接或签名版本验证。
