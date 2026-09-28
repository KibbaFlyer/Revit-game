@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install.ps1"
if errorlevel 1 (
  echo Installation did not complete. Read the message above.
  pause
  exit /b 1
)
pause
