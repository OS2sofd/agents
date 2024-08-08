#define AppId "{{1edf9312-b1b4-4f8a-b6a3-24e937fccbd1}"
#define AppSourceDir "..\SOFDCoreAD.Service\bin\Debug"
#define AppName "SofdCoreADEventDispatcher"
#define AppPublisher "Digital Identity"
#define AppURL "http://digital-identity.dk/"
#define AppExeName "SOFD Core - AD indlaesningsintegration"
#define ExeName "SOFDCoreAD.Service.exe"
#define AppVersion GetFileVersion(AppSourceDir + "\" + ExeName)

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={pf}\{#AppPublisher}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputBaseFilename={#AppExeName}
Compression=lzma
SolidCompression=yes
SourceDir={#AppSourceDir}
OutputDir=..\..\..\Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "*.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.txt"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\{#ExeName}"; Parameters: "install --delayed" 

[UninstallRun]
Filename: "{app}\{#ExeName}"; Parameters: "uninstall"
