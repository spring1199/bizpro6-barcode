; BarTenderClone Inno Setup Script
; This script creates a professional Windows installer
;
; INSTRUCTIONS:
; 1. Download and install Inno Setup from https://jrsoftware.org/isdl.php
; 2. First run the build script: scripts\build-and-pack.ps1
; 3. Open this file in Inno Setup Compiler
; 4. Click Build -> Compile to create the installer

#define MyAppName "BizPro6 Barcode"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "ChipmoBarcode"
#define MyAppExeName "BizPro6Barcode.exe"

[Setup]
; Application information
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\dist\installer
OutputBaseFilename=BizPro6_Barcode_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes

; Windows version requirements
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Appearance
WizardStyle=modern
SetupIconFile=

; Privileges
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main executable and all dependencies from publish folder
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
