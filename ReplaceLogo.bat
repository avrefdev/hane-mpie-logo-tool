@echo off
title Mom's Pie - Replace Sign Logo
echo ==========================================
echo   Mom's Pie - Replace Sign Logo
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
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0_tool\run.ps1" replace
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" if exist "%~dp0preview\logo_preview_on_dark.png" start "" "%~dp0preview\logo_preview_on_dark.png"
echo.
pause
exit /b %RESULT%
