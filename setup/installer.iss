; BizPro6 Barcode Inno Setup Script

#define MyAppName "BizPro6 Barcode"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "ChipmoBarcode"
#define MyAppExeName "BizPro6Barcode.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

; Install to per-user AppData (no admin/UAC required)
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output
OutputDir=..\dist\installer
OutputBaseFilename=BizPro6_Barcode_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes

; No admin required - per-user install
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=

; Windows version requirements
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Appearance
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";                         Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}";   Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";                   Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"
