; AppLauncher v1.0.0 インストーラースクリプト
; Inno Setup 6.x 用

#define AppName      "AppLauncher"
#define AppVersion   "1.0.0"
#define AppPublisher "dabasa1942"
#define AppExeName   "AppLauncher.exe"
#define SourceDir    "..\publish\v1.0.0"

[Setup]
AppId={{B7F4C2A1-3E8D-4F5B-9C6A-2D1E7F8A9B0C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL=
AppSupportURL=
AppUpdatesURL=
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
; 出力先フォルダ（このスクリプトと同じ場所）
OutputDir=.
OutputBaseFilename=AppLauncher_v{#AppVersion}_setup
; アイコン
SetupIconFile=..\src\resources\icons\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 64ビット専用
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; UAC（管理者権限）
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; バージョン情報
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} インストーラー
VersionInfoCopyright=Copyright © 2026 {#AppPublisher}

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon";     Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加タスク:"
Name: "startupicon";    Description: "スタートアップに登録する（Windows 起動時に自動起動）"; GroupDescription: "追加タスク:"; Flags: unchecked

[Files]
; 実行ファイル（.pdb / .xml は除外）
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; README
Source: "..\dist\README.txt";         DestDir: "{app}"; Flags: ignoreversion

[Icons]
; スタートメニュー
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} をアンインストール"; Filename: "{uninstallexe}"
; デスクトップ（タスク選択時のみ）
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
; スタートアップ（タスク選択時のみ）
Name: "{userstartup}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: startupicon

[Run]
; インストール完了後に起動するかどうか確認
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; アンインストール前にプロセスを終了
Filename: "taskkill.exe"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden skipifdoesntexist
