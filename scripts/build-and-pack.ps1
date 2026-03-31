# BarTenderClone Build and Pack Script
# This script creates a self-contained publish folder for Inno Setup

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $ProjectRoot "BarTenderClone\BarTenderClone.csproj"
$DistDir = Join-Path $ProjectRoot "dist"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  BarTenderClone Build and Pack Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Clean dist directory
if (Test-Path $DistDir) {
    Write-Host "[1/3] Cleaning dist directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $DistDir
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# Publish as self-contained
Write-Host "[2/3] Publishing application..." -ForegroundColor Yellow
Write-Host "       Configuration: $Configuration" -ForegroundColor Gray
Write-Host "       Runtime: $Runtime" -ForegroundColor Gray
Write-Host "       Output: $DistDir" -ForegroundColor Gray
Write-Host ""

$PublishDir = Join-Path $DistDir "publish"

dotnet publish $ProjectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Clean up unnecessary files
Write-Host "[3/3] Cleaning up..." -ForegroundColor Yellow
Get-ChildItem $PublishDir -Filter "*.pdb" | Remove-Item -Force

# Show result
$TotalSize = (Get-ChildItem $PublishDir -Recurse | Measure-Object -Property Length -Sum).Sum
$TotalSizeMB = [math]::Round($TotalSize / 1MB, 2)

Write-Host ""
Write-Host "================================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output folder: $PublishDir" -ForegroundColor White
Write-Host "Total size: $TotalSizeMB MB" -ForegroundColor White
Write-Host ""
Write-Host "This folder contains the self-contained application." -ForegroundColor Cyan
Write-Host "Use Inno Setup to create the final installer." -ForegroundColor Cyan
Write-Host ""
