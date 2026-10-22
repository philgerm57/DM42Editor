; ===========================================================================
;  Installeur DM42editor + Py42 (Python vers RPN HP-42S / DM42)
;
;  À compiler avec build.bat (racine G:\dm42_editor), qui publie d'abord :
;    _build\dm42editor\   DM42editor.exe
;    _build\Py42\         Py42.exe
;  Le setup est créé dans _setup\.
;
;  Dossier installé :
;    {app}\              DM42editor (éditeur RPN, .raw, simulateur, menu Python)
;    {app}\Py42\         Py42 (éditeur Python → RPN autonome)
;    {app}\doc\          manuels PDF + notice Py42
;    {app}\exemples\     programmes .raw et exemples Python
; ===========================================================================

#define MyAppName "DM42editor"
#ifndef MyAppVersion
  #define MyAppVersion "1.9.0"
#endif
#define MyAppPublisher "germain PHILIPPE"
#define MyAppExeName "DM42editor.exe"
#define PyExeName "Py42.exe"
#define DoubleAmp(Value) StringChange(Value, "&", "&&")

; chemins relatifs à ce fichier (dm42editor\) : rien n'est lié à la lettre G:
#define Root AddBackslash(SourcePath) + "..\"
#define Build Root + "_build\"

[Setup]
; AppId inchangé : l'installation met à jour une version précédente
AppId={{1A9C10A4-45BA-4F30-80F4-2A9C81BDD2D0}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile={#Root}dm42editor\Projet1.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; installation pour tous (admin) ou pour l'utilisateur seul, au choix
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#Root}_setup
OutputBaseFilename=DM42editor-setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
french.CompMain=DM42editor (éditeur RPN, conversion .raw, simulateur)
english.CompMain=DM42editor (RPN editor, .raw conversion, simulator)
french.CompPy=Py42 - éditeur Python vers RPN (application séparée)
english.CompPy=Py42 - Python to RPN editor (separate application)
french.CompDoc=Documentation (manuels PDF)
english.CompDoc=Documentation (PDF manuals)
french.CompEx=Exemples (programmes .raw et Python)
english.CompEx=Examples (.raw and Python programs)
french.TypeFull=Installation complète
english.TypeFull=Full installation
french.TypeCompact=Installation minimale (DM42editor seul)
english.TypeCompact=Minimal installation (DM42editor only)
french.TypeCustom=Installation personnalisée
english.TypeCustom=Custom installation
french.PyIcon=Py42 - Python vers RPN
english.PyIcon=Py42 - Python to RPN
french.DocIcon=Documentation
english.DocIcon=Documentation
french.ExIcon=Exemples
english.ExIcon=Examples
french.PyDesktop=Créer une icône Py42 sur le bureau
english.PyDesktop=Create a Py42 desktop icon
french.NoDotNet=Le runtime « .NET 10 Desktop Runtime (x64) » n'a pas été trouvé.%nDM42editor et Py42 en ont besoin pour démarrer.%n%nOuvrir la page de téléchargement de Microsoft ?%n(L'installation continue ensuite normalement.)
english.NoDotNet=".NET 10 Desktop Runtime (x64)" was not found.%nDM42editor and Py42 need it to start.%n%nOpen the Microsoft download page?%n(Setup then continues normally.)

[Types]
Name: "full"; Description: "{cm:TypeFull}"
Name: "compact"; Description: "{cm:TypeCompact}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "main"; Description: "{cm:CompMain}"; Types: full compact custom; Flags: fixed
Name: "py42"; Description: "{cm:CompPy}"; Types: full custom
Name: "doc"; Description: "{cm:CompDoc}"; Types: full compact custom
Name: "exemples"; Description: "{cm:CompEx}"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "desktopicon_py"; Description: "{cm:PyDesktop}"; GroupDescription: "{cm:AdditionalIcons}"; Components: py42; Flags: unchecked

[Files]
; --- DM42editor (dossier principal). Les PDF vont dans doc\ ; liste_fonctions.csv reste à côté de l'exe.
Source: "{#Build}dm42editor\*"; DestDir: "{app}"; Excludes: "*.pdf,*.pdb"; Components: main; Flags: ignoreversion recursesubdirs createallsubdirs

; --- Py42 dans un sous-dossier séparé
Source: "{#Build}Py42\*"; DestDir: "{app}\Py42"; Excludes: "*.pdb"; Components: py42; Flags: ignoreversion recursesubdirs createallsubdirs

; --- documentation
Source: "{#Root}dm42editor\data\*.pdf"; DestDir: "{app}\doc"; Components: doc; Flags: ignoreversion
Source: "{#Root}Py42\LISEZMOI.md"; DestDir: "{app}\doc"; DestName: "Py42 - LISEZMOI.txt"; Components: doc; Flags: ignoreversion

; --- exemples (.raw à la racine de exemples, Python dans exemples\python)
Source: "{#Root}exemples\*"; DestDir: "{app}\exemples"; Components: exemples; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:PyIcon}"; Filename: "{app}\Py42\{#PyExeName}"; WorkingDir: "{app}\Py42"; Components: py42
Name: "{group}\{cm:DocIcon}"; Filename: "{app}\doc"; Components: doc
Name: "{group}\{cm:ExIcon}"; Filename: "{app}\exemples"; Components: exemples
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{autodesktop}\{cm:PyIcon}"; Filename: "{app}\Py42\{#PyExeName}"; WorkingDir: "{app}\Py42"; Tasks: desktopicon_py

[InstallDelete]
; anciennes versions : les PDF étaient installés à côté de l'exe
Type: files; Name: "{app}\*.pdf"

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#DoubleAmp(MyAppName)}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Py42\{#PyExeName}"; WorkingDir: "{app}\Py42"; Description: "{cm:LaunchProgram,Py42}"; Components: py42; Flags: nowait postinstall skipifsilent unchecked

[Code]
{ Les deux applications sont publiées "framework-dependent" : elles ont besoin
  du .NET 10 Desktop Runtime x64. On le cherche dans Program Files\dotnet. }
function HasDesktopRuntime10: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not HasDesktopRuntime10 then
    if SuppressibleMsgBox(CustomMessage('NoDotNet'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;
