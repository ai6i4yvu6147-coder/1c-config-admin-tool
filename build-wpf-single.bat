@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin WPF — single-file exe ===
echo     (self-contained, все DLL упакованы внутрь)
echo.

dotnet publish "src\ConfigAdmin.Wpf\ConfigAdmin.Wpf.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "dist\wpf-single"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

echo.
echo OK: dist\wpf-single\ConfigAdmin.exe
echo.
