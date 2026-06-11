@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin Console (configadmin.exe) ===
echo.

dotnet publish "src\ConfigAdmin.Console\ConfigAdmin.Console.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "dist\console"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

echo.
echo OK: dist\console\configadmin.exe
echo.
