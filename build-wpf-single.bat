@echo off
setlocal
cd /d "%~dp0"
call "%~dp0_dotnet-env.bat"
if errorlevel 1 exit /b 1

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
set "PUBLISH_ERROR=%ERRORLEVEL%"
if not "%PUBLISH_ERROR%"=="0" (
  echo.
  echo BUILD FAILED
  pause
  exit /b 1
)

echo.
echo OK: dist\wpf-single\ConfigAdmin.exe
echo.
