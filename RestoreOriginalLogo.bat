@echo off
title Mom's Pie - Restore Original Logo
echo ==========================================
echo   Mom's Pie - Restore Original Logo
echo ==========================================
echo.
choice /C YN /M "Restore the original Mom's Pie logo"
if errorlevel 2 exit /b 0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0_tool\run.ps1" restore
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
