@echo off
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0sim\tools\start-preview.ps1" -AutoPort -Open %*
if errorlevel 1 pause
