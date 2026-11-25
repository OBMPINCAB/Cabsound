@echo off
cd /d "%~dp0"
echo Running tests...
dotnet test tests\CabSound.Core.Tests > test-results.txt 2>&1
findstr /C:"Passed!" /C:"Failed!" test-results.txt
echo Building CabSound Probe...
dotnet build tools\CabSound.Probe -c Release > build-results.txt 2>&1
if errorlevel 1 (
  type build-results.txt
  echo.
  echo BUILD FAILED - results saved to build-results.txt
  pause
  exit /b 1
)
echo Starting probe. Log is saved to probe-log.txt
dotnet run --project tools\CabSound.Probe -c Release --no-build
