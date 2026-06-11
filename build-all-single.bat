@echo off
setlocal
cd /d "%~dp0"

echo.
echo === ConfigAdmin: single-file publish (console + WPF) ===
echo.

call "%~dp0build-console-single.bat"
if errorlevel 1 exit /b 1

call "%~dp0build-wpf-single.bat"
if errorlevel 1 exit /b 1

echo.
echo === All single-file builds completed ===
echo   dist\console-single\configadmin.exe
echo   dist\wpf-single\ConfigAdmin.exe
echo.
