@echo off

del "gmdSetup.exe" 2> nul

powershell -ExecutionPolicy RemoteSigned -File .\Build.ps1 -configuration "Release" -Target Build-Setup

echo.
echo.
pause