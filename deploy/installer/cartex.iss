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
Source: "pgsql\*"; DestDir: "{app}\pgsql"; Flags: recursesubdirs ignoreversion; Check: not PgExists

[Icons]
Name: "{autoprograms}\Cartex"; Filename: "{app}\desktop\Cartex.Desktop.exe"
Name: "{autodesktop}\Cartex"; Filename: "{app}\desktop\Cartex.Desktop.exe"

[Code]
var
  DevPasswordPage: TInputQueryWizardPage;
  AdminPasswordPage: TInputQueryWizardPage;

function PgExists: Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\pgsql\bin'));
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

function RandomKey(Len: Integer): String;
var
  I: Integer;
const
  Chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
begin
  Result := '';
  for I := 1 to Len do
    Result := Result + Chars[Random(Length(Chars)) + 1];
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  DbPassword, JwtKey, Config: String;
begin
  if CurStep <> ssPostInstall then Exit;

  DbPassword := RandomKey(24);
  JwtKey := RandomKey(64);

  { PostgreSQL init + servis }
  SaveStringToFile(ExpandConstant('{app}\pgpass.tmp'), DbPassword, False);
  Exec(ExpandConstant('{app}\pgsql\bin\initdb.exe'),
    ExpandConstant('-D "{app}\pgdata" -E UTF8 --locale=C -U cartex --pwfile="{app}\pgpass.tmp"'),
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  DeleteFile(ExpandConstant('{app}\pgpass.tmp'));
  Exec(ExpandConstant('{app}\pgsql\bin\pg_ctl.exe'),
    ExpandConstant('register -N CartexDb -D "{app}\pgdata" -S auto'),
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('net', 'start CartexDb', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{app}\pgsql\bin\createdb.exe'), '-U cartex cartex', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  { appsettings.Production.json }
  Config := '{' + #13#10 +
    '  "Urls": "http://0.0.0.0:5015",' + #13#10 +
    '  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Database=cartex;Username=cartex;Password=' + DbPassword + '" },' + #13#10 +
    '  "Jwt": { "Key": "' + JwtKey + '" },' + #13#10 +
    '  "Seed": { "DeveloperPassword": "' + DevPasswordPage.Values[0] + '", "AdminPassword": "' + AdminPasswordPage.Values[0] + '" }' + #13#10 +
    '}';
  SaveStringToFile(ExpandConstant('{app}\api\appsettings.Production.json'), Config, False);

  { API servis }
  Exec('sc', ExpandConstant('create CartexApi binPath= "{app}\api\Cartex.Api.exe" start= delayed-auto obj= LocalSystem'),
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc', 'config CartexApi depend= CartexDb', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd', '/c sc failure CartexApi reset= 86400 actions= restart/5000', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('setx', 'ASPNETCORE_ENVIRONMENT Production /M', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  { Firewall (LAN kassalar uchun) }
  Exec('netsh', 'advfirewall firewall add rule name="Cartex API" dir=in action=allow protocol=TCP localport=5015',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Exec('net', 'start CartexApi', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  MsgBox('O''rnatish tugadi.' + #13#10 + #13#10 +
    'Boshqa kassalarda: Sozlamalar -> Server manzili -> http://<shu-kompyuter-IP>:5015' + #13#10 +
    'Egasi: admin / siz kiritgan parol' + #13#10 +
    'Sozlovchi: developer / siz kiritgan parol' + #13#10 + #13#10 +
    'Sotuvchilarni Foydalanuvchilar bo''limidan qo''shasiz.', mbInformation, MB_OK);
end;
