@echo off
title Mom's Pie - Restore Original Logo
echo ==========================================
echo   Mom's Pie - Restore Original Logo
echo ==========================================
echo.
if exist "%~dp0_tool\run.ps1" goto :start
echo ERROR: The tool files were not found next to this .bat file.
echo.
echo This usually happens when the .bat is opened directly from a ZIP/RAR archive
echo or from Discord, instead of from the extracted folder.
echo.
echo How to fix:
echo   1. Extract the whole "logo_tool" folder first.
echo   2. Open the extracted "logo_tool" folder.
echo   3. Run the .bat file from inside that folder.
echo.
pause
exit /b 1
:start
choice /C YN /M "Restore the original Mom's Pie logo"
if errorlevel 2 exit /b 0
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0_tool\run.ps1" restore
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
