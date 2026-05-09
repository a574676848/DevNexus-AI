#define AppName "DevNexus AI"
#define AppPublisher "DevNexus"
#define AppExeName "DevNexus.Client.exe"
#define UpdaterExeName "DevNexus.Client.Updater.exe"

[Setup]
AppId={{8D0E8AA5-0B27-4D8A-83D9-4D54E9A9A6A1}
AppName={#AppName}
AppVersion={#GetEnv("DNX_VERSION")}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\DevNexus AI
DefaultGroupName={#AppName}
OutputDir={#GetEnv("DNX_OUTPUT_DIR")}
OutputBaseFilename={#GetEnv("DNX_OUTPUT_BASENAME")}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务"; Flags: unchecked

[Files]
Source: "{#GetEnv("DNX_PUBLISH_DIR")}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent
