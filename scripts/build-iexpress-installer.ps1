# Builds a per-user Windows installer EXE using IExpress.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "3.0.0"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectRoot "dist"
$PublishDir = Join-Path $DistDir "publish"
$InstallerDir = Join-Path $DistDir "installer"
$IExpressWorkDir = Join-Path $DistDir "iexpress"
$PayloadZip = Join-Path $IExpressWorkDir "payload.zip"
$InstallCmd = Join-Path $IExpressWorkDir "install.cmd"
$InstallPs1 = Join-Path $IExpressWorkDir "install.ps1"
$SedFile = Join-Path $IExpressWorkDir "package.sed"
$OutputExe = Join-Path $InstallerDir "BizPro6_Barcode_Setup_$Version.exe"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  BizPro6 Barcode IExpress Installer Build" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

& (Join-Path $ScriptDir "build-and-pack.ps1") -Configuration $Configuration -Runtime $Runtime

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

if (Test-Path $InstallerDir) {
    Remove-Item -LiteralPath $InstallerDir -Recurse -Force
}
if (Test-Path $IExpressWorkDir) {
    Remove-Item -LiteralPath $IExpressWorkDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null
New-Item -ItemType Directory -Path $IExpressWorkDir -Force | Out-Null

$installPs1Content = @'
param(
    [string]$SourceZip = "payload.zip"
)

$ErrorActionPreference = "Stop"

$appName = "BizPro6 Barcode"
$appExe = "BizPro6Barcode.exe"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath("Programs")) "$appName.lnk"
$sourceZipPath = Join-Path $PSScriptRoot $SourceZip
$tempExtract = Join-Path $env:TEMP ("BizPro6Barcode_" + [guid]::NewGuid().ToString("N"))

if (-not (Test-Path $sourceZipPath)) {
    throw "Payload archive not found: $sourceZipPath"
}

if (Test-Path $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null

try {
    Expand-Archive -LiteralPath $sourceZipPath -DestinationPath $tempExtract -Force
    Copy-Item -Path (Join-Path $tempExtract "*") -Destination $installDir -Recurse -Force

    $wshell = New-Object -ComObject WScript.Shell

    $desktop = $wshell.CreateShortcut($desktopShortcut)
    $desktop.TargetPath = Join-Path $installDir $appExe
    $desktop.WorkingDirectory = $installDir
    $desktop.IconLocation = Join-Path $installDir $appExe
    $desktop.Save()

    $startMenu = $wshell.CreateShortcut($startMenuShortcut)
    $startMenu.TargetPath = Join-Path $installDir $appExe
    $startMenu.WorkingDirectory = $installDir
    $startMenu.IconLocation = Join-Path $installDir $appExe
    $startMenu.Save()

    Start-Process -FilePath (Join-Path $installDir $appExe)
}
finally {
    if (Test-Path $tempExtract) {
        Remove-Item -LiteralPath $tempExtract -Recurse -Force
    }
}
'@

$installCmdContent = @'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" "payload.zip"
exit /b %errorlevel%
'@

Set-Content -LiteralPath $InstallPs1 -Value $installPs1Content -Encoding ASCII
Set-Content -LiteralPath $InstallCmd -Value $installCmdContent -Encoding ASCII

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $PayloadZip -Force

$sedContent = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=BizPro6 Barcode installation is complete.
TargetName=$OutputExe
FriendlyName=BizPro6 Barcode Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
VersionInfoVersion=$Version.0
VersionInfoCompany=ChipmoBarcode
VersionInfoDescription=BizPro6 Barcode Installer
VersionInfoCopyright=ChipmoBarcode
VersionInfoProductName=BizPro6 Barcode
VersionInfoProductVersion=$Version

[Strings]
FILE0=install.cmd
FILE1=install.ps1
FILE2=payload.zip
FILECOUNT=3
InstallPrompt=
DisplayLicense=
FinishMessage=BizPro6 Barcode installation is complete.
TargetName=$OutputExe
FriendlyName=BizPro6 Barcode Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0DESC=Install command
FILE1DESC=Install script
FILE2DESC=Application payload

[SourceFiles]
SourceFiles0=$IExpressWorkDir\

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@

Set-Content -LiteralPath $SedFile -Value $sedContent -Encoding ASCII

$iExpressExe = Join-Path $env:WINDIR "System32\iexpress.exe"
if (-not (Test-Path $iExpressExe)) {
    throw "IExpress not found at $iExpressExe"
}

Write-Host "[Installer] Building self-extracting setup..." -ForegroundColor Yellow
& $iExpressExe /N $SedFile

if (-not (Test-Path $OutputExe)) {
    throw "Installer output was not created: $OutputExe"
}

$sizeMb = [math]::Round(((Get-Item $OutputExe).Length / 1MB), 2)
Write-Host ""
Write-Host "Installer ready: $OutputExe" -ForegroundColor Green
Write-Host "Installer size: $sizeMb MB" -ForegroundColor Green
