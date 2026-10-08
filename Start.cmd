@echo off
cd /d "%~dp0"
if not exist "%~dp0dist\Keyside.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    pause
    exit /b 1
  )
)
start "" "%~dp0dist\Keyside.exe"
