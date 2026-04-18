[Setup]
AppId={{A4D6E5D4-2F5D-4E4B-95A4-0E7A5F7D8E11}}
AppName=Buff
AppVersion=1.0.1
AppVerName=Buff
UninstallDisplayName=Buff
UninstallDisplayIcon={app}\tray.ico
AppPublisher=Shanto Joseph
AppPublisherURL=https://github.com/shanto-joseph/Buff
AppSupportURL=https://github.com/shanto-joseph/Buff
AppUpdatesURL=https://github.com/shanto-joseph/Buff/releases/latest
DefaultDirName={autopf}\Buff
DefaultGroupName=Buff
OutputDir=..\build\installer
OutputBaseFilename=Buff-Setup
SetupIconFile=..\Assets\tray.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\Assets\*"; DestDir: "{app}\Assets"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\bin\Release\net10.0-windows10.0.26100.0\win-x64\App.xbf"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net10.0-windows10.0.26100.0\win-x64\MainWindow.xbf"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net10.0-windows10.0.26100.0\win-x64\Buff.App.pri"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Assets\tray.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Buff"; Filename: "{app}\Buff.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\tray.ico"
Name: "{autodesktop}\Buff"; Filename: "{app}\Buff.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\tray.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: checkedonce

[Run]
Filename: "{app}\Buff.App.exe"; Description: "Launch Buff"; Flags: nowait postinstall skipifsilent
