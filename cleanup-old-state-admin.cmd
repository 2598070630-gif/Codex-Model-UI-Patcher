@echo off
setlocal

net session >nul 2>&1
if errorlevel 1 (
  echo Please right-click this file and choose "Run as administrator".
  pause
  exit /b 1
)

set "LOCAL_STATE=%LOCALAPPDATA%\CodexModelUIPatcher"
set "PROGRAM_STATE=C:\ProgramData\CodexModelUIPatcher"

if exist "%LOCAL_STATE%" (
  rmdir /s /q "%LOCAL_STATE%"
)

if exist "%PROGRAM_STATE%" (
  takeown /f "%PROGRAM_STATE%" /r /d y >nul
  icacls "%PROGRAM_STATE%" /grant Administrators:F /t /c >nul
  rmdir /s /q "%PROGRAM_STATE%"
)

if exist "%PROGRAM_STATE%" (
  echo Failed to delete "%PROGRAM_STATE%".
  pause
  exit /b 1
)

echo Old CodexModelUIPatcher state removed.
pause
