#define MyAppName "Gaeul Desktop Pet"
#define MyAppExeName "GaeulDesktopPet.exe"

[Setup]
AppId={{77F2D7B1-30AF-4F1F-94B0-C54933A13431}
AppName={#MyAppName}
AppVersion=1.0.0
DefaultDirName={localappdata}\GaeulDesktopPet
DefaultGroupName={#MyAppName}
OutputBaseFilename=GaeulDesktopPetSetup
SetupIconFile=..\src\GaeulDesktopPet\Assets\Icon\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\dist\GaeulDesktopPet-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
