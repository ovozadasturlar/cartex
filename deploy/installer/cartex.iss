; Cartex lokal server o'rnatuvchisi (Inno Setup 6)
; Tayyorlash:
;   dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/api
;   dotnet publish src/desktop/Cartex.Desktop/Cartex.Desktop.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/desktop
;   PostgreSQL 16 portable zip -> deploy/installer/pgsql (bin/initdb.exe mavjud bo'lsin)

#define AppName "Cartex"
#define AppVersion "1.0.0"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Cartex
DefaultGroupName=Cartex
OutputBaseFilename=cartex-setup-{#AppVersion}
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Files]
Source: "publish\api\*"; DestDir: "{app}\api"; Flags: recursesubdirs ignoreversion
Source: "publish\desktop\*"; DestDir: "{app}\desktop"; Flags: recursesubdirs ignoreversion
Source: "pgsql\*"; DestDir: "{app}\pgsql"; Flags: recursesubdirs ignoreversion; Check: PgMissing

[Icons]
Name: "{autoprograms}\Cartex"; Filename: "{app}\desktop\Cartex.Desktop.exe"
Name: "{autodesktop}\Cartex"; Filename: "{app}\desktop\Cartex.Desktop.exe"

[UninstallRun]
Filename: "net"; Parameters: "stop CartexApi"; Flags: runhidden; RunOnceId: "StopApi"
Filename: "sc"; Parameters: "delete CartexApi"; Flags: runhidden; RunOnceId: "DeleteApi"
Filename: "net"; Parameters: "stop CartexDb"; Flags: runhidden; RunOnceId: "StopDb"
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "unregister -N CartexDb"; Flags: runhidden skipifdoesntexist; RunOnceId: "DeleteDb"
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Cartex API"""; Flags: runhidden; RunOnceId: "DeleteFirewall"

[Code]
var
  DevPasswordPage: TInputQueryWizardPage;
  AdminPasswordPage: TInputQueryWizardPage;
  PgAlreadyInstalled: Boolean;

function PgMissing: Boolean;
begin
  Result := not PgAlreadyInstalled;
end;

procedure InitializeWizard;
begin
  DevPasswordPage := CreateInputQueryPage(wpSelectDir,
    'Developer parol', 'Tizim sozlovchisi (developer) uchun parol',
    'Bu parol bilan siz dasturga developer sifatida kirasiz. Kamida 8 belgi.');
  DevPasswordPage.Add('Parol:', True);

  AdminPasswordPage := CreateInputQueryPage(DevPasswordPage.ID,
    'Egasi (admin) paroli', 'Do''kon egasi (admin) uchun parol',
    'Do''kon egasi shu parol bilan kiradi (login: admin). Kamida 8 belgi.');
  AdminPasswordPage.Add('Parol:', True);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = DevPasswordPage.ID) and (Length(DevPasswordPage.Values[0]) < 8) then
  begin
    MsgBox('Parol kamida 8 belgi bo''lishi kerak.', mbError, MB_OK);
    Result := False;
  end;
  if (CurPageID = AdminPasswordPage.ID) and (Length(AdminPasswordPage.Values[0]) < 8) then
  begin
    MsgBox('Parol kamida 8 belgi bo''lishi kerak.', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  PgAlreadyInstalled := DirExists(ExpandConstant('{app}\pgsql\bin'));
  Exec('net', 'stop CartexApi', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('net', 'stop CartexDb', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function CryptoRandom(Expression: String): String;
var
  ResultCode: Integer;
  TempFile: String;
  Content: AnsiString;
begin
  TempFile := ExpandConstant('{tmp}\cartex-key.txt');
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -Command "[IO.File]::WriteAllText(''' + TempFile + ''', ' + Expression + ')"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if (ResultCode <> 0) or (not LoadStringFromFile(TempFile, Content)) then
    RaiseException('Xavfsiz kalit yaratib bo''lmadi (PowerShell xatosi).');
  DeleteFile(TempFile);
  Result := Trim(String(Content));
  if Length(Result) < 24 then
    RaiseException('Xavfsiz kalit yaratib bo''lmadi.');
end;

function JsonEscape(S: String): String;
var
  I: Integer;
begin
  Result := '';
  for I := 1 to Length(S) do
  begin
    if (S[I] = '"') or (S[I] = '\') then
      Result := Result + '\' + S[I]
    else
      Result := Result + S[I];
  end;
end;

procedure Run(Exe, Params: String);
var
  ResultCode: Integer;
begin
  Exec(Exe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure RunOrFail(Exe, Params, ErrorText: String);
var
  ResultCode: Integer;
begin
  if (not Exec(Exe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
    RaiseException(ErrorText + ' (kod: ' + IntToStr(ResultCode) + ')');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DbPassword, JwtKey, DataDir, ConfigPath: String;
  Config: TArrayOfString;
  FreshDb: Boolean;
begin
  if CurStep <> ssPostInstall then Exit;

  if not FileExists(ExpandConstant('{app}\pgsql\bin\initdb.exe')) then
    RaiseException('PostgreSQL fayllari topilmadi: {app}\pgsql\bin\initdb.exe');

  DataDir := ExpandConstant('{commonappdata}\Cartex');
  ConfigPath := ExpandConstant('{app}\api\appsettings.Production.json');
  FreshDb := not DirExists(ExpandConstant('{app}\pgdata'));

  if FreshDb then
  begin
    DbPassword := CryptoRandom('[Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))');
    SaveStringToFile(ExpandConstant('{app}\pgpass.tmp'), DbPassword, False);
    RunOrFail(ExpandConstant('{app}\pgsql\bin\initdb.exe'),
      ExpandConstant('-D "{app}\pgdata" -E UTF8 --locale=C -U cartex -A scram-sha-256 --pwfile="{app}\pgpass.tmp"'),
      'PostgreSQL bazasini yaratib bo''lmadi');
    DeleteFile(ExpandConstant('{app}\pgpass.tmp'));
  end
  else if not FileExists(ConfigPath) then
    RaiseException('Baza mavjud, lekin sozlama fayli yo''q — baza parolini tiklab bo''lmaydi:' + #13#10 + ConfigPath);

  Run(ExpandConstant('{app}\pgsql\bin\pg_ctl.exe'), ExpandConstant('register -N CartexDb -D "{app}\pgdata" -S auto'));
  RunOrFail('net', 'start CartexDb', 'PostgreSQL servisi ishga tushmadi');

  if FreshDb then
    Run('cmd', '/c set "PGPASSWORD=' + DbPassword + '" && ' +
      ExpandConstant('"{app}\pgsql\bin\createdb.exe" -U cartex -h 127.0.0.1 cartex'));

  CreateDir(DataDir);
  CreateDir(DataDir + '\storage');
  CreateDir(DataDir + '\keys');

  if FreshDb then
  begin
    JwtKey := CryptoRandom('[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))');
    SetArrayLength(Config, 7);
    Config[0] := '{';
    Config[1] := '  "Urls": "http://0.0.0.0:5015",';
    Config[2] := '  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Database=cartex;Username=cartex;Password=' + DbPassword + '" },';
    Config[3] := '  "Jwt": { "Key": "' + JsonEscape(JwtKey) + '" },';
    Config[4] := '  "Storage": { "LocalPath": "' + JsonEscape(DataDir) + '\\storage" },';
    Config[5] := '  "DataProtection": { "KeysPath": "' + JsonEscape(DataDir) + '\\keys" },';
    Config[6] := '  "Seed": { "DeveloperPassword": "' + JsonEscape(DevPasswordPage.Values[0]) + '", "AdminPassword": "' + JsonEscape(AdminPasswordPage.Values[0]) + '" }' + #13#10 + '}';
    if not SaveStringsToUTF8File(ConfigPath, Config, False) then
      RaiseException('Sozlama faylini yozib bo''lmadi: ' + ConfigPath);
  end;

  RunOrFail('icacls', '"' + ConfigPath + '" /inheritance:r /grant:r *S-1-5-18:(R) /grant:r *S-1-5-32-544:(F)',
    'Sozlama fayli ruxsatlarini cheklab bo''lmadi');

  Run('sc', ExpandConstant('create CartexApi binPath= "{app}\api\Cartex.Api.exe" start= delayed-auto obj= LocalSystem'));
  Run('sc', ExpandConstant('config CartexApi binPath= "{app}\api\Cartex.Api.exe" depend= CartexDb'));
  Run('cmd', '/c sc failure CartexApi reset= 86400 actions= restart/5000');
  Run('setx', 'ASPNETCORE_ENVIRONMENT Production /M');

  Run('netsh', 'advfirewall firewall add rule name="Cartex API" dir=in action=allow protocol=TCP localport=5015');

  RunOrFail('net', 'start CartexApi', 'Cartex servisi ishga tushmadi');

  MsgBox('O''rnatish tugadi.' + #13#10 + #13#10 +
    'Boshqa kassalarda: Sozlamalar -> Server manzili -> http://<shu-kompyuter-IP>:5015' + #13#10 +
    'Egasi: admin / siz kiritgan parol' + #13#10 +
    'Sozlovchi: developer / siz kiritgan parol' + #13#10 + #13#10 +
    'Ma''lumotlar: ' + DataDir + ' (zaxiraga shu papka va baza kiradi)' + #13#10 +
    'Sotuvchilarni Foydalanuvchilar bo''limidan qo''shasiz.', mbInformation, MB_OK);
end;
