@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 正在编译 HPToy...
dotnet build HPToy.Win\HPToy.Win.csproj -c Debug
if errorlevel 1 (
    echo.
    echo 编译失败。请先安装 .NET 10 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
echo.
echo 编译成功，正在启动...
start "" "HPToy.Win\bin\Debug\net10.0-windows10.0.19041.0\HPToy.exe"
