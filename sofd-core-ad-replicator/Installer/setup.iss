#define AppId "{{9db43678-cb1e-494f-9845-f6db18f54758}"
#define AppSourceDir "..\bin\Debug\net6.0\publish\win-x86"
#define AppName "SofdCoreADReplicator"
#define AppPublisher "Digital Identity"
#define AppURL "http://digital-identity.dk/"
#define ExeName "sofd-core-ad-replicator.exe"
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
OutputBaseFilename={#AppName}
Compression=lzma
SolidCompression=yes
SourceDir= {#SourcePath}\{#AppSourceDir}
OutputDir={#SourcePath}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "*.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion onlyifdoesntexist


[Run]
Filename: "sc.exe"; Parameters: "create ""{#AppName}"" binpath= ""{app}\{#ExeName}"" displayname=""{#AppName}"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "description ""{#AppName}"" ""{#AppName}"""; Flags: runhidden



[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""{#AppName}"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete ""{#AppName}"""; Flags: runhidden