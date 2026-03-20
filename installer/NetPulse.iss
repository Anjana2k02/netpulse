#define MyAppName "NetPulse"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "NetPulse-Setup"
#endif

[Setup]
AppId={{73BDEF9D-646C-4AA7-8332-B0827304C915}
AppName={#MyAppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\NetPulse
DefaultGroupName=NetPulse
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\NetPulse.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NetPulse"; Filename: "{app}\NetPulse.exe"
Name: "{autodesktop}\NetPulse"; Filename: "{app}\NetPulse.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NetPulse.exe"; Description: "Launch NetPulse"; Flags: nowait postinstall skipifsilent
