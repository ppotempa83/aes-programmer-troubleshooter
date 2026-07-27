# Windows installer, offline dependencies, and signing

The Windows release is a self-contained x64 application packaged with Inno
Setup. It does not require a separate .NET runtime installation and it never
downloads dependencies during setup.

## What the installer contains

- The complete self-contained `win-x64` publish.
- The searchable training text and original readable training PDFs.
- The Superior Fire shield and the red AES Windows icon.
- `config\credentials.template.txt`, containing placeholders only.
- The editable external `credentials.local.txt` deployment file.
- The pinned, unmodified, vendor-signed FTDI CDM 2.12.36.20 setup executable.
- For internal signed releases, the public-only Superior Fire code-signing
  certificate and its instructions. The private key is never included.

The application is installed under Program Files. The installer creates a Start
menu shortcut and offers an optional desktop shortcut. Setup imports an adjacent
`credentials.local.txt` without logging its content. For signed internal
releases, its default-on Certificate Trust task quietly imports the public
certificate into Local Machine Trusted Root and Trusted Publishers. Setup then
runs the verified FTDI x64 DPInst payload quietly and waits for success before
installing any application files. A DPInst failure bit stops app installation;
its restart-required bit is passed back to setup.

The first launch of a self-signed setup can still show an Unknown Publisher
Windows prompt: Windows cannot trust code based on a certificate it has not yet
been permitted to install. After that approval and the default certificate task,
later releases signed with the same identity can validate normally.

## Prerequisites for the release workstation

1. Windows 10 or Windows 11 on an x64 workstation.
2. The .NET 8 SDK.
3. [Inno Setup 6.3 or later](https://jrsoftware.org/isinfo.php), including
   `ISCC.exe`.
4. For signed releases, the Windows SDK Signing Tools feature and an approved
   code-signing certificate whose private key is available through the Windows
   certificate store.
5. 7-Zip when initially staging the supplied FTDI self-extracting package.

The build scripts do not install these tools.

The installed Inno Setup compiler displays a non-commercial-use banner. Its
publisher says a commercial license is not strictly required, but requests one
for commercial organizations, including in-house deployments. This local build
is suitable for evaluation; obtain the appropriate
[Inno Setup commercial license](https://jrsoftware.org/isorder.php) before the
technician package is treated as a production Superior Fire release.

## Build an unsigned installer

From the repository root:

```powershell
.\scripts\build-installer.ps1
```

The script runs the Release build and automated tests, creates the self-contained
publish, validates any staged FTDI dependency, and compiles the installer. Output
is written to:

```text
artifacts\installer\Superior-AES-Programmer-v<VERSION>-win-x64-setup.exe
artifacts\installer\Superior-AES-Programmer-v<VERSION>-win-x64-setup.exe.sha256
artifacts\release\Superior-AES-Programmer-v<VERSION>-win-x64.zip
artifacts\release\Superior-AES-Programmer-v<VERSION>-win-x64.zip.sha256
```

All generated release artifacts are intentionally ignored by
Git. `-SkipValidation` and `-SkipPublish` are available only for a repeat local
compile after the same publish has already passed validation.

## Required pinned FTDI VCP installer

This release is pinned to the supplied `CDM2123620_Setup.exe`, version
2.12.36.20, for Windows 10/11 x86 and x64:

```text
SHA-256:
6B4985913C5F8B0B9656E8BAA6F1193B2B7EF3385F67DDC09FBA83A670B5444D

FTDI signer-certificate thumbprint:
69B945B393E94C281BF255F09F569F91D3E25E07
```

The original file must come from the
[official FTDI VCP driver page](https://ftdichip.com/drivers/vcp-drivers/).
Do not use a driver from a file-sharing site, email attachment, or repackaged
download.

This repository intentionally has no automatic dependency downloader. To stage
the vendor file without modifying it:

```powershell
$driverPath = 'C:\ApprovedDownloads\CDM-vendor-setup.exe'
$driverHash = '6B4985913C5F8B0B9656E8BAA6F1193B2B7EF3385F67DDC09FBA83A670B5444D'

.\scripts\fetch-windows-dependencies.ps1 `
    -FtdiInstallerPath $driverPath `
    -ExpectedSha256 $driverHash
```

Before accepting and again before building, the scripts require:

- An exact SHA-256 match with the release operator's recorded value.
- A valid Authenticode signature whose signer identifies FTDI or Future
  Technology Devices.
- The exact pinned SHA-256 and signer thumbprint printed above.
- An unchanged byte-for-byte copy into
  `artifacts\windows-dependencies\ftdi\FTDI-CDM-VCP-Setup.exe`.
- An exact 28-file payload allowlist extracted without running the wrapper.
- Pinned DPInst/catalog hashes, valid FTDI DPInst signatures, valid Microsoft
  WHQL catalog signatures, and valid signed driver binaries.

For stronger release control, have a second person independently confirm the
SHA-256 after downloading from FTDI's HTTPS page. The staging copy,
checksum, and signature metadata remain under ignored `artifacts`; none are
committed.

To recheck a staged installer:

```powershell
.\scripts\fetch-windows-dependencies.ps1 -VerifyStaged
```

FTDI states that its VCP driver may be redistributed unmodified for use with
FTDI-based products. Confirm the actual programming interface uses a genuine
supported FTDI device before bundling it. The FTDI Windows setup executable is
not an ARM64 installer. Its outer self-extractor has no reliable unattended
switch, so setup runs the unchanged signed x64 `dpinst-amd64.exe` payload with
`/q /se /sa` instead. The original vendor EXE is also retained under the
installed app's `Dependencies\FTDI` folder for provenance/manual repair.

## Editable pre-distribution credential file

The release folder and ZIP contain this external file beside setup:

```text
credentials.local.txt
```

It is deliberately outside the signed installer so the release owner can edit
it before distributing the package without invalidating the setup signature.
Use UTF-8 plaintext, one `VariableName=Value` entry per line, and `#` for
comments. The currently implemented credential variable is:

```text
GeoapifyApiKey=***
```

Replace `***` before distribution. Unknown variable names are ignored until an
app feature explicitly implements them. Do not add subscriber ciphers, panel
programming codes, or site-specific radio passcodes.

The release ZIP must be extracted before setup is launched so the adjacent file
is visible. The default-on Deployment Credentials setup task copies it, without
logging its content, to:

```text
%ProgramData%\SuperiorFire\AES Programmer\credentials.local.txt
```

The app reads `GeoapifyApiKey` from that machine-wide file when present. If the
adjacent file is missing, setup creates a placeholder-only fallback. The
application also checks `config\credentials.local.txt` beside the executable,
and the environment variable `GEOAPIFY_API_KEY` remains supported. Placeholder
values are ignored.

Never put a live key in the template, installer source, repository, build logs,
or technician documentation. The editable deployment copy and any personalized
ZIP must stay under ignored `artifacts` or another approved secure release
location; never commit it.

A desktop program cannot make a plaintext API key unreadable to a person who can
run or administer that same computer: the application itself must be able to read
the value. Windows file permissions can limit casual access, but they cannot turn
an embedded or plaintext desktop credential into a secret from the program's
operator. Use a minimally scoped, quota-limited, monitored, and readily rotatable
Geoapify key. For stronger isolation, place API calls behind a company-controlled
service instead of distributing the provider key.

## Create the internal self-signed certificate

For the requested internal technician release, run:

```powershell
$certificate = .\scripts\create-self-signed-certificate.ps1
$certificate | Format-List
```

The script creates an RSA-3072, SHA-256 code-signing certificate in
`Cert:\CurrentUser\My` with a non-exportable software private key, then exports
only its public `.cer` under ignored `artifacts\signing`. It does not create a
PFX. A non-exportable software key is not equivalent to a hardware-backed key;
protect access to the signing workstation and user account.

Build with the returned values:

```powershell
.\scripts\build-installer.ps1 `
    -CertificateThumbprint $certificate.CertificateThumbprint `
    -PublicCertificatePath $certificate.PublicCertificate
```

To package a credential file you prepared outside the repository:

```powershell
.\scripts\build-installer.ps1 `
    -CertificateThumbprint $certificate.CertificateThumbprint `
    -PublicCertificatePath $certificate.PublicCertificate `
    -DeploymentCredentialSourcePath 'C:\SecureReleaseInput\credentials.local.txt'
```

If that parameter is omitted, the release contains the documented placeholder
format for you to edit after extracting it.

The versioned release ZIP contains the signed setup, checksums, release manifest,
editable `credentials.local.txt`, public `.cer`, and certificate-first
instructions. Technicians can either run setup and leave the default Certificate
Trust task checked, or manually install the adjacent `.cer` into Local Machine
Trusted Root Certification Authorities and Trusted Publishers first.

Confirm the certificate thumbprint through a separate trusted company channel.
Anyone able to replace both a setup file and its adjacent certificate could
otherwise substitute a different signed pair. Remove an obsolete certificate
from both trust stores after an announced rotation or compromise.

## Publicly trusted production signing

For broader distribution, replace the internal identity with either:

- An organization-validated code-signing certificate from a publicly trusted
  certificate authority, preferably with a hardware-backed or managed private
  key; or
- Microsoft's managed
  [Artifact Signing service](https://learn.microsoft.com/en-us/azure/artifact-signing/overview)
  if the release process is later moved to a supported CI workflow.

Follow the certificate provider's organization-validation and key-enrollment
steps. Do not commit a PFX, password, token, private key, or cloud signing
credential. The included signing script deliberately supports a certificate in
the Windows `CurrentUser\My` or `LocalMachine\My` store and does not accept a PFX
password on the command line.

List suitable local certificates:

```powershell
Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My |
    Where-Object {
        $_.HasPrivateKey -and
        $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
    } |
    Select-Object Subject, Thumbprint, NotAfter
```

## Build and sign in the correct order

Provide the approved certificate thumbprint:

```powershell
.\scripts\build-installer.ps1 `
    -CertificateThumbprint 'YOUR_CERTIFICATE_THUMBPRINT'
```

The script performs the required order:

1. Validate and publish the application.
2. Sign and RFC 3161 timestamp the published application executable using
   SHA-256.
3. Build the installer around the signed application.
4. Sign and timestamp the final setup executable.
5. Verify both Authenticode signatures and write the final SHA-256 file.
6. Create the exact, versioned technician release ZIP.

To sign or re-verify an individual release file separately:

```powershell
.\scripts\sign-release.ps1 `
    -Path 'C:\ApprovedRelease\Superior-AES-Programmer-setup.exe' `
    -CertificateThumbprint 'YOUR_CERTIFICATE_THUMBPRINT'
```

The script uses Microsoft's `SignTool.exe`, verifies the certificate has an
accessible private key and code-signing usage, timestamps the signature, and
runs the default Authenticode verification policy. See Microsoft's
[SignTool documentation](https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe)
and [Authenticode timestamp guidance](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures).

Signing establishes publisher identity and detects post-signing changes; it does
not guarantee that a new download will immediately avoid Microsoft Defender
SmartScreen warnings. Microsoft currently explains that both OV and EV signed
applications may show an unrecognized-app warning while reputation accumulates,
and EV no longer bypasses that process. Use one consistent signing identity and
do not modify files after signing. See
[Microsoft's SmartScreen reputation guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Release checklist

- [ ] `dotnet test SuperiorAes.sln -c Release` passes.
- [ ] The published EXE opens in Simulation mode.
- [ ] Training PDFs and searchable text open from the installed application.
- [ ] No live key is present in source, logs, or the default release; no
      subscriber cipher, private key, PFX, or password is distributed.
- [ ] Only the intentional public `.cer` is included.
- [ ] The pinned FTDI dependency passes SHA-256, signer-thumbprint, and
      Authenticode verification.
- [ ] The FTDI driver completes before application files are installed.
- [ ] The editable adjacent credential file imports without logging its content.
- [ ] Automatic and manual certificate-trust paths are both tested.
- [ ] The application EXE is signed before installer compilation.
- [ ] The final installer is signed, timestamped, and verified.
- [ ] The release SHA-256 is recorded with the release.
- [ ] Installation and uninstall are tested on clean Windows 10 and Windows 11
      x64 virtual machines.
- [ ] Physical bench validation with known-good 7744F and 7788F units is complete
      before field deployment.
