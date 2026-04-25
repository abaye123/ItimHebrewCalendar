; Inno Setup installer for ItimHebrewCalendar.
;
; Build steps:
;   1. dotnet publish -c Release -r win-x64
;   2. iscc HebDate.iss   (or run via Inno Setup IDE)
;   3. Output goes to ..\Release\

#define AppName "ItimHebrewCalendar"
#define AppNameH "עיתים - לוח שנה עברי"
#define AppVersion "1.1.0"
#define AppPublisher "abaye"
#define AppExeName "ItimHebrewCalendar.exe"
#define SourceFolder "..\bin\x64\Release\net8.0-windows10.0.19041.0"
#define WinAppRuntimeInstaller "Redist\WindowsAppRuntimeInstall-x64.exe"

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
; Windows App Runtime installer - נכלל במתקין אך מועתק לתיקייה זמנית בלבד
Source: "{#WinAppRuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsWinAppRuntime

[Icons]
Name: "{group}\{#AppNameH}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppNameH}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppNameH}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\AppIcon.ico"

[Registry]
; HKCU Run entry for the optional auto-start with Windows.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
; התקנת Windows App Runtime לפני הפעלת האפליקציה (אם נדרש)
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; \
    Parameters: "--quiet"; \
    StatusMsg: "מתקין את Windows App Runtime..."; \
    Check: NeedsWinAppRuntime; \
    Flags: waituntilterminated

Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

[Code]
function IsWinAppRuntimeInstalled: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  SubKey, DisplayName: string;
begin
  Result := False;
  
  // בדיקה ב-Uninstall registry של 64-bit
  if RegGetSubkeyNames(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      SubKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[I];
      if RegQueryStringValue(HKLM, SubKey, 'DisplayName', DisplayName) then
      begin
        if (Pos('Windows App Runtime', DisplayName) > 0) or 
           (Pos('Microsoft.WindowsAppRuntime', DisplayName) > 0) then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
  
  // בדיקה גם ב-WOW6432Node
  if not Result then
  begin
    if RegGetSubkeyNames(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
    begin
      for I := 0 to GetArrayLength(Names) - 1 do
      begin
        SubKey := 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[I];
        if RegQueryStringValue(HKLM, SubKey, 'DisplayName', DisplayName) then
        begin
          if (Pos('Windows App Runtime', DisplayName) > 0) or 
             (Pos('Microsoft.WindowsAppRuntime', DisplayName) > 0) then
          begin
            Result := True;
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

function NeedsWinAppRuntime: Boolean;
begin
  Result := not IsWinAppRuntimeInstalled;
end;