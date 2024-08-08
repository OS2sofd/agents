; This file is a script that allows to build the OrgSyncer instalation package
; To generate the installer, define the variable MyAppSourceDir MUST point to the Directory where the dll's should be copied from
; The script may be executed from the console-mode compiler - iscc "c:\isetup\samples\my script.iss" or from the Inno Setup Compiler UI
#define AppId "{{b789c27a-cd93-443f-bb21-cc2f82b7d40c}"
#define AppSourceDir "..\SOFD Core Account Agent\bin\Debug"
#define AppName "SofdCoreAccountAgent"
#define AppPublisher "Digital Identity"
#define AppURL "http://digital-identity.dk/"
#define ExeName "SOFD Core User Agent.exe"
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
OutputBaseFilename=SOFD Core - User Account Agent
Compression=lzma
SolidCompression=yes
SourceDir={#AppSourceDir}
OutputDir=..\..\..\Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "SOFD Core User Agent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "SOFD Core User Agent.exe.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "ActiveDirectory\*"; DestDir: "{app}\ActiveDirectory"; Flags: ignoreversion onlyifdoesntexist
Source: "Exchange\*"; DestDir: "{app}\Exchange"; Flags: ignoreversion onlyifdoesntexist
Source: "InternalPowershell\*"; DestDir: "{app}\InternalPowershell"; Flags: ignoreversion

[Run]
Filename: "{app}\{#ExeName}"; Parameters: "install --delayed" 

[UninstallRun]
Filename: "{app}\{#ExeName}"; Parameters: "uninstall"
