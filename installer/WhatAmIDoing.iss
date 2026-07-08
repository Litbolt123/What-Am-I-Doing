; Inno Setup script for "What Am I Doing"
; Build flow: scripts\build-installer.ps1 (publish-installer.ps1 self-contained single-file, then ISCC).
; The published EXE is self-contained (.NET 8 + Windows Desktop / WPF bundled) — no separate Microsoft runtime installer in this wizard.

#define AppName        "What Am I Doing"
#define AppShortName   "WhatAmIDoing"
; Version comes from MSBuild (repo root Directory.Build.props). Build scripts must pass:
;   ISCC /DAppVersion=x.y.z installer\WhatAmIDoing.iss
; scripts\build-installer.ps1 reads Version via dotnet msbuild -getProperty:Version.

#ifndef AppVersion
; Fallback when compiling the .iss by hand without /DAppVersion — keep in sync with Directory.Build.props.
#define AppVersion "1.2.3"
#endif
#define AppPublisher   "What Am I Doing"
#define AppExe         "WhatAmIDoing.exe"
#define PublishDir     "..\src\WhatAmIDoing\bin\Publish\win-x64"

[Setup]
AppId={{7C4F1AB6-2D0E-4E3F-BE6F-9F0C1B8C2A1A}
; If you change AppId, update APP_UNINSTALL_SUBKEY in [Code] so existing-version detection still matches.
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppShortName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Matches SupportedOSPlatformVersion in WhatAmIDoing.csproj (Windows 10 1809+).
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename={#AppShortName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\WhatAmIDoing\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
InfoBeforeFile=legal\notice-before-install.txt
LicenseFile=legal\terms-license.txt
InfoAfterFile=legal\notice-after-install.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "express"; Description: "Express install (recommended)"
Name: "advanced"; Description: "Advanced — optional idle and screen capture defaults"

[Components]
Name: "main"; Description: "What Am I Doing"; Types: express advanced; Flags: fixed
Name: "strictidle"; Description: "Stricter idle defaults (45 s idle + 15 s Thinking grace — change anytime in Settings)"; Types: advanced
Name: "screenshots"; Description: "Turn on encrypted periodic screen captures (change anytime in Settings)"; Types: advanced

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "autostart";   Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Auto-start:"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "WhatAmIDoing"; \
    ValueData: """{app}\{#AppExe}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  { Must match [Setup] AppId (single braces) + Inno's _is1 suffix. }
  APP_UNINSTALL_SUBKEY = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C4F1AB6-2D0E-4E3F-BE6F-9F0C1B8C2A1A}_is1';
  { Bump when installer\legal\terms-license.txt changes in a way users must re-accept. }
  TERMS_VERSION = '1';
  REG_APP_KEY = 'Software\{#AppShortName}';

function DefaultInstallExePath: String;
begin
  Result := ExpandConstant('{localappdata}') + '\Programs\{#AppShortName}\{#AppExe}';
end;

function FormatVerFromDwords(VersionMS, VersionLS: Cardinal): String;
begin
  Result := IntToStr(VersionMS shr 16) + '.' +
    IntToStr(VersionMS and $FFFF) + '.' +
    IntToStr(VersionLS shr 16) + '.' +
    IntToStr(VersionLS and $FFFF);
end;

function TryGetExeFileVersion(const Path: String; var Ver: String): Boolean;
var
  VersionMS, VersionLS: Cardinal;
begin
  Result := False;
  Ver := '';
  if not FileExists(Path) then
    Exit;
  if not GetVersionNumbers(Path, VersionMS, VersionLS) then
    Exit;
  Ver := FormatVerFromDwords(VersionMS, VersionLS);
  Result := True;
end;

function RegReadInstallLocation(Root: Integer): String;
begin
  Result := '';
  RegQueryStringValue(Root, APP_UNINSTALL_SUBKEY, 'InstallLocation', Result);
  Result := Trim(Result);
end;

function RegReadDisplayVersion(Root: Integer): String;
begin
  Result := '';
  RegQueryStringValue(Root, APP_UNINSTALL_SUBKEY, 'DisplayVersion', Result);
  Result := Trim(Result);
end;

function GetExistingInstallLocation: String;
begin
  Result := RegReadInstallLocation(HKCU);
  if Result <> '' then
    Exit;
  Result := RegReadInstallLocation(HKLM64);
  if Result <> '' then
    Exit;
  Result := RegReadInstallLocation(HKLM);
end;

function GetExistingDisplayVersion: String;
begin
  Result := RegReadDisplayVersion(HKCU);
  if Result <> '' then
    Exit;
  Result := RegReadDisplayVersion(HKLM64);
  if Result <> '' then
    Exit;
  Result := RegReadDisplayVersion(HKLM);
end;

function GetDetectedExistingVersion: String;
var
  FromExe: String;
begin
  Result := GetExistingDisplayVersion;
  if Result <> '' then
    Exit;

  if TryGetExeFileVersion(DefaultInstallExePath, FromExe) then
    Result := FromExe;
end;

function IsExistingInstallLikely: Boolean;
begin
  Result := (GetExistingDisplayVersion <> '') or
    FileExists(DefaultInstallExePath) or
    (GetExistingInstallLocation <> '');
end;

function GetAcceptedTermsVersion: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU, REG_APP_KEY, 'TermsAcceptedVersion', Result) then
    Result := Trim(Result);
end;

procedure SetAcceptedTermsVersion(const Ver: String);
begin
  { RegWriteStringValue creates REG_APP_KEY and value if missing (no RegCreateKey in IS 6.7). }
  RegWriteStringValue(HKCU, REG_APP_KEY, 'TermsAcceptedVersion', Ver);
end;

function ShouldSkipLegalPagesOnUpdate: Boolean;
begin
  { Existing install + same terms version => skip notice + license (user already accepted). }
  Result := IsExistingInstallLikely and (CompareText(GetAcceptedTermsVersion, TERMS_VERSION) = 0);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if not ShouldSkipLegalPagesOnUpdate then
    Exit;
  if (PageID = wpLicense) or (PageID = wpInfoBefore) then
    Result := True;
end;

procedure ApplyWizardCaption;
var
  Ev: String;
begin
  Ev := GetDetectedExistingVersion;
  if Ev <> '' then
    WizardForm.Caption := '{#AppName} Setup — update from ' + Ev + ' to {#AppVersion}'
  else if IsExistingInstallLikely then
    WizardForm.Caption := '{#AppName} Setup — reinstall / update to {#AppVersion}'
  else
    WizardForm.Caption := '{#AppName} Setup — {#AppVersion}';
end;

procedure InitializeWizard;
begin
  ApplyWizardCaption;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ApplyWizardCaption;
end;

function ExistingInstallReadyMemo(Space, NewLine: String): String;
var
  Ver, Loc: String;
begin
  Result := '';
  Ver := GetDetectedExistingVersion;
  Loc := GetExistingInstallLocation;

  if Ver = '' then
  begin
    if not IsExistingInstallLikely then
      Exit;
    Ver := '(installed — version not read; try closing the app and reinstalling)';
  end;

  Result := NewLine + NewLine +
    'Existing installation' + NewLine +
    Space + 'Current version: ' + Ver + NewLine;

  if Loc <> '' then
    Result := Result + Space + 'Install location: ' + Loc + NewLine
  else
    Result := Result + Space + 'Install location: (default under your user profile)' + NewLine;

  Result := Result + Space + 'This wizard will install: {#AppVersion}' + NewLine;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
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
  if MemoTasksInfo <> '' then
    Result := Result + MemoTasksInfo + NewLine + NewLine;
  Result := Trim(Result);
  Result := Result + ExistingInstallReadyMemo(Space, NewLine);
end;

procedure WriteInstallBootstrapJson;
var
  Dir, Path, Json: String;
begin
  Dir := ExpandConstant('{localappdata}') + '\WhatAmIDoing';
  Path := Dir + '\install-bootstrap.json';
  Json := '';

  if WizardIsComponentSelected('strictidle') then
  begin
    Json := Json + '"idleThresholdMs":45000,"thinkingExtraMs":15000';
  end;

  if WizardIsComponentSelected('screenshots') then
  begin
    if Json <> '' then
      Json := Json + ',';
    Json := Json + '"screensEnabled":true';
  end;

  if Json = '' then
  begin
    if FileExists(Path) then
      DeleteFile(Path);
    Exit;
  end;

  Json := '{' + Json + '}';
  if not DirExists(Dir) then
    CreateDir(Dir);
  SaveStringToFile(Path, Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SetAcceptedTermsVersion(TERMS_VERSION);
    WriteInstallBootstrapJson;
  end;
end;
