; Cartex Desktop client o'rnatuvchisi (Inno Setup 6)
; Faqat mijoz ilovasi: API va PostgreSQL'ni o'rnatmaydi.

#define AppName "Cartex Desktop"
#define AppVersion "0.0.2"

[Setup]
AppId={{9C55D522-F2F4-43F3-A15B-114856CCB65E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Cartex
DefaultDirName={autopf}\Cartex Desktop
DefaultGroupName=Cartex
OutputDir=output
OutputBaseFilename=cartex-desktop-setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Cartex.Desktop.exe

[Files]
Source: "publish\desktop-client\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Cartex"; Filename: "{app}\Cartex.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Cartex"; Filename: "{app}\Cartex.Desktop.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\Cartex.Desktop.exe"; Description: "Cartex'ni ishga tushirish"; Flags: nowait postinstall skipifsilent
