@echo off
rem Постоянный Hub URL через Tailscale Funnel (порт 18443).
rem Первый раз: powershell -ExecutionPolicy Bypass -File setup-tailscale-funnel.ps1

setlocal
cd /d "%~dp0"

set "TS=%ProgramFiles%\Tailscale\tailscale.exe"
if not exist "%TS%" (
  echo Tailscale not installed. Run setup-tailscale-funnel.ps1
  pause
  exit /b 1
)

set "HUBURL=%APPDATA%\ConfigAdmin\sync-tunnel.url"
if exist "%HUBURL%" (
  echo Hub URL for RDP:
  type "%HUBURL%"
  echo.
)

echo Starting Tailscale Funnel on port 18443...
echo ConfigAdmin must run in Hub mode. Keep Tailscale running.
"%TS%" funnel --bg 18443
if errorlevel 1 (
  echo.
  echo If not logged in, run: setup-tailscale-funnel.ps1
  pause
  exit /b 1
)

echo Funnel started in background.
pause
