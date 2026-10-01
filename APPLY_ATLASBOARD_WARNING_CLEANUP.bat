@echo off
setlocal
cd /d "%~dp0"

echo.
echo Atlas Board Unity 6.5 Warning Cleanup
echo ================================================
echo Updates deprecated Unity/TMP APIs and removes the
echo LiberationSans ellipsis fallback warnings.
echo A backup will be saved under Library.
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Apply-AtlasBoardUnity65WarningCleanup.ps1"

set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
  echo Warning cleanup completed. Return to Unity and wait for compilation.
) else (
  echo Warning cleanup returned error code %EXITCODE%.
  echo Copy this window output back to ChatGPT.
)

echo.
pause
exit /b %EXITCODE%
