@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin WPF (ConfigAdmin.exe) ===
echo.

dotnet publish "src\ConfigAdmin.Wpf\ConfigAdmin.Wpf.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "dist\wpf"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

echo.
echo OK: dist\wpf\ConfigAdmin.exe
echo.
