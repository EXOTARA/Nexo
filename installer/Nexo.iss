#ifndef MyAppVersion
  #define MyAppVersion "0.9.0-beta"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define MyAppName "Nexo"
#define MyAppPublisher "Nexo"
#define MyAppExeName "Nexo.exe"
#define MyAppId "{{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Nexo
DefaultGroupName=Nexo
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Nexo-{#MyAppVersion}-Setup
SetupIconFile=..\src\Nexo.App\Assets\Nexo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
AllowNoIcons=yes
MinVersion=10.0.19041
VersionInfoVersion=0.9.0.0
VersionInfoCompany=Nexo
VersionInfoDescription=Instalador de Nexo
VersionInfoProductName=Nexo
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nexo"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Nexo"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Nexo"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectory: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, RunKey, 'Nexo');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataDirectory := ExpandConstant('{localappdata}\Nexo');
    if DirExists(DataDirectory) and
       (MsgBox(
          '¿También quieres eliminar las tareas, rutinas, preferencias e historial local de Nexo?',
          mbConfirmation,
          MB_YESNO) = IDYES) then
    begin
      DelTree(DataDirectory, True, True, True);
    end;
  end;
end;
