@echo off
rem Double-click launcher: boots the Android emulator and starts Casino Tracker in it.
rem Options are passed through, e.g.  Start-Emulator.cmd -NoApp   or   Start-Emulator.cmd -Wipe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Start-Emulator.ps1" %*
if errorlevel 1 (
    echo.
    echo Something went wrong. See the messages above.
    pause
)
