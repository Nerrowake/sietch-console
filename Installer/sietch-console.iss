; Sietch Console — Inno Setup installer script
; Compile with: iscc /DAppVersion=0.1.0-alpha.1 sietch-console.iss
; Or let the release.yml GitHub Actions workflow compile it automatically.
;
; Prerequisites: Inno Setup 6.x  https://jrsoftware.org/isinfo.php

#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif

#define AppName      "Sietch Console"
#define AppPublisher "Michael Stoffer"
#define AppURL       "https://github.com/michaelstoffer/sietch-console"
#define AppExeName   "Sietch Console.exe"

[Setup]
AppId={{E7A2F3B1-4C8D-4E9F-A012-3B456C789D01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=Output
OutputBaseFilename=SietchConsole-{#AppVersion}-Setup
SetupIconFile=Installer\AppIcon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
; Require Windows 10 20H1 (build 19041) — minimum for .NET 8 + modern WPF
MinVersion=10.0.19041
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

; Source files are in ..\publish\ (output of dotnet publish)
SourceDir=..

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startupicon"; Description: "Start {#AppName} with Windows"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application (single-file self-contained publish)
Source: "publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; OxyPlot assemblies are excluded from the single-file bundle (ExcludeFromSingleFile in .csproj)
; so the WPF BAML type-resolver can find the XmlnsDefinitionAttribute at runtime.
; skipifsourcedoesntexist guards against Debug builds where these DLLs are not published.
Source: "publish\OxyPlot.dll";     DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "publish\OxyPlot.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}";                           Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}";     Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";                   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Startup registry entry (optional task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove any SQLite database and app data left behind only if user confirms via
; the standard uninstaller — we don't auto-delete user data here.
; User data lives in %LOCALAPPDATA%\SietchConsole and is intentionally preserved.
