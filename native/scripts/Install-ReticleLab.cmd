@echo off
setlocal
set "RETICLE_PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "RETICLE_PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%RETICLE_PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-dev.ps1" %*
set "RETICLE_EXIT=%ERRORLEVEL%"
echo.
echo Installer exit code: %RETICLE_EXIT%
if "%RETICLE_EXIT%"=="2" echo Cancelled before installation. Run this script again and type INSTALL to continue.
echo See install-logs beside the MSIX package for details.
pause
exit /b %RETICLE_EXIT%
