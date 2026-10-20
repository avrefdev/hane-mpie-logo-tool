@echo off
title Mom's Pie - Replace Sign Logo
echo ==========================================
echo   Mom's Pie - Replace Sign Logo
echo ==========================================
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0_tool\run.ps1" replace
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" if exist "%~dp0preview\logo_preview_on_dark.png" start "" "%~dp0preview\logo_preview_on_dark.png"
echo.
pause
exit /b %RESULT%
