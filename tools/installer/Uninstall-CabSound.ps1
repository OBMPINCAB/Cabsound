Add-Type -AssemblyName System.Windows.Forms

$installDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$confirm = [System.Windows.Forms.MessageBox]::Show(
    "Uninstall CabSound from:`n$installDir ?",
    "Uninstall CabSound", 'YesNo', 'Question')
if ($confirm -ne 'Yes') { exit 0 }

$running = Get-Process -Name CabSound -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 600
}

$keepData = [System.Windows.Forms.MessageBox]::Show(
    "Keep your saved levels (CabSound.json) and backups folder?",
    "Uninstall CabSound", 'YesNo', 'Question')

# Remove shortcuts
Remove-Item -Path (Join-Path ([Environment]::GetFolderPath('Desktop')) 'CabSound.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path ([Environment]::GetFolderPath('Startup')) 'CabSound.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'CabSound') -Recurse -Force -ErrorAction SilentlyContinue

# Remove Add/Remove Programs entry
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CabSound' -Force -ErrorAction SilentlyContinue

# Remove installed binaries
Remove-Item -Path (Join-Path $installDir 'CabSound.exe') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $installDir 'README.txt') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $installDir 'CabSound.log') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $installDir 'CabSound.log.old') -Force -ErrorAction SilentlyContinue

if ($keepData -ne 'Yes') {
    Remove-Item -Path (Join-Path $installDir 'CabSound.json') -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $installDir 'backups') -Recurse -Force -ErrorAction SilentlyContinue
}

[System.Windows.Forms.MessageBox]::Show("CabSound has been uninstalled.", "Uninstall CabSound", 'OK', 'Information') | Out-Null
