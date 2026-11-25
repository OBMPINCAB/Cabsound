param(
    [string]$Version = '1.0.0',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$InstallerDir = $PSScriptRoot
$StageDir = Join-Path $RepoRoot 'dist\stage'
$DistDir = Join-Path $RepoRoot 'dist'
$SetupExe = Join-Path $DistDir 'CabSound-Setup.exe'
$ZipPath = Join-Path $DistDir "CabSound-v$Version-win-x64.zip"
$Csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $Csc)) { throw "C# compiler not found at $Csc" }

Write-Host "1/4 Running unit tests..."
dotnet test (Join-Path $RepoRoot 'tests\CabSound.Core.Tests\CabSound.Core.Tests.csproj') --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

if (-not $SkipPublish -or -not (Test-Path (Join-Path $StageDir 'CabSound.exe'))) {
    Write-Host "2/4 Publishing self-contained single-file CabSound.exe (win-x64)..."
    if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

    dotnet publish (Join-Path $RepoRoot 'src\CabSound.App\CabSound.App.csproj') `
        -c Release -r win-x64 --self-contained true --no-restore `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
        -o $StageDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
} else {
    Write-Host "2/4 Using existing staged CabSound.exe..."
}

Write-Host "3/4 Compiling CabSound-Setup.exe..."
if (Test-Path $SetupExe) { Remove-Item $SetupExe -Force }
$icon = Join-Path $RepoRoot 'src\CabSound.App\CabSound.ico'
$stagedExe = Join-Path $StageDir 'CabSound.exe'
$readme = Join-Path $InstallerDir 'README.txt'
$uninst = Join-Path $InstallerDir 'Uninstall-CabSound.ps1'
$setupCs = Join-Path $InstallerDir 'SetupApp.cs'

& $Csc /nologo /target:winexe /platform:x64 /optimize+ `
    "/out:$SetupExe" `
    "/win32icon:$icon" `
    "/reference:System.Windows.Forms.dll" `
    "/reference:System.Drawing.dll" `
    "/resource:$stagedExe,CabSound.exe" `
    "/resource:$readme,README.txt" `
    "/resource:$uninst,Uninstall-CabSound.ps1" `
    $setupCs
if ($LASTEXITCODE -ne 0) { throw "SetupApp.cs compilation failed." }

Write-Host "4/4 Creating distributable zip: $ZipPath"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
$ZipStage = Join-Path $DistDir 'zip-contents'
if (Test-Path $ZipStage) { Remove-Item $ZipStage -Recurse -Force }
New-Item -ItemType Directory -Path $ZipStage -Force | Out-Null
Copy-Item $SetupExe (Join-Path $ZipStage 'CabSound-Setup.exe') -Force
Copy-Item $readme (Join-Path $ZipStage 'README.txt') -Force

Compress-Archive -Path (Join-Path $ZipStage '*') -DestinationPath $ZipPath -CompressionLevel Optimal
Remove-Item $ZipStage -Recurse -Force
Remove-Item $StageDir -Recurse -Force

$setupMb = [Math]::Round((Get-Item $SetupExe).Length / 1MB, 1)
$zipMb = [Math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host "Done! Setup: $SetupExe ($setupMb MB), Zip: $ZipPath ($zipMb MB)"
