; Inno Setup script for SpotiTube Kiosk.
; Build with installer\build.ps1, or manually:
;   1. dotnet publish src\SpotiTube.Kiosk\SpotiTube.Kiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
;   2. ISCC installer\SpotiTube.Kiosk.iss
; Requires Inno Setup 6: https://jrsoftware.org/isdl.php

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "SpotiTube Kiosk"
#define MyAppExeName "SpotiTube.Kiosk.exe"
#define MyAppPublisher "SpotiTube Kiosk"
#define MyAppURL "https://github.com/FedePedest/Youtube-and-spotify-api-touch-screen-integration"

[Setup]
AppId={{319BE8E3-42D1-4363-840E-BF7FED72D6D9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\SpotiTube Kiosk
DefaultGroupName=SpotiTube Kiosk
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=SpotiTube.Kiosk.Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

; Expects a self-contained single-file publish output in ..\publish (see build.ps1).
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now (this also turns on auto-start at login)"; Flags: postinstall skipifsilent nowait

; The app registers its own Startup-folder shortcut the first time it runs (see
; Startup/AutostartInstaller.cs), so the installer doesn't need to touch autostart on
; install - it just needs to clean that shortcut up again on uninstall.
[UninstallDelete]
Type: files; Name: "{userstartup}\SpotiTube.Kiosk.lnk"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"
