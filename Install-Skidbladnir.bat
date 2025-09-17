@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-Skidbladnir.ps1" %*
set ERR=%ERRORLEVEL%
if %ERR% NEQ 0 (
    echo.
    echo Installation failed. Review the messages above for details.
) else (
    echo.
    echo Skidbladnir Image Processor installation completed successfully.
)
pause
exit /B %ERR%
