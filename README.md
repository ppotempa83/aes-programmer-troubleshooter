# AES Programmer & Troubleshooter

Windows field programming and diagnostic utility for legacy AES IntelliNet 7744F and
7788F subscriber units.

Version 0.5 replaces terminal key combinations with a technician workspace while
retaining the official legacy PC command set. It includes separate hardware and
virtual-mesh simulation paths, so training and workflow review do not require a
connected radio.

## Included

- Automatic COM-port discovery
- 4800 baud, 8 data bits, no parity, one stop bit, flow control off
- ID and cipher programming
- Check-in, AC-report, and normal reporting timers
- Zone and restoral programming
- Repeating and trouble-report suppression modes
- RAM reset with typed confirmation
- Local status, full routing table, and zone status
- RX Monitor, TX Monitor, and Monitor All
- Five-second transmitter key test with safety gates
- Time-to-Live, text-message, F1, and F3 interactive access
- Raw ASCII terminal
- STAT, NETCON, Q/N/L, route-diversity, and enrollment diagnosis
- Troubleshooting-only HTML export with the complete routing table, visualized
  alternate RF paths, associated-radio L/N/Q values, and prioritized recommendations
- Controlled site-survey comparisons
- Reusable JSON programming templates (the system cipher is never stored)
- Contact ID / dialer-capture workspace with recommended 7794 IntelliPro Fire
  setup, full interactive J2 controls, configurable template values, legacy
  7067 IntelliTap II guidance, and a 7794A compatibility warning
- Four-second generic AES splash and a red rounded AES application icon
- Manufacturer-published device and antenna thumbnails
- Click-to-enlarge official hardware images with copyable AES product links
- Antenna catalog drop-downs for the AES 7214, 7211, and 7210 families
- Four-radio virtual mesh with editable NETCON, link layer, Q, online state, TX/RX,
  repeating, acknowledgements, and dropped signals
- Emergency24 public AES coverage evidence in the Troubleshooter
- New Radio Check with Geoapify geocoding, terrain elevation, an in-app static
  map, all four Emergency24 antenna layers, and antenna/location planning guidance
- In-app searchable reader for the complete training library and original AES
  7794, 7794A, and historical 7067 manuals
- Forward/reflected power and keyed-voltage recording
- Printable HTML field reports plus paired text and Excel session exports

## Safety and deployment status

This is a field engineering build. Bench-validate direct programming commands with
a known-good 7744F and 7788F before field deployment.

- The Android USB transport is a hardware-bench implementation, not a
  field-validation claim. Simulation remains its startup default.
- Put the account on test before programming or transmitting.
- Use a proper USB-to-**RS-232** interface. Do not use an unbuffered FTDI-TTL cable.
- Keep J1 pin 6 (+12 V) physically isolated.
- Connect the proper antenna, 50-ohm dummy load, or approved RF instrument before
  using the transmitter test.
- `Reset RAM` is destructive to timers, zones, restorals, and operating modes. It is
  not the physical board RESET function.
- After zone-programming changes, use the physical RESET button on the subscriber
  board.
- Confirm alarm, trouble, supervisory, and restoral signals at the central station.

See [Cable and J1 interface](docs/CABLE.md), [protocol command map](docs/PROTOCOL.md),
and [bench-validation plan](docs/BENCH-VALIDATION.md).

## Run

The published folder contains `AesProgrammer.Troubleshooter.exe`. Simulation is selected
by default:

1. Start the application.
2. Leave **Simulation** selected.
3. Select 7744F or 7788F.
4. Click **Connect**.
5. Read status, routes, and zones, or run the Troubleshooter baseline.

For a physical radio, clear Simulation, select the assigned COM port, and connect the
verified programming cable.

## Training library

The published application includes searchable in-app text plus each original PDF:

- AES 7744F/7788F Complete Technician Guide
- AES 7744F/7788F NETCON, Signal Survey and Antenna Guide
- AES 7744F/7788F US232R Wiring and Commands
- AES Contact ID — IntelliPro + IntelliTap Field Guide
- AES 7794 IntelliPro Fire Installation Manual and Quick Start Guide
- AES 7067 IntelliTap II historical original manual
- AES 7794A IntelliPro 2.0 original manual (reference for compatible 2.0 radios)

Use **Training guides** to search within a guide or open the original PDF for exact
page layout and diagrams.

## New Radio Check setup

Emergency24 coverage layers work from latitude/longitude without an API key.
Address geocoding, terrain elevation, and the in-app static map require a
Geoapify key with these APIs enabled:

- Forward Geocoding API
- Elevation API
- Static Maps API

Register or sign in at [Geoapify MyProjects](https://myprojects.geoapify.com/),
create a project, open **API Keys**, and copy the automatically generated key.
Paste it into the in-app prompt for the current session. No key or credential file
is bundled with the application or installer, and the app never logs or exports
the entered value. Geoapify terrain data is planning evidence,
not a structural survey; roof height and construction remain field observations.
When no runtime key is loaded, entering Site Survey or New Radio Check prompts
for one, and a manually entered value remains in memory only for that app session.

## Session audit exports

Every terminal entry is shown and recorded with a
`[MM-DD-YYYY / HH:MM (AM or PM)]` timestamp. The session export creates a complete text
terminal dump and a companion `.xlsx` workbook with subscriber, model, computer,
computer user, connection, session-start, and export metadata. System ciphers and
API keys are excluded or redacted.

## Build

Requirements:

- Windows 10 or Windows 11
- .NET 8 SDK

From PowerShell:

```powershell
.\scripts\build.ps1
```

Publish a self-contained Windows package:

```powershell
.\scripts\publish.ps1
```

Build the Android APK after installing the .NET 10 MAUI Android workload,
compatible Microsoft OpenJDK, and Android SDK:

```powershell
cd .\android-app
.\scripts\validate-source.ps1
dotnet build .\src\SuperiorAes.Android\SuperiorAes.Android.csproj `
  -f net10.0-android -c Debug
```

The full .NET MAUI Android fork is under `android-app/`. It shares the desktop
command, parser, diagnostics, template, site-analysis, simulation, and reporting
core. Android starts in Simulation and exposes an explicit, unvalidated FTDI USB
bench mode for the allowlisted `0403:6001` FT232-family USB-to-RS-232 cable.
Android uses USB Host/OTG directly; it does not install the Windows VCP driver.
See [`android-app/README.md`](android-app/README.md).

Windows installer, FTDI staging, and certificate instructions are in
[`docs/WINDOWS-INSTALLER-AND-SIGNING.md`](docs/WINDOWS-INSTALLER-AND-SIGNING.md).

## Architecture

- `src/SuperiorAes.Core` — platform-neutral protocol bytes, simulated transport,
  parsers, diagnostic rules, map/site analysis, virtual mesh, templates, and
  report generation
- `src/SuperiorAes.App` — WPF technician interface and Windows
  `System.IO.Ports`/VCP transport
- `tests/SuperiorAes.Core.Tests` — parser, diagnostic, and simulator tests
- `android-app` — full .NET MAUI Android fork with simulation-default and explicit
  FTDI USB hardware-bench transports
- `installer/windows` — Inno Setup Windows installer source

## Primary references

- [AES 7788F Installation and Operation Manual, Rev. 6](https://aes-corp.com/wp-content/uploads/2020/07/40-7788-Rev-6.pdf)
- [AES 7744F Installation and Operation Manual, Rev. 6](https://aes-corp.com/wp-content/uploads/2020/07/7744-Install-Manual.pdf)
- [AES Antenna Matrix](https://aes-corp.com/wp-content/uploads/2022/09/AES_Antenna-Matrix-Buying-Guide_Rev5.pdf)
- [AES 7794 IntelliPro Fire](https://aes-corp.com/product/7794-subscriber-add-on-module/)
- [AES discontinued-product replacements](https://aes-corp.com/products/discontinued-products/)
- [Emergency24 AES Radio Network Map](https://www.emergency24.com/maps/)
- [Geoapify Forward Geocoding](https://apidocs.geoapify.com/docs/geocoding/forward-geocoding/)
- [Geoapify Elevation](https://apidocs.geoapify.com/docs/elevation/)
- [Geoapify Static Maps](https://apidocs.geoapify.com/docs/maps/static/)

AES, AES-IntelliNet, 7744F, and 7788F are identifiers of AES Corporation. This
independent application is not represented as an AES Corporation product. Hardware photographs are real AES manufacturer-published
images; see [image sources](docs/IMAGE-SOURCES.md).
