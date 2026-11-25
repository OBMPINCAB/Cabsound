@echo off
rem Runs the tests, then publishes CabSound as one exe to App\ (data files live next to it).
cd /d "%~dp0"
echo Running tests...
dotnet test tests\CabSound.Core.Tests > test-results.txt 2>&1
findstr /C:"Passed!" /C:"Failed!" test-results.txt
findstr /C:"Failed!" test-results.txt >nul && (echo TESTS FAILED - not publishing & pause & exit /b 1)
echo Publishing CabSound to App\ ...
dotnet publish src\CabSound.App -c Release -o App -p:PublishSingleFile=true --self-contained false > build-results.txt 2>&1
if errorlevel 1 (
  type build-results.txt
  echo PUBLISH FAILED
  pause
  exit /b 1
)
echo Done: App\CabSound.exe
