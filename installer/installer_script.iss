; AnimeFlow Installer Script for Inno Setup
; Requires Inno Setup 6.0 or later
; Download from: https://jrsoftware.org/isdl.php

#define MyAppName "AnimeFlow"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AnimeFlow Team"
#define MyAppURL "https://github.com/yourusername/animeflow"
#define MyAppExeName "AnimeFlow.exe"

[Setup]
; Basic information
AppId={{A3B2D77B-64B3-497F-9557-DCA2627C06F2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\Output
OutputBaseFilename=AnimeFlow-Setup-{#MyAppVersion}
SetupIconFile=..\AnimeFlow\Resources\animeflow.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

; Minimum Windows version
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "fileassoc"; Description: "Associate video files with {#MyAppName}"; GroupDescription: "File Associations:"; Flags: unchecked

[Files]
; Main application
Source: "..\AnimeFlow\bin\Release\net8.0-windows\publish\AnimeFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AnimeFlow\bin\Release\net8.0-windows\publish\AnimeFlow.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AnimeFlow\bin\Release\net8.0-windows\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; mpv
Source: "..\Dependencies\mpv\*"; DestDir: "{app}\mpv"; Flags: ignoreversion recursesubdirs createallsubdirs

; VapourSynth
Source: "..\Dependencies\vapoursynth\*"; DestDir: "{app}\vapoursynth"; Flags: ignoreversion recursesubdirs createallsubdirs

; Scripts
Source: "..\Dependencies\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs

; Models (if pre-downloaded)
Source: "..\Dependencies\models\*"; DestDir: "{app}\models"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: ModelsExist

; Tools
Source: "..\Dependencies\tools\yt-dlp.exe"; DestDir: "{app}\tools"; Flags: ignoreversion

; Documentation
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Registry]
; File associations
Root: HKA; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: string; ValueName: "AnimeFlow.mkv"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: string; ValueName: "AnimeFlow.mp4"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.avi\OpenWithProgids"; ValueType: string; ValueName: "AnimeFlow.avi"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.webm\OpenWithProgids"; ValueType: string; ValueName: "AnimeFlow.webm"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

Root: HKA; Subkey: "Software\Classes\AnimeFlow.mkv"; ValueType: string; ValueName: ""; ValueData: "MKV Video File"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\AnimeFlow.mkv\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\AnimeFlow.mkv\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;
  ModelsDownloaded: Boolean;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded %s', [FileName]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
  ModelsDownloaded := False;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ModelsDir: String;
begin
  if CurPageID = wpReady then begin
    DownloadPage.Clear;
    
    // Check if models already exist
    ModelsDir := ExpandConstant('{app}\models\rife-v4.6');
    if not DirExists(ModelsDir) then begin
      // Add model download
      DownloadPage.Add('https://github.com/HomeOfVapourSynthEvolution/VapourSynth-RIFE-ncnn-Vulkan/releases/download/r5/rife-v4.6.7z', 'rife-v4.6.7z', '');
      DownloadPage.Show;
      try
        try
          DownloadPage.Download;
          ModelsDownloaded := True;
          Result := True;
        except
          if DownloadPage.AbortedByUser then
            Log('Download aborted by user.')
          else
            SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
          Result := False;
        end;
      finally
        DownloadPage.Hide;
      end;
    end else begin
      Result := True;
    end;
  end else
    Result := True;
end;

function ModelsExist: Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\models'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  ModelsDir: String;
begin
  if CurStep = ssPostInstall then begin
    // Extract models if downloaded
    if ModelsDownloaded then begin
      ModelsDir := ExpandConstant('{app}\models');
      CreateDir(ModelsDir);
      
      // Extract using 7z (if available) or built-in extraction
      // This is a simplified version - actual implementation would use 7z
      Log('Models downloaded, extraction would happen here');
    end;
    
    // Check for .NET 8 Runtime
    if not RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost\8.0') then begin
      if MsgBox('.NET 8 Runtime is required but not installed. Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then begin
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOW, ewNoWait, ResultCode);
      end;
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{userappdata}\AnimeFlow"
