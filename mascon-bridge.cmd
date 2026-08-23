@echo off
rem ---------------------------------------------------------------------------
rem  Opens the bridge control panel. Double click and accept the UAC prompt.
rem
rem  For the console mode instead:  mascon-bridge.exe run
rem
rem  This elevates itself BEFORE launching the exe. mascon-bridge looks for
rem  config.json in the working directory, and when the exe elevates on its own
rem  that directory can end up being system32.
rem ---------------------------------------------------------------------------

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

rem Released builds sit next to this script; developer builds are under bin.
set "EXE=%~dp0mascon-bridge.exe"
if not exist "%EXE%" set "EXE=%~dp0bin\Release\net10.0-windows10.0.26100.0\win-x64\mascon-bridge.exe"

if not exist "%EXE%" (
    echo ERROR: executable not found. Build it with: dotnet build -c Release
    pause
    exit /b 1
)

start "" "%EXE%"
