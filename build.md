# 溯刻 · SuKe 构建与验证

环境：Windows、.NET 8 SDK。命令从仓库根目录执行，工程为 `SuKe.sln`。

## 根目录一键启动

运行仓库根目录的 `start-suke.bat`（或在命令行执行 `start-suke.bat`）。入口会检查 .NET 8 SDK 和 Windows Desktop Runtime、构建当前 Release，并启动项目构建目标同步到仓库根目录的最新 `SuKe.exe`。支持 `start-suke.bat en` 切换启动语言、`--minimized` 等应用参数；`--dry-run` 只检查启动环境。

若检测到已有 `SuKe.exe` 进程，入口会停止并提示先正常退出已有程序，再次运行入口。它不会强制结束进程或覆盖正在使用的程序文件。

## 构建与测试

```powershell
dotnet restore SuKe.sln
dotnet build SuKe.sln -c Release
dotnet test SuKe.sln -c Release --no-restore
```

构建会同步可运行文件到仓库根目录；请先退出正在运行的程序。SDK不在PATH时使用已安装SDK的完整路径，本机可用 `D:/env/dotnet-sdk/dotnet.exe`。

## 独立验收构建

为避免覆盖运行中的程序或触发根目录文件权限调整，可在自己的输出目录创建以下 `IsolatedBuild.targets`：

```xml
<Project>
  <Target Name="CopySuKeToRepositoryRoot" />
  <Target Name="EnsureRootExecutableIntegrity" />
</Project>
```

```powershell
dotnet test SuKe.sln -c Release --artifacts-path out/verification `
  -p:CustomAfterMicrosoftCommonTargets=C:/absolute/path/IsolatedBuild.targets
dotnet build src/SuKe/SuKe.csproj -c Release --artifacts-path out/verification `
  -p:CustomAfterMicrosoftCommonTargets=C:/absolute/path/IsolatedBuild.targets --no-restore
```

主程序位于 `out/verification/bin/SuKe/release/SuKe.exe`。测试报告、构建成功与真实桌面恢复验收分别记录，不互相替代。

## 单文件发布

```powershell
dotnet publish src/SuKe/SuKe.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishReadyToRun=true --output out/publish
```

可执行文件为 `out/publish/SuKe.exe`。发布包仍需独立检查启动、托盘、数据兼容和真实浏览器连接。

## 浏览器连接器与终端脚本

```powershell
node --test firefox-extension/tests/background.test.cjs
```

连接器安装见 [Chromium说明](browser-extension/README.md) 与 [Firefox说明](firefox-extension/README.md)。当前没有本项目商店版本或已签名Firefox包；构建ZIP不代表签名或发布成功。

可选终端脚本为 `scripts/SuKe.Terminal.ps1`，仅由用户手动引入，不修改全局profile。

GitHub Release工作流从当前仓库执行测试并生成SuKe名称的应用、扩展和终端脚本资产。触发发布与资产上传需单独授权；本地构建不会发布。
