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
#define AppVersion "1.1.0"
#endif
#define AppPublisher   "What Am I Doing"
#define AppExe         "WhatAmIDoing.exe"
#define PublishDir     "..\src\WhatAmIDoing\bin\Publish\win-x64"

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
    WriteInstallBootstrapJson;
end;
