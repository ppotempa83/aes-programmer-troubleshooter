# Superior AES Android companion guidance

This folder is an isolated Android companion. It must not change the Windows
solution, WPF application, public simulation-only web demo, or their identities.

## Invariants

- Simulation is the default and only enabled transport until the Android USB path
  passes physical validation with known-good AES hardware.
- AES J1 pin 6 carries +12 V and must remain physically isolated.
- Use a genuine USB-to-RS-232 adapter. A bare USB-to-TTL cable is not equivalent.
- Do not commit API keys, subscriber ciphers, signing keys, JAR/AAR files, APK/AAB
  files, dependencies, caches, or other generated output.
- The credential template contains placeholders only.
- FTDI Java D2XX may be integrated only from an official, license-reviewed vendor
  package and only for genuine FTDI-based hardware.
- Every terminal entry must use `[MM-DD-YYYY / HH:MM AM/PM]` formatting.
- API keys and subscriber cipher values must never appear in logs or exports.
- Physical-radio programming and transmission remain disabled until the Android
  bench-validation checklist is completed.

## Validation

Source-only validation, which does not require the Android toolchain:

```powershell
.\scripts\validate-source.ps1
```

Once the documented .NET MAUI and Android toolchain is installed:

```powershell
dotnet build .\src\SuperiorAes.Android\SuperiorAes.Android.csproj `
  -f net10.0-android -c Debug
```

