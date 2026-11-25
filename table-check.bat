@echo off
rem CabSound TableCheck: sound/SSF checks for VPX tables.
rem   table-check scan              all tables (report file in this folder; changes nothing)
rem   table-check check "<table.vpx>"
rem   table-check stereo ["<table.vpx>"]   what the old mono step changed (all tables: docs\tables-stereo-report.md; changes nothing)
rem   table-check unmono "<table.vpx>"   give those sounds their stereo back (backup in App\backups first; VPX and Popper must be closed)
rem   table-check backglass "<table.vpx>" SFX_* "Some Callout"   (those sounds to the backbox; same safety steps)
rem   table-check table "<table.vpx>" doomboom_*   (those backbox sounds back to the table: exciters + shaker; same safety steps)
rem   table-check restore "<table.vpx>" "<backup.vpx>" name prefix*   (audio of those sounds back from the backup, e.g. stereo; output kept)
cd /d "%~dp0"
dotnet build tools\CabSound.TableCheck -c Release > build-results.txt 2>&1
if errorlevel 1 (
  type build-results.txt
  echo BUILD FAILED - results saved to build-results.txt
  exit /b 1
)
dotnet run --project tools\CabSound.TableCheck -c Release --no-build -- %*
