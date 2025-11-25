; Script d'installation Inno Setup pour Powershell 7 ISE

#define MyAppName "Powershell 7 ISE"
#define MyAppVersion "1.0.7"
#define MyAppPublisher "Powershell 7 ISE"
#define MyAppExeName "Powershell7ISE.exe"
#define MyAppId "{{A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D}"

[Setup]
; Informations de base
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Powershell7ISE
DefaultGroupName=Powershell 7 ISE
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=Powershell7ISE-Setup
SetupIconFile=Resources\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64 x86 arm64

; Langues
[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; Tâches
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsWin64

; Fichiers à installer
[Files]
; Application et dépendances depuis le dossier Release
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Exclure les fichiers .pdb en production (optionnel, décommentez si nécessaire)
; Excluded: "bin\Release\net8.0-windows\*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Raccourci bureau avec nom fixe pour éviter les doublons
Name: "{autodesktop}\Powershell 7 ISE"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Fonction pour supprimer l'ancien raccourci "PsConsoleHost" s'il existe
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Supprimer l'ancien raccourci "PsConsoleHost" du bureau
    if FileExists(ExpandConstant('{autodesktop}\PsConsoleHost.lnk')) then
    begin
      DeleteFile(ExpandConstant('{autodesktop}\PsConsoleHost.lnk'));
    end;
  end;
end;

// Fonction appelée avant l'installation
function InitializeSetup(): Boolean;
begin
  // Aucune vérification de prérequis - l'installation continue directement
  Result := True;
end;

// Fonction appelée avant la désinstallation
function InitializeUninstall(): Boolean;
begin
  Result := True;
end;

