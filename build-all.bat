@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin: publish console + WPF ===
echo.

call "%~dp0build-console.bat"
if errorlevel 1 exit /b 1

call "%~dp0build-wpf.bat"
if errorlevel 1 exit /b 1

echo.
echo === All builds completed ===
echo   dist\console\configadmin.exe
echo   dist\wpf\ConfigAdmin.exe
echo.
pause
