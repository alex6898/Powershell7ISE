; Script d'installation Inno Setup pour Powershell 7 ISE
; Ce script installe automatiquement .NET 8.0 Desktop Runtime si nécessaire

#define MyAppName "Powershell 7 ISE"
#define MyAppVersion "1.0.2"
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
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Raccourci bureau avec nom fixe pour éviter les doublons
Name: "{autodesktop}\Powershell 7 ISE"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Fonction pour vérifier si .NET 8.0 Desktop Runtime est installé
function IsDotNet80Installed(): Boolean;
var
  Release: Cardinal;
  RegKey: String;
  ResultCode: Integer;
begin
  Result := False;
  
  // Méthode 1: Vérifier via le chemin d'installation (la plus fiable)
  if DirExists(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0')) then
  begin
    Result := True;
    Exit;
  end;
  
  if DirExists(ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0')) then
  begin
    Result := True;
    Exit;
  end;
  
  // Méthode 2: Vérifier via dotnet --list-runtimes (plus fiable que --version)
  if Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      // Utiliser une commande PowerShell pour vérifier la présence de .NET 8.0 Desktop Runtime
      if Exec('powershell.exe', '-Command "$runtimes = dotnet --list-runtimes; if ($runtimes -match ''Microsoft.WindowsDesktop.App 8\.'') { exit 0 } else { exit 1 }"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        if ResultCode = 0 then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
  
  // Méthode 3: Vérifier via le registre (moins fiable, mais on essaie quand même)
  try
    if IsWin64 then
    begin
      // Vérifier dans les différentes clés possibles
      RegKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost';
      if RegQueryDWordValue(HKEY_LOCAL_MACHINE, RegKey, '8.0', Release) then
      begin
        Result := True;
        Exit;
      end;
      
      // Vérifier aussi dans WOW6432Node
      RegKey := 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost';
      if RegQueryDWordValue(HKEY_LOCAL_MACHINE, RegKey, '8.0', Release) then
      begin
        Result := True;
        Exit;
      end;
      
      // Vérifier les clés de version spécifique (essayer de lire une valeur)
      RegKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost\8.0';
      if RegQueryDWordValue(HKEY_LOCAL_MACHINE, RegKey, '', Release) then
      begin
        Result := True;
        Exit;
      end;
    end
    else
    begin
      RegKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedhost';
      if RegQueryDWordValue(HKEY_LOCAL_MACHINE, RegKey, '8.0', Release) then
      begin
        Result := True;
        Exit;
      end;
    end;
  except
    // Ignorer les erreurs de registre
  end;
  
  // Méthode 4: Vérifier via dotnet --version (dernier recours)
  if Exec('dotnet', '--version', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      // Si dotnet existe, vérifier qu'il peut lister les runtimes
      if Exec('powershell.exe', '-Command "try { $runtimes = dotnet --list-runtimes 2>&1; if ($runtimes -match ''Microsoft.WindowsDesktop.App 8\.'') { exit 0 } else { exit 1 } } catch { exit 1 }"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        if ResultCode = 0 then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
end;

// Fonction pour vérifier si WebView2 Runtime est installé
function IsWebView2Installed(): Boolean;
var
  Version: String;
  RegKey: String;
begin
  Result := False;
  
  // Vérifier la version installée de WebView2
  RegKey := 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, RegKey, 'pv', Version) then
  begin
    Result := True;
    Exit;
  end;
  
  // Vérifier dans la clé 32-bit
  RegKey := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, RegKey, 'pv', Version) then
  begin
    Result := True;
    Exit;
  end;
  
  // Vérifier via le chemin d'installation
  if DirExists(ExpandConstant('{pf}\Microsoft\EdgeWebView\Application')) then
  begin
    Result := True;
  end;
end;

// Fonction pour trouver un fichier SDK .NET dans le dossier installer
function FindDotNetSDKFile(Architecture: String): String;
var
  FileName: String;
begin
  Result := '';
  
  // Chercher les fichiers SDK possibles pour cette architecture
  // Format: dotnet-sdk-8.0.*-win-{arch}.exe
  // Essayer d'abord avec {srcexe}, puis avec {src}
  
  // Vérifier les versions SDK courantes (8.0.416, 8.0.400, etc.)
  // Essayer d'abord avec {srcexe} (plus fiable)
  FileName := ExpandConstant('{srcexe}\..\installer\dotnet-sdk-8.0.416-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  FileName := ExpandConstant('{srcexe}\..\installer\dotnet-sdk-8.0.400-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  FileName := ExpandConstant('{srcexe}\..\installer\dotnet-sdk-8.0.300-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  FileName := ExpandConstant('{srcexe}\..\installer\dotnet-sdk-8.0.200-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  FileName := ExpandConstant('{srcexe}\..\installer\dotnet-sdk-8.0.100-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  // Essayer avec {src} comme alternative
  FileName := ExpandConstant('{src}\installer\dotnet-sdk-8.0.416-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
  
  FileName := ExpandConstant('{src}\installer\dotnet-sdk-8.0.400-win-' + Architecture + '.exe');
  if FileExists(FileName) then
  begin
    Result := FileName;
    Exit;
  end;
end;

// Fonction pour télécharger un fichier via VBScript (plus fiable que PowerShell)
function DownloadFile(Url: String; DestFile: String): Boolean;
var
  ResultCode: Integer;
  VbsScript: String;
  ScriptFile: String;
  DestDir: String;
begin
  Result := False;
  ScriptFile := ExpandConstant('{tmp}\download.vbs');
  DestDir := ExtractFileDir(DestFile);
  
  // Créer le répertoire de destination s'il n'existe pas
  if not DirExists(DestDir) then
    ForceDirectories(DestDir);
  
  // Créer un script VBScript pour télécharger le fichier (plus fiable)
  VbsScript := 'On Error Resume Next' + #13#10 +
               'Set xHttp = CreateObject("Microsoft.XMLHTTP")' + #13#10 +
               'xHttp.Open "GET", "' + Url + '", False' + #13#10 +
               'xHttp.Send' + #13#10 +
               'If Err.Number = 0 And xHttp.Status = 200 Then' + #13#10 +
               '  Set oStream = CreateObject("ADODB.Stream")' + #13#10 +
               '  oStream.Open' + #13#10 +
               '  oStream.Type = 1' + #13#10 +
               '  oStream.Write xHttp.responseBody' + #13#10 +
               '  oStream.SaveToFile "' + DestFile + '", 2' + #13#10 +
               '  oStream.Close' + #13#10 +
               '  If Err.Number = 0 Then' + #13#10 +
               '    WScript.Quit 0' + #13#10 +
               '  End If' + #13#10 +
               'End If' + #13#10 +
               'WScript.Quit 1';
  
  // Écrire le script dans un fichier temporaire
  if SaveStringToFile(ScriptFile, VbsScript, False) then
  begin
    // Exécuter VBScript pour télécharger le fichier
    if Exec('cscript.exe', '//nologo "' + ScriptFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if (ResultCode = 0) and FileExists(DestFile) then
      begin
        Result := True;
      end;
    end;
  end;
end;

// Fonction pour télécharger et installer .NET 8.0 Desktop Runtime
function InstallDotNet80(): Boolean;
var
  StatusText: String;
  ResultCode: Integer;
  DownloadUrl: String;
  TempFile: String;
  Architecture: String;
begin
  Result := False;
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Téléchargement de .NET 8.0 Desktop Runtime...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  
  try
    // Déterminer l'architecture
    // Note: La détection ARM64 est complexe, on utilise x64 par défaut pour les systèmes 64-bit
    // Si seulement des fichiers ARM64 sont présents, ils seront utilisés
    if IsWin64 then
      Architecture := 'x64'
    else
      Architecture := 'x86';
    
    TempFile := ExpandConstant('{tmp}\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe');
    
    // Vérifier si le fichier existe déjà dans le dossier installer (priorité)
    // D'abord chercher Desktop Runtime, puis SDK (les SDK incluent aussi le runtime)
    // Essayer plusieurs emplacements possibles
    if FileExists(ExpandConstant('{srcexe}\..\installer\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe')) then
    begin
      TempFile := ExpandConstant('{srcexe}\..\installer\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe');
    end
    else if FileExists(ExpandConstant('{src}\installer\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe')) then
    begin
      TempFile := ExpandConstant('{src}\installer\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe');
    end
    else
    begin
      // Chercher le SDK correspondant (peut avoir différentes versions)
      TempFile := FindDotNetSDKFile(Architecture);
      if TempFile = '' then
      begin
        // Aucun fichier local trouvé, on va télécharger
        TempFile := ExpandConstant('{tmp}\dotnet-desktop-runtime-8.0.0-win-' + Architecture + '.exe');
        
        // URLs de téléchargement pour .NET 8.0 Desktop Runtime
        // Utiliser les URLs officielles Microsoft
        if Architecture = 'x64' then
          DownloadUrl := 'https://download.visualstudio.microsoft.com/download/pr/81513200-6370-4fd8-9579-6d59bd0c8d62/8b0ad1953d5e8f54e5d0e42e8c5b7e3e/dotnet-desktop-runtime-8.0.0-win-x64.exe'
        else if Architecture = 'x86' then
          DownloadUrl := 'https://download.visualstudio.microsoft.com/download/pr/81513200-6370-4fd8-9579-6d59bd0c8d62/8b0ad1953d5e8f54e5d0e42e8c5b7e3e/dotnet-desktop-runtime-8.0.0-win-x86.exe'
        else
          DownloadUrl := 'https://download.visualstudio.microsoft.com/download/pr/81513200-6370-4fd8-9579-6d59bd0c8d62/8b0ad1953d5e8f54e5d0e42e8c5b7e3e/dotnet-desktop-runtime-8.0.0-win-arm64.exe';
        
        // Télécharger le fichier
        if not DownloadFile(DownloadUrl, TempFile) then
        begin
          // Si le téléchargement échoue, proposer un téléchargement manuel
          if MsgBox('Impossible de télécharger automatiquement .NET 8.0 Desktop Runtime.' + #13#10 + #13#10 +
                    'Souhaitez-vous ouvrir la page de téléchargement dans votre navigateur ?' + #13#10 +
                    'Vous pourrez télécharger manuellement le runtime et relancer l''installation.', 
                    mbConfirmation, MB_YESNO) = IDYES then
          begin
            ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
          end;
          Result := False;
          Exit;
        end;
      end;
    end;
    
    // Ajuster le message selon le type de fichier trouvé
    if Pos('sdk', LowerCase(TempFile)) > 0 then
      WizardForm.StatusLabel.Caption := 'Installation de .NET 8.0 SDK (inclut le Runtime)...'
    else
      WizardForm.StatusLabel.Caption := 'Installation de .NET 8.0 Desktop Runtime...';
    WizardForm.ProgressGauge.Style := npbstNormal;
    
    // Installer .NET 8.0 Desktop Runtime en mode silencieux
    if Exec(TempFile, '/quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if (ResultCode = 0) or (ResultCode = 3010) then // 3010 = redémarrage requis
      begin
        Result := True;
      end
      else
      begin
        MsgBox('Erreur lors de l''installation de .NET 8.0 Desktop Runtime. Code d''erreur: ' + IntToStr(ResultCode), mbError, MB_OK);
      end;
    end
    else
    begin
      MsgBox('Impossible de lancer l''installateur de .NET 8.0 Desktop Runtime.', mbError, MB_OK);
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
end;

// Fonction pour télécharger et installer WebView2 Runtime
function InstallWebView2(): Boolean;
var
  StatusText: String;
  ResultCode: Integer;
  TempFile: String;
  DownloadUrl: String;
  Architecture: String;
begin
  Result := False;
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Téléchargement de WebView2 Runtime...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  
  try
    // Déterminer l'architecture
    if IsWin64 then
      Architecture := 'x64'
    else
      Architecture := 'x86';
    
    // URL officielle de téléchargement de WebView2 Runtime
    DownloadUrl := 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';
    
    TempFile := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');
    
    // Vérifier si le fichier existe déjà dans le dossier installer
    if FileExists(ExpandConstant('{src}\installer\MicrosoftEdgeWebview2Setup.exe')) then
    begin
      TempFile := ExpandConstant('{src}\installer\MicrosoftEdgeWebview2Setup.exe');
    end
    else if not FileExists(TempFile) then
    begin
      // Télécharger le fichier
      if not DownloadFile(DownloadUrl, TempFile) then
      begin
        // Si le téléchargement échoue, proposer un téléchargement manuel
        if MsgBox('Impossible de télécharger automatiquement WebView2 Runtime.' + #13#10 + #13#10 +
                  'Souhaitez-vous ouvrir la page de téléchargement dans votre navigateur ?' + #13#10 +
                  'Vous pourrez télécharger manuellement le runtime et relancer l''installation.', 
                  mbConfirmation, MB_YESNO) = IDYES then
        begin
          ShellExec('open', 'https://go.microsoft.com/fwlink/p/?LinkId=2124703', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
        end;
        Result := False;
        Exit;
      end;
    end;
    
    WizardForm.StatusLabel.Caption := 'Installation de WebView2 Runtime...';
    WizardForm.ProgressGauge.Style := npbstNormal;
    
    // Installer WebView2 Runtime en mode silencieux
    if Exec(TempFile, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if (ResultCode = 0) or (ResultCode = 3010) then // 3010 = redémarrage requis
      begin
        Result := True;
      end
      else
      begin
        MsgBox('Erreur lors de l''installation de WebView2 Runtime. Code d''erreur: ' + IntToStr(ResultCode), mbError, MB_OK);
      end;
    end
    else
    begin
      MsgBox('Impossible de lancer l''installateur de WebView2 Runtime.', mbError, MB_OK);
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
end;

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
var
  DotNetMissing: Boolean;
  Response: Integer;
begin
  Result := True;
  DotNetMissing := not IsDotNet80Installed();
  
  // WebView2 n'est plus requis (pas utilisé dans l'application)
  // On ne vérifie plus WebView2
  
  if DotNetMissing then
  begin
    Response := MsgBox('.NET 8.0 Desktop Runtime est manquant.' + #13#10 +
                       'L''application ne fonctionnera pas sans .NET 8.0.' + #13#10 + #13#10 +
                       'Souhaitez-vous l''installer maintenant ?' + #13#10 +
                       '(Cliquez sur Non pour continuer quand même)', mbConfirmation, MB_YESNO);
    
    if Response = IDYES then
    begin
      if not InstallDotNet80() then
      begin
        // Même si l'installation échoue, on continue
        MsgBox('L''installation de .NET 8.0 a échoué, mais l''installation de l''application va continuer.' + #13#10 +
               'Vous devrez installer .NET 8.0 manuellement pour utiliser l''application.' + #13#10 + #13#10 +
               'Téléchargement : https://dotnet.microsoft.com/download/dotnet/8.0', 
               mbInformation, MB_OK);
      end;
    end
    else
    begin
      MsgBox('Attention : L''application nécessite .NET 8.0 Desktop Runtime pour fonctionner.' + #13#10 +
             'Vous pouvez l''installer plus tard depuis :' + #13#10 +
             'https://dotnet.microsoft.com/download/dotnet/8.0', 
             mbInformation, MB_OK);
    end;
  end;
end;

// Fonction appelée avant la désinstallation
function InitializeUninstall(): Boolean;
begin
  Result := True;
end;

