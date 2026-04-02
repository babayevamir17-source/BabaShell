; BabaShell installer (Inno Setup 6)
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#define AppName "BabaShell"
#define DistDir "..\\dist"
#define VsixPath "{#DistDir}\\vs\\BabaShell.Vsix.vsix"

[Setup]
AppId={{D2E0E1B6-6D0C-4E7A-9F2D-8E2F7C7A0A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=BabaShell
DefaultDirName={autopf}\BabaShell
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir=..\\dist
OutputBaseFilename=BabaShell-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Files]
Source: "{#DistDir}\\cli\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DistDir}\\vscode\\BabaShell.vsix"; DestDir: "{app}"; Flags: ignoreversion; Check: FileExists(ExpandConstant('{#DistDir}\\vscode\\BabaShell.vsix'))
#if FileExists(VsixPath)
Source: "{#VsixPath}"; DestDir: "{app}"; Flags: ignoreversion
#endif

[Icons]
Name: "{autoprograms}\\BabaShell"; Filename: "{app}\\babashell.exe"

[Run]
Filename: "{code:GetCodeExe}"; Parameters: "--install-extension ""{app}\\BabaShell.vsix"""; StatusMsg: "Installing VS Code extension..."; Flags: runhidden; Check: IsCodeAvailable and FileExists(ExpandConstant('{app}\\BabaShell.vsix'))
#if FileExists(VsixPath)
Filename: "VSIXInstaller.exe"; Parameters: """{app}\\BabaShell.Vsix.vsix"""; StatusMsg: "Installing Visual Studio extension..."; Flags: runhidden; Check: FileExists(ExpandConstant('{app}\\BabaShell.Vsix.vsix'))
#endif

[Registry]
Root: HKLM; Subkey: "Software\\Classes\\.babashell"; ValueType: string; ValueName: ""; ValueData: "BabaShellFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\\Classes\\BabaShellFile"; ValueType: string; ValueName: ""; ValueData: "BabaShell Script"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\\Classes\\BabaShellFile\\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\\babashell.exe,0"
Root: HKLM; Subkey: "Software\\Classes\\BabaShellFile\\shell\\open\\command"; ValueType: string; ValueName: ""; ValueData: """{app}\\babashell.exe"" ""%1"""
Root: HKLM; Subkey: "Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\babashell.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\\babashell.exe"; Flags: uninsdeletekey

[Code]
procedure AddToPath(const Dir: String);
var
  PathEnv: String;
begin
  if not RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', PathEnv) then
    PathEnv := '';

  if Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(PathEnv) + ';') = 0 then
  begin
    if PathEnv = '' then
      PathEnv := Dir
    else
      PathEnv := PathEnv + ';' + Dir;

    RegWriteExpandStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', PathEnv);
  end;
end;

procedure RemoveFromPath(const Dir: String);
var
  PathEnv: String;
begin
  if not RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', PathEnv) then
    Exit;

  StringChangeEx(PathEnv, ';' + Dir + ';', ';', True);
  if Pos(Dir + ';', PathEnv) = 1 then
    Delete(PathEnv, 1, Length(Dir) + 1);
  if (Length(PathEnv) > Length(Dir)) and (Copy(PathEnv, Length(PathEnv) - Length(Dir), Length(Dir) + 1) = ';' + Dir) then
    Delete(PathEnv, Length(PathEnv) - Length(Dir), Length(Dir) + 1);
  if CompareText(PathEnv, Dir) = 0 then
    PathEnv := '';

  RegWriteExpandStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', PathEnv);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    AddToPath(ExpandConstant('{app}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveFromPath(ExpandConstant('{app}'));
  end;
end;

function IsCodeAvailable(): Boolean;
begin
  Result := GetCodeExe('') <> '';
end;

function GetCodeExe(Param: String): String;
begin
  Result := FileSearch('code.cmd', GetEnv('PATH'));
  if Result <> '' then Exit;
  Result := FileSearch('code.exe', GetEnv('PATH'));
  if Result <> '' then Exit;

  Result := ExpandConstant('{localappdata}\Programs\Microsoft VS Code\Code.exe');
  if FileExists(Result) then Exit;
  Result := ExpandConstant('{pf}\Microsoft VS Code\Code.exe');
  if FileExists(Result) then Exit;
  Result := ExpandConstant('{pf32}\Microsoft VS Code\Code.exe');
  if FileExists(Result) then Exit;

  Result := '';
end;
