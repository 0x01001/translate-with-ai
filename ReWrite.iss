#define AppName "ReWrite"
#define AppPublisher "Huynh Nam Duy"
#define AppPublisherURL "https://github.com/hynady/ReWrite"
#define AppSupportURL "https://github.com/hynady/ReWrite/issues"
#define AppUpdatesURL "https://github.com/hynady/ReWrite"

#define AppGuid "9F3A2B42-9A72-4B8D-8B0F-6C0D6C3B3D61"
#define AppExeName "ReWrite.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{{#AppGuid}}}

AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}

AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppUpdatesURL}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

OutputDir=installer
OutputBaseFilename={#AppName}-Setup-v{#AppVersion}

SetupIconFile=ui\logo.ico

WizardStyle=modern
WizardImageFile=ui\wizard-large.bmp
WizardSmallImageFile=ui\wizard-small.bmp

Compression=lzma2
SolidCompression=yes

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

PrivilegesRequired=admin

CloseApplications=yes
RestartApplications=no

AppMutex=ReWriteMutex

LicenseFile=LICENSE.txt

[Files]
Source: "release\ReWrite-Portable-v{#AppVersion}.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon";
Name: "startup"; Description: "Start with Windows";

[Registry]

Root: HKCU; \
Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
ValueType: string; \
ValueName: "ReWrite"; \
ValueData: """{app}\{#AppExeName}"""; \
Tasks: startup; \
Flags: uninsdeletevalue

Root: HKCU; \
Subkey: "Software\Classes\Applications\{#AppExeName}\DefaultIcon"; \
ValueType: string; \
ValueData: "{app}\{#AppExeName},0"; \
Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Flags: nowait postinstall skipifsilent

[Code]

var
  LinkLabel: TLabel;
  LangPage: TInputOptionWizardPage;

procedure OpenGitHub(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec(
    'open',
    'https://github.com/hynady/ReWrite',
    '',
    '',
    SW_SHOWNORMAL,
    ewNoWait,
    ErrorCode
  );
end;

procedure InitializeWizard();
begin
  { --- BMP UI is handled by Setup section (WizardImageFile / WizardSmallImageFile) --- }

  { --- Language selection for app --- }
  LangPage := CreateInputOptionPage(
    wpWelcome,
    'ReWrite Language',
    'Choose application language',
    'This will be saved into ReWrite settings.',
    True,
    False
  );

  LangPage.Add('English (en)');
  LangPage.Add('Vietnamese (vi)');
  LangPage.Add('French (fr)');
  LangPage.Add('German (de)');
  LangPage.Add('Japanese (ja)');
  LangPage.Add('Korean (ko)');
  LangPage.Add('Chinese Simplified (zh-CN)');
  LangPage.Add('Chinese Traditional (zh-TW)');

  LangPage.Values[0] := True;

  { --- Link label (no button, no layout issue) --- }
  LinkLabel := TLabel.Create(WizardForm);
  LinkLabel.Parent := WizardForm;

  LinkLabel.Caption := 'GitHub / Support / Updates';
  LinkLabel.Font.Color := clBlue;
  LinkLabel.Font.Style := [fsUnderline];
  LinkLabel.Cursor := crHandPoint;

  LinkLabel.Left := ScaleX(20);
  LinkLabel.Top := WizardForm.ClientHeight - ScaleY(40);

  LinkLabel.OnClick := @OpenGitHub;
end;

function GetSelectedLocale(): string;
begin
  if LangPage.Values[1] then Result := 'vi'
  else if LangPage.Values[2] then Result := 'fr'
  else if LangPage.Values[3] then Result := 'de'
  else if LangPage.Values[4] then Result := 'ja'
  else if LangPage.Values[5] then Result := 'ko'
  else if LangPage.Values[6] then Result := 'zh-CN'
  else if LangPage.Values[7] then Result := 'zh-TW'
  else Result := 'en';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir, FileName, JSON: string;
begin
  if CurStep = ssPostInstall then
  begin
    Dir := ExpandConstant('{localappdata}\ReWrite');
    if not DirExists(Dir) then
      ForceDirectories(Dir);

    FileName := Dir + '\appsettings.json';

    JSON := '{"locale":"' + GetSelectedLocale() + '"}';

    SaveStringToFile(FileName, JSON, False);
  end;
end;