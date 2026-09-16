@echo off
setlocal

rem Build the Unity project next to this script using Unity.exe from PATH.
set "SAMPLE_PROJECT=%~dp0"
set "SAMPLE_OUTPUT=%~dp0Builds\Windows\UnitySample.exe"
set "SAMPLE_LOG=%~dp0build.log"

where Unity.exe >nul 2>&1
if errorlevel 1 (
    echo ERROR: Unity.exe was not found in PATH.
    exit /b 1
)

if not exist "%SAMPLE_PROJECT%ProjectSettings\ProjectVersion.txt" (
    echo ERROR: This script must be located at the Unity project root.
    exit /b 1
)

echo Save your scene and close this project's Unity Editor before building.
echo The scene must be enabled in Build Profiles / Scene List.
echo Building Windows x64...

rem START /WAIT waits for the graphical Unity executable to finish in any invocation context.
start "" /wait Unity.exe -batchmode -quit -projectPath "%SAMPLE_PROJECT%." -buildWindows64Player "%SAMPLE_OUTPUT%" -logFile "%SAMPLE_LOG%"
set "SAMPLE_RESULT=%ERRORLEVEL%"

if not "%SAMPLE_RESULT%"=="0" (
    echo ERROR: Unity build failed with exit code %SAMPLE_RESULT%.
    echo See "%SAMPLE_LOG%".
    exit /b %SAMPLE_RESULT%
)

echo Build completed: "%SAMPLE_OUTPUT%"
exit /b 0
