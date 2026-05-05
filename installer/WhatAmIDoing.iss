; Inno Setup script for "What Am I Doing"
; Build flow: scripts\build-installer.ps1 (fetches .NET Desktop Runtime bundle, framework-dependent publish, ISCC).
; The main EXE is framework-dependent single-file; this wizard can install the shared .NET 8 Desktop Runtime when missing.

#define AppName        "What Am I Doing"
#define AppShortName   "WhatAmIDoing"
; Version comes from MSBuild (repo root Directory.Build.props). Build scripts must pass:
;   ISCC /DAppVersion=x.y.z installer\WhatAmIDoing.iss
; scripts\build-installer.ps1 reads Version via dotnet msbuild -getProperty:Version.

#ifndef AppVersion
; Fallback when compiling the .iss by hand without /DAppVersion — keep in sync with Directory.Build.props.
#define AppVersion "1.0.2"
#endif
#define AppPublisher   "What Am I Doing"
#define AppExe         "WhatAmIDoing.exe"
#define PublishDir     "..\src\WhatAmIDoing\bin\Publish\win-x64"
#define BundledRuntime "DesktopRuntime-8-x64.exe"
; Bundled runtime must exist at installer\prereq\ before compile. CI: fetch + "Verify Desktop Runtime bundle" step.
; (Removed ISPP file-existence guard: #ifexist does not evaluate expressions; #if FileExists(SourcePath+…) was still false on some runners.)

[Setup]
AppId={{7C4F1AB6-2D0E-4E3F-BE6F-9F0C1B8C2A1A}
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
Source: "prereq\{#BundledRuntime}"; DestDir: "{tmp}\waiddn"; DestName: "{#BundledRuntime}"; Flags: deleteafterinstall

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "WhatAmIDoing"; \
    ValueData: """{app}\{#AppExe}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Interactive: Microsoft's own wizard + UAC (no /quiet). Silent /VERYSILENT setups keep /quiet for CI.
Filename: "{tmp}\waiddn\{#BundledRuntime}"; \
    Parameters: "/install /norestart"; \
    StatusMsg: "Installing Microsoft .NET 8 Desktop Runtime..."; \
    Check: RunBundledDotNetInteractive; \
    Flags: waituntilterminated
Filename: "{tmp}\waiddn\{#BundledRuntime}"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "Installing Microsoft .NET 8 Desktop Runtime..."; \
    Check: RunBundledDotNetSilent; \
    Flags: waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  UserAgreedDotNetInstall: Boolean;

function IsWinDesktopMajor8Name(const Name: String): Boolean;
begin
  { e.g. 8.0.11, 8.0.12 — must be .NET 8.x, not 9.x }
  Result := Pos('8.', Name) = 1;
end;

function DotNetWinDesktop8RegistrySubkeys(Root: Integer; const SubKey: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if not RegGetSubkeyNames(Root, SubKey, Names) then
    Exit;
  for I := 0 to GetArrayLength(Names) - 1 do
    if IsWinDesktopMajor8Name(Names[I]) then
    begin
      Result := True;
      Exit;
    end;
end;

function DotNetWinDesktop8SharedOnDisk(const ProgramsParent: String): Boolean;
var
  Base: String;
  FindRec: TFindRec;
begin
  { Matches how the apphost resolves Microsoft.WindowsDesktop.App — more reliable than registry alone. }
  Result := False;
  Base := ProgramsParent + '\dotnet\shared\Microsoft.WindowsDesktop.App';
  if not DirExists(Base) then
    Exit;
  if not FindFirst(Base + '\*', FindRec) then
    Exit;
  try
    repeat
      if ((FindRec.Attributes and faDirectory) <> 0) and
         IsWinDesktopMajor8Name(FindRec.Name) then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function DotNetWindowsDesktopApp8Present: Boolean;
const
  RegSub = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
begin
  Result :=
    DotNetWinDesktop8SharedOnDisk(ExpandConstant('{commonpf64}')) or
    DotNetWinDesktop8SharedOnDisk(ExpandConstant('{localappdata}') + '\Microsoft') or
    DotNetWinDesktop8RegistrySubkeys(HKLM64, RegSub) or
    DotNetWinDesktop8RegistrySubkeys(HKCU64, RegSub);
end;

function NeedsBundledDotNetInstall: Boolean;
begin
  Result := (not DotNetWindowsDesktopApp8Present) and UserAgreedDotNetInstall;
end;

function RunBundledDotNetInteractive: Boolean;
begin
  Result := NeedsBundledDotNetInstall and (not WizardSilent());
end;

function RunBundledDotNetSilent: Boolean;
begin
  Result := NeedsBundledDotNetInstall and WizardSilent();
end;

procedure InitializeWizard;
begin
  { Interactive: ask on Ready. Silent /VERYSILENT: no prompt; use quiet bundled install when needed. }
  UserAgreedDotNetInstall := WizardSilent();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then
    Exit;

  if DotNetWindowsDesktopApp8Present then
    Exit;

  if WizardSilent() then
  begin
    UserAgreedDotNetInstall := True;
    Exit;
  end;

  case MsgBox(
    'This computer does not have the Microsoft .NET 8 Windows Desktop Runtime that {#AppName} needs. WPF apps use the Desktop runtime — it is not the same as the smaller ".NET Runtime" only or ASP.NET installers.'#13#13 +
    'Install it now? You will see Microsoft''s installer and may need to approve a Windows security (UAC) prompt — the same as installing many other apps.'#13#13 +
    'If you choose No, setup will still copy {#AppName}, but it will not start until the Desktop Runtime is installed (choose ".NET Desktop Runtime 8", x64, at https://aka.ms/dotnet/download ).',
    mbConfirmation, MB_YESNO) of
    IDYES:
      UserAgreedDotNetInstall := True;
    IDNO:
      begin
        UserAgreedDotNetInstall := False;
        MsgBox(
          'Understood. You can install the .NET 8 Desktop Runtime later, then start {#AppName} from the Start menu.'#13#13 +
          'Click Next to finish copying the app, or Cancel to exit setup.',
          mbInformation, MB_OK);
      end;
  end;
end;

procedure WriteInstallBootstrapJson;
var
  Dir, Path, Json: String;
begin
  Dir := ExpandConstant('{localappdata}') + '\WhatAmIDoing';
  Path := Dir + '\install-bootstrap.json';
  Json := '';

  if IsComponentSelected('strictidle') then
  begin
    Json := Json + '"idleThresholdMs":45000,"thinkingExtraMs":15000';
  end;

  if IsComponentSelected('screenshots') then
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
    WriteInstallBootstrapJson;
end;
