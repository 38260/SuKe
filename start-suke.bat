@echo off
setlocal EnableExtensions
title SuKe Start

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\SuKe\SuKe.csproj"
set "EXE=%ROOT%SuKe.exe"
set "DOTNET=D:\env\dotnet-sdk\dotnet.exe"
set "UI_LANG=zh"
set "DRY_RUN=0"

if /i "%~1"=="en" set "UI_LANG=en"
if /i "%~1"=="--english" set "UI_LANG=en"
if /i "%~1"=="--dry-run" set "DRY_RUN=1"
if /i "%~1"=="-n" set "DRY_RUN=1"

if not exist "%PROJECT%" goto project_missing
if not exist "%DOTNET%" set "DOTNET="
if not defined DOTNET for /f "delims=" %%D in ('where.exe dotnet 2^>nul') do if not defined DOTNET set "DOTNET=%%D"
if not defined DOTNET goto sdk_missing

"%DOTNET%" --list-sdks | findstr /b "8." >nul
if errorlevel 1 goto sdk_missing
"%DOTNET%" --list-runtimes | findstr /b /c:"Microsoft.WindowsDesktop.App 8." >nul
if errorlevel 1 goto runtime_missing

if "%DRY_RUN%"=="1" goto dry_run

powershell.exe -NoProfile -NonInteractive -Command "if (Get-Process -Name SuKe -ErrorAction SilentlyContinue) { exit 2 }; exit 0"
if errorlevel 2 goto already_running
if errorlevel 1 goto process_check_failed

echo Building the latest Release version...
pushd "%ROOT%"
"%DOTNET%" build "%PROJECT%" --configuration Release
set "BUILD_RESULT=%ERRORLEVEL%"
popd
if not "%BUILD_RESULT%"=="0" goto build_failed
if not exist "%EXE%" goto exe_missing

echo Starting the root-level SuKe.exe...
set "SUKE_UI_LANG=%UI_LANG%"
set "SUKE_EXPECTED_EXE=%EXE%"
start "" /d "%ROOT%" "%EXE%" %*
if errorlevel 1 goto start_failed

powershell.exe -NoProfile -NonInteractive -Command "$expected=$env:SUKE_EXPECTED_EXE; $deadline=[DateTime]::UtcNow.AddSeconds(10); do { $p=Get-Process -Name SuKe -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $expected }; if ($p) { exit 0 }; Start-Sleep -Milliseconds 250 } while ([DateTime]::UtcNow -lt $deadline); exit 1"
if errorlevel 1 goto start_failed

echo SuKe is running from the project root.
exit /b 0

:dry_run
echo Environment check passed. Running this launcher will build Release and start the root-level SuKe.exe.
exit /b 0

:already_running
echo SuKe is already running. No processes were terminated.
echo Close the existing instance normally, then run this launcher to build and start the latest version.
exit /b 2

:project_missing
echo [ERROR] Project file not found: "%PROJECT%"
goto failed

:sdk_missing
echo [ERROR] A .NET 8 SDK was not found. Install one or add dotnet.exe to PATH.
goto failed

:runtime_missing
echo [ERROR] The .NET 8 Windows Desktop Runtime is required to run this WPF application.
goto failed

:process_check_failed
echo [ERROR] Could not check whether SuKe is already running. No build or process termination was attempted.
goto failed

:build_failed
echo [ERROR] Release build failed. SuKe was not started.
goto failed

:exe_missing
echo [ERROR] The build succeeded but did not create the root-level SuKe.exe.
goto failed

:start_failed
echo [ERROR] SuKe did not start successfully. Check the application log under %APPDATA%\SuKe.
goto failed

:failed
pause
exit /b 1