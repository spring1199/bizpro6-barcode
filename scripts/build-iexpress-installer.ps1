# Builds a Windows installer EXE using Inno Setup.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "3.0.0"
)

$ErrorActionPreference = "Stop"

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$DistDir     = Join-Path $ProjectRoot "dist"
$PublishDir  = Join-Path $DistDir "publish"
$InstallerDir = Join-Path $DistDir "installer"
$IssFile     = Join-Path $ProjectRoot "setup\installer.iss"
$OutputExe   = Join-Path $InstallerDir "BizPro6_Barcode_Setup_$Version.exe"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  BizPro6 Barcode Installer Build (Inno Setup)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Find Inno Setup compiler
$isccPaths = @(
    "C:\Users\mooji\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install from https://jrsoftware.org/isdl.php"
}
Write-Host "[Setup] Using Inno Setup: $iscc" -ForegroundColor Gray
Write-Host ""

# Build and publish the application
& (Join-Path $ScriptDir "build-and-pack.ps1") -Configuration $Configuration -Runtime $Runtime

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

# Clean installer output
if (Test-Path $InstallerDir) {
    Remove-Item -LiteralPath $InstallerDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

# Compile installer
Write-Host "[Installer] Compiling with Inno Setup..." -ForegroundColor Yellow
& $iscc `
    "/DMyAppVersion=$Version" `
    "/O$InstallerDir" `
    $IssFile

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed (exit code $LASTEXITCODE)"
}

if (-not (Test-Path $OutputExe)) {
    throw "Installer output was not created: $OutputExe"
}

$sizeMb = [math]::Round(((Get-Item $OutputExe).Length / 1MB), 2)
Write-Host ""
Write-Host "Installer ready: $OutputExe" -ForegroundColor Green
Write-Host "Installer size:  $sizeMb MB" -ForegroundColor Green
