<#
.SYNOPSIS
    Publishes SpotiTube Kiosk and compiles the Windows installer.

.DESCRIPTION
    1. Runs `dotnet publish` to produce a self-contained, single-file win-x64 build.
    2. Compiles installer\SpotiTube.Kiosk.iss with Inno Setup (ISCC.exe) into
       installer\output\SpotiTube.Kiosk.Setup.exe.

    Requires Inno Setup 6 (https://jrsoftware.org/isdl.php) to be installed for step 2.

.PARAMETER Version
    Version number to stamp on the installer (default 1.0.0).

.PARAMETER Configuration
    Build configuration to publish (default Release).

.EXAMPLE
    installer\build.ps1
    installer\build.ps1 -Version 1.2.0
#>
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src\SpotiTube.Kiosk\SpotiTube.Kiosk.csproj"
$publishDir = Join-Path $repoRoot "publish"
$issFile = Join-Path $PSScriptRoot "SpotiTube.Kiosk.iss"

Write-Host "Publishing SpotiTube.Kiosk ($Configuration, win-x64, self-contained, single file)..." -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $csproj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php and re-run this script."
}

Write-Host "Compiling installer with $iscc..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" $issFile
if ($LASTEXITCODE -ne 0) {
    throw "ISCC compile failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Installer written to installer\output\SpotiTube.Kiosk.Setup.exe" -ForegroundColor Green
