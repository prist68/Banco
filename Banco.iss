#define MyAppName "Banco"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DILTECH"
#define MyAppExeName "Banco.exe"
#define MyAppSourceDir "publish\\Banco"
#define MyFastReportSourceDir "FastReport"

[Setup]
AppId={{8E6B2D02-6C42-4E75-A8A0-6C18F6C24B51}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\Banco
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
WizardStyle=modern
OutputDir=installer
OutputBaseFilename=Banco-Setup
SetupIconFile=Banco.UI.Wpf\Immagini\Banco.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter={#MyAppExeName}

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "Crea collegamento sul desktop"; GroupDescription: "Collegamenti:"

[Dirs]
Name: "{app}\Config"; Flags: uninsneveruninstall
Name: "{app}\LocalStore"; Flags: uninsneveruninstall
Name: "{app}\Log"; Flags: uninsneveruninstall
Name: "{app}\Stampa"; Flags: uninsneveruninstall
Name: "{app}\Stampa\Layouts"; Flags: uninsneveruninstall
Name: "{app}\Stampa\DesignData"; Flags: uninsneveruninstall
Name: "{app}\Stampa\Anteprime"; Flags: uninsneveruninstall
Name: "{app}\FastReport"; Flags: uninsneveruninstall

[Files]
Source: "{#MyAppSourceDir}\Config\appsettings.user.json"; DestDir: "{app}\Config"; Flags: onlyifdoesntexist ignoreversion
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Config\*,LocalStore\*,Log\*,*.pdb,QuestPDF.dll,runtimes\win-x64\native\QuestPdfSkia.dll,runtimes\win-x86\native\QuestPdfSkia.dll,Stampa\Anteprime\*,Stampa\DesignData\*,Stampa\Layouts\*.customized.lock,Stampa\questpdf-*,Stampa\questpdf-layout-settings.json,Stampa\*.pdf,Stampa\*.xps,Stampa\*.html"
Source: "{#MyFastReportSourceDir}\*"; DestDir: "{app}\FastReport"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{app}\Banco.Repx.dll"
Type: files; Name: "{app}\Banco.Repx.pdb"
Type: files; Name: "{app}\QuestPDF.dll"
Type: files; Name: "{app}\runtimes\win-x64\native\QuestPdfSkia.dll"
Type: files; Name: "{app}\runtimes\win-x86\native\QuestPdfSkia.dll"
Type: files; Name: "{app}\Stampa\questpdf-layout-settings.json"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Avvia Banco"; Flags: nowait postinstall skipifsilent shellexec

[Code]
function StartsWithText(const Prefix, Value: string): Boolean;
begin
  Result := CompareText(Copy(Value, 1, Length(Prefix)), Prefix) = 0;
end;

function ShouldPreserveInstallEntry(const EntryName: string): Boolean;
begin
  Result :=
    (CompareText(EntryName, 'Config') = 0) or
    (CompareText(EntryName, 'LocalStore') = 0) or
    (CompareText(EntryName, 'Log') = 0) or
    (CompareText(EntryName, 'Stampa') = 0) or
    (CompareText(EntryName, 'FastReport') = 0) or
    StartsWithText('unins', LowerCase(EntryName));
end;

procedure CleanObsoleteInstallArtifacts();
var
  FindRec: TFindRec;
  AppDir: string;
  EntryName: string;
  EntryPath: string;
begin
  AppDir := ExpandConstant('{app}');
  if not DirExists(AppDir) then
  begin
    exit;
  end;

  Log(Format('Pulizia preventiva cartella applicativa: %s', [AppDir]));

  if FindFirst(AppDir + '\*', FindRec) then
  begin
    try
      repeat
        EntryName := FindRec.Name;
        if (EntryName = '.') or (EntryName = '..') then
        begin
          continue;
        end;

        if ShouldPreserveInstallEntry(EntryName) then
        begin
          Log(Format('Preservo voce persistente: %s', [EntryName]));
          continue;
        end;

        EntryPath := AppDir + '\' + EntryName;

        if DirExists(EntryPath) then
        begin
          if DelTree(EntryPath, True, True, True) then
          begin
            Log(Format('Cartella obsoleta rimossa: %s', [EntryPath]));
          end
          else
          begin
            Log(Format('Impossibile rimuovere cartella obsoleta: %s', [EntryPath]));
          end;
        end
        else
        begin
          if DeleteFile(EntryPath) then
          begin
            Log(Format('File obsoleto rimosso: %s', [EntryPath]));
          end
          else
          begin
            Log(Format('Impossibile rimuovere file obsoleto: %s', [EntryPath]));
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    CleanObsoleteInstallArtifacts();
  end;
end;
