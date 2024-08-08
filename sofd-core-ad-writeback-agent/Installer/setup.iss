; This file is a script that allows to build the OrgSyncer instalation package
; To generate the installer, define the variable MyAppSourceDir MUST point to the Directory where the dll's should be copied from
; The script may be executed from the console-mode compiler - iscc "c:\isetup\samples\my script.iss" or from the Inno Setup Compiler UI
#define AppId "{{6218dc7d-2947-490a-8c32-39130daec6ad}"
#define AppSourceDir "..\SOFD Core AD Writeback Agent\bin\Debug"
#define AppName "SofdCoreADWritebackAgent"
#define AppPublisher "Digital Identity"
#define AppURL "http://digital-identity.dk/"
#define ExeName "SOFD Core AD Writeback Agent.exe"
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
OutputBaseFilename=SOFD Core - AD Writeback Agent
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
Source: "SOFD Core AD Writeback Agent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "SOFD Core AD Writeback Agent.exe.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "AttributeWriteback\*"; DestDir: "{app}\AttributeWriteback"; Flags: ignoreversion onlyifdoesntexist
Source: "CustomPowershell\*"; DestDir: "{app}\CustomPowershell"; Flags: ignoreversion onlyifdoesntexist

[Run]
Filename: "{app}\{#ExeName}"; Parameters: "install --delayed" 

[UninstallRun]
Filename: "{app}\{#ExeName}"; Parameters: "uninstall"
