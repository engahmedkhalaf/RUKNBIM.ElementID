[Setup]
AppName=RUKNBIM Smart Select
AppVersion=1.0.0
AppPublisher=RUKNBIM
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\RUKNBIM.ElementID.bundle
DefaultGroupName=RUKNBIM
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=RUKNBIM.SmartSelect.Setup
DisableDirPage=yes
DisableProgramGroupPage=yes
DirExistsWarning=no

[Files]
; Copy PackageContents.xml to the bundle root
Source: "C:\Users\sati7\AppData\Roaming\Autodesk\ApplicationPlugins\RUKNBIM.ElementID.bundle\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion

; Copy all contents recursively (dlls, images, localizations)
Source: "C:\Users\sati7\AppData\Roaming\Autodesk\ApplicationPlugins\RUKNBIM.ElementID.bundle\Contents\*"; DestDir: "{app}\Contents"; Flags: ignoreversion recursesubdirs createallsubdirs

[Messages]
SetupAppTitle=Install RUKNBIM Smart Select
SetupWindowTitle=RUKNBIM Smart Select Installer
