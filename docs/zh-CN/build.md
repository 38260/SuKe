# SuKe — 构建、测试与发布

> 本文为 [build.md](../../build.md) 的中文翻译。所有命令保持原样，请以英文原文为准。

SuKe 面向 .NET 8 与 Windows x64。所有命令请在仓库根目录执行。

## 校验服务层

```powershell
dotnet test SuKe.sln --configuration Release
```

该测试套件覆盖：持久化迁移与原子性、窗口策略与身份匹配、纯恢复计划、审批投影、过期计划检测、执行边界，以及手动预览模型。同时仿真了包更新与恢复后位置接受、DPI 噪声、应用主动移动窗口、已关闭的 HWND、有界重试、检查点保留/过期、损坏隔离、持久化失败、「先切换后关闭」的顺序、撤销的撤销安全性、原生任务窗口的样式/cloaking 策略、仅后台运行的进程、会话级 HWND 多重性、受限的 Squirrel 版本路径重绑定、可选的预览/检查点策略、可见边框边缘对齐、显式恢复模式行为、按条目的策略覆盖、新窗口关联、仅预览不改动状态，以及与其自身启动成功活动相关联的就绪等待。默认套件不会移动或启动真实的桌面窗口。

也可以只运行确定性的「恢复仿真」子集，或查看某一条脱敏后的规划器结果，而完全不查询、不改变桌面：

```powershell
dotnet test SuKe.sln --configuration Release --no-restore `
  --filter "FullyQualifiedName~RestoreSimulationTests"
dotnet .\src\SuKe\bin\Release\net8.0-windows\SuKe.dll `
  --simulate-restore .\tests\SuKe.Tests\Fixtures\restore-simulations\single-monitor.json
```

可选的 Windows 11 文件资源管理器集成关卡会创建一个临时资源管理器窗口并关闭它、恢复三个文件夹标签页、校验激活标签页，并重复一次恢复以证明不会产生重复标签页：

```powershell
$env:SUKE_LIVE_EXPLORER_SMOKE = "1"
dotnet test tests/SuKe.Tests/SuKe.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Live_windows_11_explorer_tab_restore_smoke_when_enabled"
```

## 完整全新构建（Debug）

删除生成产物并重新构建：

```powershell
Remove-Item -Recurse -Force src\SuKe\bin, src\SuKe\obj -ErrorAction SilentlyContinue
dotnet restore src\SuKe\SuKe.csproj
dotnet build src\SuKe\SuKe.csproj -c Debug
```

输出 exe：
```
src\SuKe\bin\Debug\net8.0-windows\SuKe.exe
```

---

## 完整全新构建（Release — 单个自包含 exe）

```powershell
Remove-Item -Recurse -Force src\SuKe\bin, src\SuKe\obj -ErrorAction SilentlyContinue
dotnet restore src\SuKe\SuKe.csproj
dotnet publish src\SuKe\SuKe.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

输出 exe：
```
src\SuKe\bin\Release\net8.0-windows\win-x64\publish\SuKe.exe
```

---

## 一行命令（Debug，可直接复制粘贴）

```powershell
Remove-Item -Recurse -Force src\SuKe\bin, src\SuKe\obj -ErrorAction SilentlyContinue; dotnet build src\SuKe\SuKe.csproj -c Debug
```

## 一行命令（Release 发布，可直接复制粘贴）

```powershell
Remove-Item -Recurse -Force src\SuKe\bin, src\SuKe\obj -ErrorAction SilentlyContinue; dotnet publish src\SuKe\SuKe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

## 发布产物

```powershell
$tag = "v<release-version>"
Copy-Item src/SuKe/bin/Release/net8.0-windows/win-x64/publish/SuKe.exe "SuKe-$tag.exe"
Compress-Archive browser-extension/* "SuKe-Browser-Connector-$tag.zip"
Push-Location firefox-extension
npx --yes web-ext@10.7.0 lint
npx --yes web-ext@10.7.0 build --filename "suke-firefox-connector-amo-$tag.zip" --artifacts-dir ..
Pop-Location
Move-Item "suke-firefox-connector-amo-$tag.zip" "SuKe-Firefox-Connector-AMO-$tag.zip"
Copy-Item scripts/SuKe.Terminal.ps1 "SuKe-Terminal-Integration-$tag.ps1"
Get-FileHash -Algorithm SHA256 "SuKe-$tag.exe", "SuKe-Browser-Connector-$tag.zip", "SuKe-Firefox-Connector-AMO-$tag.zip", "SuKe-Terminal-Integration-$tag.ps1"
```

当前使用与连接器安装说明见 [项目说明](../../README.md)。历史分发记录不代表本项目已发布商店版本或签名包。