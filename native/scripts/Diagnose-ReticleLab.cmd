@echo off
call "%~dp0Install-ReticleLab.cmd" -CheckOnly
exit /b %ERRORLEVEL%
