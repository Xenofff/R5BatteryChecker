@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Error: csc.exe not found at %CSC%
    pause
    exit /b 1
)

echo Compiling R5BatteryChecker.exe...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /out:R5BatteryChecker.exe Program.cs

if %ERRORLEVEL% equ 0 (
    echo Build successful: R5BatteryChecker.exe
) else (
    echo Build failed!
)
