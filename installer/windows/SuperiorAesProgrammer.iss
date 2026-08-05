; AES Programmer & Troubleshooter - Windows 10/11 x64 installer
; Compile through scripts/build-installer.ps1 so all source paths and the
; application version are supplied consistently.

#ifndef RepoRoot
  #define RepoRoot "..\.."
#endif

#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif

#ifndef PublishRoot
  #define PublishRoot RepoRoot + "\artifacts\publish\win-x64"
#endif

#ifndef InstallerOutputRoot
  #define InstallerOutputRoot RepoRoot + "\artifacts\installer"
#endif

#ifndef DependencyRoot
  #define DependencyRoot RepoRoot + "\artifacts\windows-dependencies"
#endif

#define AppName "AES Programmer & Troubleshooter"
#define AppPublisher "AES Programmer Project"
#define AppExecutable "AesProgrammer.Troubleshooter.exe"
#define BrandingRoot RepoRoot + "\src\SuperiorAes.App\Assets\Branding"
#define AppIcon BrandingRoot + "\superior-aes.ico"
#define RequiredTrainingGuide PublishRoot + "\Assets\Training\AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.pdf"
#define FtdiInstaller DependencyRoot + "\ftdi\FTDI-CDM-VCP-Setup.exe"
#define FtdiPayloadRoot DependencyRoot + "\ftdi\payload"
#define FtdiDpInst FtdiPayloadRoot + "\dpinst-amd64.exe"
#define FtdiBusCatalog FtdiPayloadRoot + "\ftdibus.cat"
#define FtdiPortCatalog FtdiPayloadRoot + "\ftdiport.cat"

#ifdef SigningCertificate
  #if FileExists(SigningCertificate)
    #define IncludeSigningCertificate 1
  #endif
#endif

#if !FileExists(PublishRoot + "\" + AppExecutable)
  #error "The self-contained Windows publish is missing. Run scripts\publish.ps1 first."
#endif

#if !FileExists(RequiredTrainingGuide)
  #error "The published training library is incomplete."
#endif

#if !FileExists(AppIcon)
  #error "The Windows application icon is missing."
#endif

#if !FileExists(FtdiInstaller)
  #error "The pinned FTDI VCP driver installer is missing. Stage and verify it before compiling setup."
#endif

#if !FileExists(FtdiDpInst)
  #error "The verified FTDI x64 silent-install payload is missing."
#endif

#if !FileExists(FtdiBusCatalog) || !FileExists(FtdiPortCatalog)
  #error "The verified FTDI WHQL catalogs are missing."
#endif

[Setup]
AppId={{A868110A-478A-4F4E-AB61-38D2AEF21C13}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Windows installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={autopf64}\AES Programmer and Troubleshooter
DefaultGroupName=AES Programmer and Troubleshooter
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#InstallerOutputRoot}
OutputBaseFilename=AES-Programmer-Troubleshooter-v{#AppVersion}-win-x64-setup
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#AppExecutable}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
#ifdef IncludeSigningCertificate
Name: "trustcertificate"; Description: "Trust the AES Programmer Project code-signing certificate for this computer"; GroupDescription: "Certificate trust (optional):"; Flags: dontinheritcheck
#endif

[Files]
; The original, signed FTDI SFX has no reliable silent switch. Its exact,
; unchanged 28-file payload is extracted and signature-checked at build time.
; These temporary files are listed first for efficient PrepareToInstall access.
Source: "{#FtdiPayloadRoot}\*"; DestDir: "FTDI-Driver"; Flags: dontcopy noencryption recursesubdirs createallsubdirs
#ifdef IncludeSigningCertificate
Source: "{#SigningCertificate}"; DestName: "AES-Programmer-Troubleshooter-Code-Signing.cer"; Flags: dontcopy noencryption
#endif
Source: "{#PublishRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppIcon}"; DestDir: "{app}\Assets\Branding"; Flags: ignoreversion
; Keep the exact vendor wrapper available after installation for provenance and
; manual repair, even though setup uses its verified payload silently.
Source: "{#FtdiInstaller}"; DestDir: "{app}\Dependencies\FTDI"; DestName: "CDM2123620_Setup.exe"; Flags: ignoreversion
#ifdef IncludeSigningCertificate
Source: "{#SigningCertificate}"; DestDir: "{app}\Certificate"; DestName: "AES-Programmer-Troubleshooter-Code-Signing.cer"; Flags: ignoreversion
#endif
#ifdef CertificateInstructions
  #if FileExists(CertificateInstructions)
Source: "{#CertificateInstructions}"; DestDir: "{app}\Certificate"; DestName: "INSTALL-CERTIFICATE-FIRST.txt"; Flags: ignoreversion
  #endif
#endif

[Icons]
Name: "{group}\AES Programmer & Troubleshooter"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"
Name: "{group}\Uninstall AES Programmer & Troubleshooter"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AES Programmer & Troubleshooter"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExecutable}"; Description: "Launch AES Programmer & Troubleshooter"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  CertificateTrustCompletedThisRun: Boolean;
  FtdiCompletedThisRun: Boolean;

#ifdef IncludeSigningCertificate
function AddCertificateToStore(
  StoreName: String;
  CertificatePath: String;
  var ResultCode: Integer): Boolean;
var
  CertUtilPath: String;
  Arguments: String;
begin
  CertUtilPath := ExpandConstant('{sys}\certutil.exe');
  Arguments := Format('-f -addstore "%s" "%s"', [StoreName, CertificatePath]);
  Log(Format(
    'Adding the internal code-signing certificate to Local Machine %s.', [StoreName]));
  Result := Exec(CertUtilPath, Arguments, '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
end;

function InstallInternalSigningCertificate: String;
var
  CertificatePath: String;
  ResultCode: Integer;
begin
  Result := '';

  if CertificateTrustCompletedThisRun or
    not WizardIsTaskSelected('trustcertificate') then
    exit;

  WizardForm.StatusLabel.Caption :=
    'Trusting the AES Programmer Project code-signing certificate...';
  ExtractTemporaryFile('Superior-AES-Programmer-Code-Signing.cer');
  CertificatePath :=
    ExpandConstant('{tmp}\AES-Programmer-Troubleshooter-Code-Signing.cer');

  if not AddCertificateToStore('Root', CertificatePath, ResultCode) then
  begin
    Result :=
      'Windows could not start Certificate Services. The internal signing ' +
      'certificate was not installed, and setup has stopped.';
    exit;
  end;
  if ResultCode <> 0 then
  begin
    Result := Format(
      'Windows could not trust the internal signing certificate in Trusted ' +
      'Root Certification Authorities (exit code %d). Setup has stopped.', [ResultCode]);
    exit;
  end;

  if not AddCertificateToStore(
    'TrustedPublisher', CertificatePath, ResultCode) then
  begin
    Result :=
      'Windows could not start Certificate Services for Trusted Publishers. ' +
      'Setup has stopped.';
    exit;
  end;
  if ResultCode <> 0 then
  begin
    Result := Format(
      'Windows could not trust the internal signing certificate in Trusted ' +
      'Publishers (exit code %d). Setup has stopped.', [ResultCode]);
    exit;
  end;

  CertificateTrustCompletedThisRun := True;
  Log('The internal code-signing certificate is trusted for this computer.');
end;
#endif

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DriverPath: String;
  ResultCode: Integer;
begin
  Result := '';

#ifdef IncludeSigningCertificate
  Result := InstallInternalSigningCertificate;
  if Result <> '' then
    exit;
#endif

  if FtdiCompletedThisRun then
    exit;

  WizardForm.StatusLabel.Caption :=
    'Installing the verified FTDI VCP driver quietly before AES Programmer & Troubleshooter...';
  Log('Extracting the pinned, verified FTDI CDM 2.12.36.20 driver payload.');
  ExtractTemporaryFiles('FTDI-Driver\*');
  DriverPath := ExpandConstant('{tmp}\FTDI-Driver\dpinst-amd64.exe');

  Log('Launching the signed FTDI x64 DPInst package in unattended mode before application installation.');
  if not Exec(
    DriverPath,
    '/q /se /sa',
    ExpandConstant('{tmp}\FTDI-Driver'),
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result :=
      'Windows could not start the required FTDI VCP driver installer. ' +
      'AES Programmer & Troubleshooter has not been installed.';
    exit;
  end;

  { DPInst uses a DWORD bitfield, not conventional zero/nonzero success.
    Bit 31 means failure and bit 30 means a restart is required. }
  if ResultCode < 0 then
  begin
    Result := Format(
      'The required FTDI VCP driver package reported a failure ' +
      '(DPInst result %d). AES Programmer & Troubleshooter has not been installed.', [ResultCode]);
    exit;
  end;

  if (ResultCode and $40000000) <> 0 then
  begin
    NeedsRestart := True;
    Log(Format(
      'FTDI driver setup succeeded and requested a restart (DPInst result %d).', [ResultCode]));
  end;

  FtdiCompletedThisRun := True;
  Log(Format(
    'FTDI VCP driver setup completed successfully (DPInst result %d).', [ResultCode]));
end;
