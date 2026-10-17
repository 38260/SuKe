# SuKe Chromium 浏览器连接器

用于溯刻的 Manifest V3 连接器，支持普通窗口的标签顺序、活动标签、固定标签和标签组；排除隐私窗口及不支持的URL类型。

## 安装与连接

当前没有本项目商店版本。在 `chrome://extensions/` 或 `edge://extensions/` 中使用开发者模式加载本目录。

扩展使用项目自己的公钥固定开发身份：`pbfiagcpaddcgecbpkohpbbfhapnmndl`。manifest中的`key`是公钥，不包含私钥。其机制见 [Chrome文档](https://developer.chrome.com/docs/extensions/reference/manifest/key)。

构建应用后，在「设置 → 浏览器集成」选择对应浏览器进行注册。也可手动执行项目脚本：

```powershell
./register-native-host.ps1 -ExtensionId pbfiagcpaddcgecbpkohpbbfhapnmndl -SuKePath 'C:/path/to/SuKe.exe' -Chrome -Edge
```

脚本只注册选定浏览器的当前用户原生通信主机 `com.suke.browser`，允许来源精确限定到扩展ID，不使用通配符。调用脚本前请核对可执行文件路径。

## 隐私与协议

权限为 `tabs`、`tabGroups`、`storage`、`nativeMessaging`。扩展保存一个不透明的本机配置标识，用于区分浏览器配置；不读取Cookie、密码、页面正文或浏览历史。

协议版本2支持 `capture`、`restore` 和 `ping`，桌面端通过 `SuKe.BrowserBridge` 命名管道通信。精确复用受浏览器配置标识和恢复策略约束，文件URL与未知配置可单独打开。

缺少原生主机时连接器保持空闲并给出诊断；注册与自动化单元测试不代表真实浏览器连接已验收。
