; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "W3Devices"
#define MyAppVersion "1.2.2"
#define MyAppExeName MyAppName + ".exe"
#define MyAppPublisher "NASS e.K."
#define MyAppURL "https://www.nass-ek.de"
#define ProgramFiles GetEnv("ProgramFiles")

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{A9A81C16-DD06-418C-9130-13191CB62EEB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=D:\Dokumente\gpl_de.txt
; Uncomment the following line to run in non administrative install mode (install for current user only.)
PrivilegesRequired=lowest
OutputDir=bin/Release
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
SetupIconFile=w3coach.ico
UninstallDisplayIcon={app}\{#MyAppExeName},0
;Begin adjustments for showing the logo
DisableWelcomePage=False
WizardImageFile=D:\Bilder\wz_nass-ek.bmp
WizardSmallImageFile=D:\Bilder\wz_leer_small.bmp
;End adjustments for showing the logo
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SignTool=Certum

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}";
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon;

[Files]
Source: "bin\Release\{#MyAppExeName}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: confirmoverwrite
;Source: "..\bin\Release\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"
;Source: "..\bin\Release\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"
;Source: "..\bin\Release\EntityFramework.dll"; DestDir: "{app}"
;Source: "..\bin\Release\EntityFramework.SqlServer.dll"; DestDir: "{app}"
;Source: "..\bin\Release\Newtonsoft.Json.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Buffers.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Data.SQLite.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Data.SQLite.EF6.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Data.SQLite.Linq.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Diagnostics.DiagnosticSource.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Memory.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Numerics.Vectors.dll"; DestDir: "{app}"
;Source: "..\bin\Release\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"

[Code]

function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;


{ ///////////////////////////////////////////////////////////////////// }
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;


{ ///////////////////////////////////////////////////////////////////// }
function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
{ Return Values: }
{ 1 - uninstall string is empty }
{ 2 - error executing the UnInstallString }
{ 3 - successfully executed the UnInstallString }

  { default return value }
  Result := 0;

  { get the uninstall string of the old app }
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;

{ ///////////////////////////////////////////////////////////////////// }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep=ssInstall) then
  begin
    if (IsUpgrade()) then
    begin
      UnInstallOldVersion();
    end;
  end;
end;
