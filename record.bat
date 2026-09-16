@echo off
setlocal

rem Record camera 1 and the application screen, then exit after finalization.
set "SAMPLE_EXE=%~dp0Builds\Windows\UnitySample.exe"

if not exist "%SAMPLE_EXE%" (
    echo ERROR: UnitySample.exe is missing. Run build.bat first.
    exit /b 1
)

echo Recording camera 1 and screen for 10 seconds: 4K render/output, up to 60 FPS, VSync, MSAA 4x, preset P5.
start "" /wait "%SAMPLE_EXE%" --purge --render 4k --resolution 4k --fps 60 --vsync on --aa 4 --preset p5 --record camera1,screen --duration 10 --quit-after-recording
set "SAMPLE_RESULT=%ERRORLEVEL%"
powershell.exe -NoProfile -File "%~dp0show-record-stats.ps1" -OutputDirectory "%~dp0Builds\Windows\output"
if not "%SAMPLE_RESULT%"=="0" (
    echo ERROR: UnitySample exited with code %SAMPLE_RESULT%.
    exit /b %SAMPLE_RESULT%
)
echo Session finished. Videos and statistics: "%~dp0Builds\Windows\output"
exit /b 0
