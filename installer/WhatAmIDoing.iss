; Inno Setup script for "What Am I Doing"
; Build a single-file self-contained EXE first, then run this script in
; Inno Setup Compiler (https://jrsoftware.org/isinfo.php) to produce a
; per-user installer that requires no admin rights.

#define AppName        "What Am I Doing"
#define AppShortName   "WhatAmIDoing"
#define AppVersion     "1.0.0"
#define AppPublisher   "What Am I Doing"
#define AppExe         "WhatAmIDoing.exe"
; Path to the published single-file binary, relative to this .iss file.
; Run scripts\publish.ps1 first to populate this directory.
#define PublishDir     "..\src\WhatAmIDoing\bin\Publish\win-x64"

[Setup]
AppId={{7C4F1AB6-2D0E-4E3F-BE6F-9F0C1B8C2A1A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppShortName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename={#AppShortName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\WhatAmIDoing\app.ico
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "autostart";   Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Auto-start:"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}";        Filename: "{app}\{#AppExe}"
Name: "{userdesktop}\{#AppName}";         Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "WhatAmIDoing"; \
    ValueData: """{app}\{#AppExe}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
