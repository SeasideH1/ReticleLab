@echo off
setlocal
set "RETICLE_PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "RETICLE_PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%RETICLE_PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-dev.ps1" -UpdateOnly %*
set "RETICLE_EXIT=%ERRORLEVEL%"
echo.
echo Update exit code: %RETICLE_EXIT%
echo See install-logs for details. Existing settings are retained.
pause
exit /b %RETICLE_EXIT%
