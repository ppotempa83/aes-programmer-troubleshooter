# AES Programmer & Troubleshooter for Android

Android .NET MAUI fork of the Windows AES Programmer & Troubleshooter. It provides the same
technician feature surface as the Windows application while retaining an explicit
safety boundary around unvalidated Android hardware use.

## Current status

- **Simulation is selected at every startup.**
- USB attachment does not auto-launch the app or request permission during
  startup. Permission is requested only after selecting FTDI hardware mode and
  tapping Connect.
- An explicit **FTDI USB hardware bench** mode is available for a directly
  attached FT232-class adapter with VID/PID `0403:6001`.
- The Android USB path is **not field-validated**. It must pass
  [the physical checklist](docs/PHYSICAL-VALIDATION.md) with known-good 7744F and
  7788F units before any field deployment.
- APKs, signing material, build output, dependencies, and caches remain ignored
  and are never committed.

## Full feature surface

- Live/simulated connection dashboard with an explicit transport selector.
- Complete AES command catalog backed by the shared `SuperiorAes.Core`
  command bytes, explanations, formats, examples, guided prompts, and
  safety confirmations.
- Fully editable programming templates with JSON import/export; cipher is not
  part of the template schema.
- Live timestamped terminal, status, routing, zones, RF monitors, and guided
  diagnostics.
- Session-level activity logging with exact
  `[MM-DD-YYYY / HH:MM (AM/PM)]` timestamps.
- Actual session TXT/XLSX exports, printable field HTML, and a focused
  troubleshooting HTML with routing-path visualization and associated L/N/Q
  values.
- Measured site-survey trials, Geoapify address/elevation/static-map lookup,
  Emergency24 public AES layer comparison, and recommendation output.
- Editable one-to-four-radio virtual mesh simulator with per-radio model choice
  and a visualized direct/repeated path.
- Contact ID section for live 7794 IntelliPro Fire J2 controls, a manual-derived
  7067 IntelliTap II physical jumper/programming wizard, and the separate 7794A
  IntelliNet 2.0 compatibility boundary.
- Packaged readable training PDFs/text and manufacturer-image gallery with
  expanded views, copyable official AES URLs, and share/save controls.
- 3.5-second generic AES startup screen and consistent navy/red field-tool theme.
- Runtime Geoapify setup using a user-created key stored in Android Secure Storage.

## Android FTDI transport

Android does not load the Windows FTDI VCP driver. The app uses Android USB
Host/OTG APIs directly and therefore does not bundle a vendor JAR, AAR, or native
library. The current allowlist is deliberately restricted to FTDI VID/PID
`0403:6001`.

The port is configured for:

- 4800 baud, 8 data bits, no parity, 1 stop bit;
- flow control off;
- DTR asserted and RTS cleared;
- ASCII with carriage-return interactive line endings.

Switching transport, connecting/disconnecting, sending a command, and each full
guided programming conversation are serialized by one session operation gate so
a sequence cannot split between simulation and hardware.

## Build

Prerequisites:

1. .NET 10 SDK.
2. `.NET MAUI Android` workload.
3. Microsoft OpenJDK 17 (or the compatible JDK installed by
   `InstallAndroidDependencies`).
4. Android SDK platform/build tools and platform-tools.

Then run:

```powershell
.\scripts\validate-source.ps1
.\scripts\build.ps1
```

or:

```powershell
dotnet build .\src\SuperiorAes.Android\SuperiorAes.Android.csproj `
  -f net10.0-android -c Debug
```

Build output is local-only and ignored by Git. See
[Android signing](docs/ANDROID-SIGNING.md) for a protected release-keystore
workflow.

## Geoapify key

No API key or credential file is packaged with the APK. Register or sign in at
[Geoapify MyProjects](https://myprojects.geoapify.com/), create a project, open
**API Keys**, and copy the generated key. The Site/New Radio page prompts for it
and can store the user-supplied value in Android Secure Storage. Values are
excluded from terminal logs and every report/export.

Any key used by a client application can ultimately be recovered by a determined
device owner. Use a server-side proxy when the key must remain secret from device
users.

## Safety

- AES J1 pin 6 carries +12 V and must remain disconnected.
- Use a genuine USB-to-RS-232 interface; a USB-to-TTL cable is not equivalent.
- Use a USB Host/OTG-capable Android device; a powered hub may be required.
- Simulation and automated tests do not replace retained bench results.
- Put the account on test and use the app's additional confirmations for RF key,
  cipher, programming, and RAM-reset operations.

## More detail

- [Architecture](docs/ARCHITECTURE.md)
- [FTDI Android USB transport](docs/FTDI-D2XX-INTEGRATION.md)
- [Android signing](docs/ANDROID-SIGNING.md)
- [Physical validation](docs/PHYSICAL-VALIDATION.md)
