@echo off
REM Quartermaster General - Launch Two Clients for Multiplayer Testing
REM This batch file is a wrapper for the PowerShell script

echo Building and launching multiplayer test...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0launch_multiplayer.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Script failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Done!
pause
