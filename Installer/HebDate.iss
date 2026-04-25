; Inno Setup installer for ItimHebrewCalendar.
;
; Build steps:
;   1. dotnet publish -c Release -r win-x64
;   2. iscc HebDate.iss   (or run via Inno Setup IDE)
;   3. Output goes to ..\Release\

#define AppName "ItimHebrewCalendar"
#define AppVersion "1.0.0"
#define AppPublisher "abaye"
#define AppExeName "ItimHebrewCalendar.exe"

#define SourceFolder "..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

#define WinAppRuntimeUrl "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe"

[Setup]
AppId={{7F2E4C1B-8A3D-4B6E-A5C9-1F8D7E2A9B4C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL=https://github.com/abaye123/ItimHebrewCalendar
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\Release
OutputBaseFilename=ItimHebrewCalendar-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\Assets\AppIcon.ico
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"

[Tasks]
Name: "desktopicon"; Description: "צור קיצור דרך על שולחן העבודה"; GroupDescription: "קיצורי דרך נוספים:"; Flags: unchecked
Name: "startup"; Description: "הפעל אוטומטית בעליית Windows (ב-tray)"; GroupDescription: "הפעלה:"

[Files]
Source: "{#SourceFolder}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\AppIcon.ico"

[Registry]
; HKCU Run entry for the optional auto-start with Windows.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

[Code]
function NeedsWinAppRuntime: Boolean;
var
  SubKey: string;
begin
  Result := True;
  SubKey := 'Software\Microsoft\WindowsAppRuntime\Packages';
  if RegKeyExists(HKLM, SubKey) or RegKeyExists(HKCU, SubKey) then
  begin
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Msg: String;
begin
  if CurStep = ssInstall then
  begin
    if NeedsWinAppRuntime then
    begin
      Msg := 'התקנת ItimHebrewCalendar דורשת את Windows App Runtime 1.7.' + #13#10 + #13#10 +
             'האם לפתוח את דף ההורדה של Microsoft עכשיו?' + #13#10 +
             'לאחר ההתקנה - יש להריץ שוב את מתקין ItimHebrewCalendar.';
      if MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES then
      begin
        ShellExec('open', '{#WinAppRuntimeUrl}', '', '', SW_SHOW, ewNoWait, ResultCode);
        Abort;
      end;
    end;
  end;
end;
