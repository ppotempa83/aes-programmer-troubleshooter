# Windows installer and signing

The Inno Setup package installs the self-contained Windows application and runs
the verified FTDI CDM 2.12.36.20 x64 driver payload before installing the app.
No API key, credential file, passcode, private signing key, or subscriber cipher
is included in the installer or release ZIP.

## Build the installer

Requirements:

- Windows 10 or 11 x64
- .NET 8 SDK
- Inno Setup 6.3 or later
- the pinned FTDI dependency staged by `scripts/fetch-windows-dependencies.ps1`

Run:

```powershell
.\scripts\build-installer.ps1
```

The output is written under `artifacts/installer/` and a public release bundle is
written under `artifacts/release/`.

## Optional code signing

For public distribution, prefer a certificate from a trusted public code-signing
certificate authority. A self-signed certificate is useful only for controlled
testing because recipients must explicitly trust it.

To create a generic test certificate:

```powershell
.\scripts\create-self-signed-certificate.ps1
```

Then pass its thumbprint and exported public `.cer` file to
`scripts/build-installer.ps1`. Never put a private key or PFX in the repository or
release package.

## Geoapify

The installer does not include or import a Geoapify key. Each user should:

1. Open <https://myprojects.geoapify.com/>.
2. Register or sign in.
3. Create a project.
4. Open **API Keys**.
5. Copy the generated key and paste it into the app when Site Survey or New Radio
   Check requests it.

The Windows app keeps the value only for the current process and excludes it from
logs and exports.

## FTDI safety

The Windows package retains the original vendor installer for provenance and runs
the verified signed x64 DPInst payload quietly. This does not replace physical
cable validation: AES J1 pin 6 carries +12 V and must remain disconnected.
