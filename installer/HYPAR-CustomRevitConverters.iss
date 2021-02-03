#define MyAppName "HYPAR Converters by Parallax Team"
#define MyAppVersion "2021.2.1"
#define MyAppPublisher "ParallaxTeam"
#define MyAppURL "https://github.com/ParallaxTeam/HyparElementConverters"

#define HyparConverterFolder "{%USERPROFILE}\.hypar\converters\revit"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{18EBB6B9-C5AF-4AB5-849B-8A0620F09D8E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName=Parallax Team, Inc\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=C:\Users\johnpierson\Documents\Repos\HyparElementConverters\LICENSE
OutputDir=.
OutputBaseFilename=HYPAR-CustomRevitConverters.v{#MyAppVersion}
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Components]
Name: curtainWallConverter; Description: Hypar Revit Curtain Wall Converter;  Types: full
Name: roofConverter; Description: Hypar Roof Converter;  Types: full

[Files]
; Curtain Wall Converter Option
Source: "C:\Users\johnpierson\Documents\Repos\HyparElementConverters\src\CurtainWall\HyparRevitCurtainWallConverter\bin\Debug\HyparRevitCurtainWallConverter.dll"; DestDir: "{#HyparConverterFolder}"; Flags: ignoreversion; Components: curtainWallConverter 

; Roof Converter Option
Source: "C:\Users\johnpierson\Documents\Repos\HyparElementConverters\src\Roof\HyparRevitRoofConverter\bin\Debug\HyparRevitRoofConverter.dll"; DestDir: "{#HyparConverterFolder}"; Flags: ignoreversion; Components: roofConverter 
