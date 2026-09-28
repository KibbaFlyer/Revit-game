@echo off
dotnet "%~dp0package\Demo\BimArena.Demo.dll"
if errorlevel 1 (
  echo The demo requires the .NET 10 Windows Desktop Runtime.
  pause
)
