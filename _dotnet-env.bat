@echo off
rem User-installed SDK (dotnet-install.ps1) must precede system runtime-only host.
set "PATH=%USERPROFILE%\.dotnet;%PATH%"

dotnet --list-sdks 2>nul | findstr /R /C:"8\.0\." >nul
if errorlevel 1 (
  echo.
  echo ERROR: .NET 8 SDK not found.
  echo.
  echo Install SDK: https://dotnet.microsoft.com/download/dotnet/8.0
  echo Or run in PowerShell:
  echo   Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
  echo   .\dotnet-install.ps1 -Channel 8.0
  echo.
  pause
  exit /b 1
)
