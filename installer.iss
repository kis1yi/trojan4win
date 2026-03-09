; Inno Setup Script for trojan4win Installer
; Build the project first: dotnet publish -c Release -r win-x64 --self-contained
; Then compile this script with Inno Setup

#define MyAppName "trojan4win"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "trojan4win"
#define MyAppExeName "trojan4win.exe"

; NDISAPI / Windows Packet Filter bundled MSI
#define WpfMsiName "Windows.Packet.Filter.3.6.2.1.x64.msi"
#define WpfVersion "3.6.2.1"
#define WpfDisplayName "Windows Packet Filter"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer_output
OutputBaseFilename=trojan4win_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Prevent running two installers simultaneously
SetupMutex=trojan4win_setup_mutex
; Detect running application and offer to close it before upgrading
CloseApplications=yes
CloseApplicationsFilter=*.exe
AppMutex=trojan4win_app_mutex
; Per-user areas (LocalAppData, HKCU) are used intentionally:
; each user gets individual settings. This is not a mistake.
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installwpf"; Description: "Install Windows Packet Filter (NDISAPI) driver"; GroupDescription: "Components:"; Flags: checkedonce

[Files]
; Published application output
Source: "trojan4win\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Trojan binary and dependencies - place trojan.exe in Tools\trojan\ before building
Source: "trojan4win\Tools\trojan\*"; DestDir: "{app}\Tools\trojan"; Excludes: "*.gitkeep,README.md"; Flags: ignoreversion recursesubdirs createallsubdirs

; Proxifyre binary and dependencies - place proxifyre.exe in Tools\proxifyre\ before building
Source: "trojan4win\Tools\proxifyre\*"; DestDir: "{app}\Tools\proxifyre"; Excludes: "*.gitkeep,README.md"; Flags: ignoreversion recursesubdirs createallsubdirs

; NDISAPI MSI — extract to temp dir for install only (not deployed to {app})
Source: "trojan4win\Tools\ndisapi\{#WpfMsiName}"; DestDir: "{tmp}"; Excludes: "*.gitkeep,README.md"; Flags: ignoreversion deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; WPF driver installation is handled in [Code] section (CurStepChanged)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
; Remove the entire application directory (catches runtime-generated files)
Type: filesandordirs; Name: "{app}"

[Code]
// ---------------------------------------------------------------
//  Helpers: detect installed Windows Packet Filter version
// ---------------------------------------------------------------

function FindWpfVersionInRegistry(const BasePath: String): String;
var
  SubKeys: TArrayOfString;
  I: Integer;
  DisplayName, DisplayVersion: String;
begin
  Result := '';
  if RegGetSubkeyNames(HKLM, BasePath, SubKeys) then
  begin
    for I := 0 to GetArrayLength(SubKeys) - 1 do
    begin
      if RegQueryStringValue(HKLM, BasePath + '\' + SubKeys[I],
           'DisplayName', DisplayName) then
      begin
        if Pos('Windows Packet Filter', DisplayName) > 0 then
        begin
          if RegQueryStringValue(HKLM, BasePath + '\' + SubKeys[I],
               'DisplayVersion', DisplayVersion) then
            Result := DisplayVersion
          else
            Result := 'unknown';
          Exit;
        end;
      end;
    end;
  end;
end;

function GetInstalledWpfVersion: String;
begin
  Result := FindWpfVersionInRegistry(
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall');
  if Result <> '' then Exit;
  Result := FindWpfVersionInRegistry(
    'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall');
end;

// ---------------------------------------------------------------
//  Global state
// ---------------------------------------------------------------
var
  WpfSkipped: Boolean;

// ---------------------------------------------------------------
//  InitializeSetup
// ---------------------------------------------------------------

function InitializeSetup: Boolean;
begin
  Result := True;
  WpfSkipped := False;
end;

// ---------------------------------------------------------------
//  NextButtonClick — handle WPF version check after task page
// ---------------------------------------------------------------

function NextButtonClick(CurPageID: Integer): Boolean;
var
  InstalledVer: String;
  Msg: String;
begin
  Result := True;

  // After the tasks page, check for an existing WPF installation
  if CurPageID = wpSelectTasks then
  begin
    if WizardIsTaskSelected('installwpf') then
    begin
      InstalledVer := GetInstalledWpfVersion;
      if InstalledVer <> '' then
      begin
        if InstalledVer = '{#WpfVersion}' then
          Msg := '{#WpfDisplayName} version ' + InstalledVer +
                 ' is already installed (same version as bundled).' + #13#10 + #13#10 +
                 'Do you want to skip the driver installation?' + #13#10 +
                 'Click Yes to skip, or No to reinstall.'
        else
          Msg := '{#WpfDisplayName} version ' + InstalledVer +
                 ' is already installed.' + #13#10 +
                 'The bundled version is {#WpfVersion}.' + #13#10 + #13#10 +
                 'Do you want to skip the driver installation?' + #13#10 +
                 'Click Yes to keep the current version, or No to reinstall.';

        if MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES then
          WpfSkipped := True
        else
          WpfSkipped := False;
      end;
    end;
  end;
end;

// ---------------------------------------------------------------
//  UpdateReadyMemo — reflect WpfSkipped state on Ready page
// ---------------------------------------------------------------

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Tasks: String;
begin
  Tasks := MemoTasksInfo;
  if WpfSkipped then
    StringChangeEx(Tasks,
      'Install Windows Packet Filter (NDISAPI) driver',
      'Install Windows Packet Filter (NDISAPI) driver (skipped — already installed)',
      True);

  Result := '';
  if MemoUserInfoInfo <> '' then
    Result := Result + MemoUserInfoInfo + NewLine + NewLine;
  if MemoDirInfo <> '' then
    Result := Result + MemoDirInfo + NewLine + NewLine;
  if MemoTypeInfo <> '' then
    Result := Result + MemoTypeInfo + NewLine + NewLine;
  if MemoComponentsInfo <> '' then
    Result := Result + MemoComponentsInfo + NewLine + NewLine;
  if MemoGroupInfo <> '' then
    Result := Result + MemoGroupInfo + NewLine + NewLine;
  if Tasks <> '' then
    Result := Result + Tasks + NewLine + NewLine;
end;

// ---------------------------------------------------------------
//  Helpers: find WPF product GUID in registry for uninstall
// ---------------------------------------------------------------

function FindWpfProductGuid(const BasePath: String): String;
var
  SubKeys: TArrayOfString;
  I: Integer;
  DisplayName: String;
begin
  Result := '';
  if RegGetSubkeyNames(HKLM, BasePath, SubKeys) then
  begin
    for I := 0 to GetArrayLength(SubKeys) - 1 do
    begin
      if RegQueryStringValue(HKLM, BasePath + '\' + SubKeys[I],
           'DisplayName', DisplayName) then
      begin
        if Pos('Windows Packet Filter', DisplayName) > 0 then
        begin
          Result := SubKeys[I];
          Exit;
        end;
      end;
    end;
  end;
end;

procedure UninstallWpfDriver;
var
  ProductGuid: String;
  ResultCode: Integer;
begin
  // Find product GUID in registry and uninstall by GUID
  ProductGuid := FindWpfProductGuid(
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall');
  if ProductGuid = '' then
    ProductGuid := FindWpfProductGuid(
      'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall');

  if ProductGuid <> '' then
    Exec('msiexec.exe', '/x ' + ProductGuid + ' /qn /norestart',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// ---------------------------------------------------------------
//  CurUninstallStepChanged — clean up WPF driver + per-user data
// ---------------------------------------------------------------

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Uninstall WPF driver (while {app} files still exist)
    UninstallWpfDriver;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    // Remove per-user settings folder
    UserDataDir := ExpandConstant('{localappdata}\trojan4win');
    if DirExists(UserDataDir) then
      DelTree(UserDataDir, True, True, True);

    // Remove per-user autostart registry entry
    RegDeleteValue(HKCU, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'trojan4win');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  MsiPath: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Install WPF only if the task was selected and not skipped
    if WizardIsTaskSelected('installwpf') and (not WpfSkipped) then
    begin
      MsiPath := ExpandConstant('{tmp}\{#WpfMsiName}');

      if FileExists(MsiPath) then
      begin
        WizardForm.StatusLabel.Caption := 'Installing Windows Packet Filter driver...';

        if not Exec('msiexec.exe',
             '/i "' + MsiPath + '" /qn /norestart',
             '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        begin
          MsgBox('Failed to launch the Windows Packet Filter installer.' + #13#10 +
                 'You can install it manually from:' + #13#10 + MsiPath,
                 mbError, MB_OK);
        end
        else
        begin
          if (ResultCode <> 0) and (ResultCode <> 3010) then
            MsgBox('Windows Packet Filter installation returned code ' +
                   IntToStr(ResultCode) + '.' + #13#10 +
                   'The driver may not have been installed correctly.' + #13#10 +
                   'You can try installing it manually from:' + #13#10 + MsiPath,
                   mbError, MB_OK);
        end;
      end
      else
      begin
        MsgBox('Windows Packet Filter MSI was not found at:' + #13#10 + MsiPath + #13#10 +
               'The driver was not installed.',
               mbError, MB_OK);
      end;
    end;
  end;
end;
