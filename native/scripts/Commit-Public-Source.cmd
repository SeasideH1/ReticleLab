@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Commit-Public-Source.ps1"
set "RETICLE_EXIT=%ERRORLEVEL%"
pause
exit /b %RETICLE_EXIT%
