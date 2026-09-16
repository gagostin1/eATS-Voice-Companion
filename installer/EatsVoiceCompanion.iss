#ifndef MyAppVersion
  #define MyAppVersion "0.3.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#if PublishDir == ""
  #error PublishDir must not be empty.
#endif

#if !FileExists(PublishDir + "\EatsVoiceCompanion.App.exe")
  #error PublishDir does not contain EatsVoiceCompanion.App.exe.
#endif

#if OutputDir == ""
  #error OutputDir must not be empty.
#endif

#define MyAppName "eATS Voice Companion"
#define MyAppPublisher "Gus Agostinho"
#define MyAppExeName "EatsVoiceCompanion.App.exe"
#define MyAppUrl "https://github.com/gagostin1/eATS-Voice-Companion"

[Setup]
AppId={{1F64D590-02A2-40E4-A935-6FB6E60B94CE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases/latest
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\eATS Voice Companion
DefaultGroupName=eATS Voice Companion
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
SetupIconFile=..\EatsVoiceCompanion.App\Assets\eats-voice-companion-v2.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=EatsVoiceCompanion-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\eATS Voice Companion"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\eATS Voice Companion"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
