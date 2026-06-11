@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin Console — single-file exe ===
echo     (self-contained, все DLL упакованы внутрь)
echo.

dotnet publish "src\ConfigAdmin.Console\ConfigAdmin.Console.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "dist\console-single"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

echo.
echo OK: dist\console-single\configadmin.exe
echo.
