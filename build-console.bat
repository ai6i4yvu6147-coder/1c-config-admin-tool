@echo off
setlocal
cd /d "%~dp0"
call "%~dp0_dotnet-env.bat"
if errorlevel 1 exit /b 1

echo.
echo === ConfigAdmin Console (configadmin.exe) ===
echo.

dotnet publish "src\ConfigAdmin.Console\ConfigAdmin.Console.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "dist\console"
set "PUBLISH_ERROR=%ERRORLEVEL%"
if not "%PUBLISH_ERROR%"=="0" (
  echo.
  echo BUILD FAILED
  pause
  exit /b 1
)

echo.
echo OK: dist\console\configadmin.exe
echo.
