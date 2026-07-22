#ifndef MyAppVersion
  #define MyAppVersion "0.9.3-beta"
#endif

#ifndef MyNumericVersion
  #define MyNumericVersion "0.9.2.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define MyAppName "Kohana"
#define MyAppPublisher "EXOTARA"
#define MyAppExeName "Kohana.exe"
#define MyAppId "{{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/EXOTARA/Nexo
AppSupportURL=https://github.com/EXOTARA/Nexo/issues
DefaultDirName={localappdata}\Programs\Kohana
DefaultGroupName=Kohana
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Kohana-{#MyAppVersion}-Setup
SetupIconFile=..\src\Nexo.App\Assets\Kohana.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
AllowNoIcons=yes
MinVersion=10.0.19041
VersionInfoVersion={#MyNumericVersion}
VersionInfoCompany=EXOTARA
VersionInfoDescription=Instalador de Kohana
VersionInfoProductName=Kohana
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "startup"; Description: "Iniciar Kohana con Windows"; GroupDescription: "Integración con Windows:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Kohana"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Kohana"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Kohana"; ValueData: """{app}\{#MyAppExeName}"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Kohana"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectory: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, RunKey, 'Kohana');
    RegDeleteValue(HKCU, RunKey, 'Nexo');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataDirectory := ExpandConstant('{localappdata}\Kohana');
    if DirExists(DataDirectory) and
       (MsgBox(
          '¿También quieres eliminar las tareas, rutinas, preferencias e historial local de Kohana?',
          mbConfirmation,
          MB_YESNO) = IDYES) then
    begin
      DelTree(DataDirectory, True, True, True);
    end;
  end;
end;
