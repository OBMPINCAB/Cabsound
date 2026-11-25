@echo off
cd /d "%~dp0"
echo Running CabSound tests... (first run downloads packages)
dotnet test tests\CabSound.Core.Tests > test-results.txt 2>&1
type test-results.txt
echo.
echo Results saved to test-results.txt
pause
