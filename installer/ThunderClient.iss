#define AppName "Thunder Compute Client"
#define AppVersion "0.1.0-alpha.14"
[Setup]
AppId={{9D42AC07-3F3A-4C68-AB08-89A84D327D51}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=thomasasen
AppPublisherURL=https://github.com/thomasasen/thundercomputeclient
DefaultDirName={localappdata}\Programs\ThunderClient
DefaultGroupName=Thunder Compute Client
OutputDir=..\artifacts\installer
OutputBaseFilename=ThunderClient-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
WizardStyle=modern
UninstallDisplayIcon={app}\ThunderClient.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Thunder Compute Client"; Filename: "{app}\ThunderClient.exe"
Name: "{autoprograms}\Thunder Compute Client"; Filename: "{app}\ThunderClient.exe"
[Run]
Filename: "{app}\ThunderClient.exe"; Description: "Thunder Compute Client starten"; Flags: nowait postinstall skipifsilent

