# Superior AES Programmer

Windows field programming and diagnostic utility for legacy AES IntelliNet 7744F and
7788F subscriber units.

Version 0.2 replaces terminal key combinations with a technician workspace while
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
- Controlled site-survey comparisons
- Reusable JSON programming templates (the system cipher is never stored)
- Manufacturer-published device and antenna thumbnails
- Antenna catalog drop-downs for the AES 7214, 7211, and 7210 families
- Four-radio virtual mesh with editable NETCON, link layer, Q, online state, TX/RX,
  repeating, acknowledgements, and dropped signals
- Emergency24 public AES coverage evidence in the Troubleshooter
- New Radio Check with Google geocoding, elevation, Solar Building Insights, all
  four Emergency24 antenna layers, and antenna/location planning guidance
- In-app searchable reader for the three uploaded AES technician training guides
- Forward/reflected power and keyed-voltage recording
- Printable HTML field reports and raw session logs

## Safety and deployment status

This is a field engineering build. Bench-validate direct programming commands with
a known-good 7744F and 7788F before field deployment.

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

The published folder contains `SuperiorAes.Programmer.exe`. Simulation is selected
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

Use **Training guides** to search within a guide or open the original PDF for exact
page layout and diagrams.

## New Radio Check setup

Emergency24 coverage layers work from latitude/longitude without an API key.
Address geocoding, elevation, and imagery-derived roof data require a Google Maps
Platform key with these APIs enabled:

- Geocoding API
- Elevation API
- Solar API

Enter the key for a single lookup, or set `GOOGLE_MAPS_API_KEY` before launching.
The app never saves or logs the key. Google Solar Building Insights is
imagery-derived planning data, not a structural survey.

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

## Architecture

- `src/SuperiorAes.Core` — protocol bytes, serial and simulated transports, parsers,
  diagnostic rules, map/site analysis, virtual mesh, templates, and report generation
- `src/SuperiorAes.App` — WPF technician interface
- `tests/SuperiorAes.Core.Tests` — parser, diagnostic, and simulator tests

## Primary references

- [AES 7788F Installation and Operation Manual, Rev. 6](https://aes-corp.com/wp-content/uploads/2020/07/40-7788-Rev-6.pdf)
- [AES 7744F Installation and Operation Manual, Rev. 6](https://aes-corp.com/wp-content/uploads/2020/07/7744-Install-Manual.pdf)
- [AES Antenna Matrix](https://aes-corp.com/wp-content/uploads/2022/09/AES_Antenna-Matrix-Buying-Guide_Rev5.pdf)
- [Emergency24 AES Radio Network Map](https://www.emergency24.com/maps/)
- [Google Maps Platform Geocoding](https://developers.google.com/maps/documentation/geocoding/overview)
- [Google Maps Platform Elevation](https://developers.google.com/maps/documentation/elevation/overview)
- [Google Solar Building Insights](https://developers.google.com/maps/documentation/solar/building-insights)

AES, AES-IntelliNet, 7744F, and 7788F are identifiers of AES Corporation. This
application is a Superior Fire & Security engineering tool and is not represented as
an AES Corporation product. Hardware photographs are real AES manufacturer-published
images; see [image sources](docs/IMAGE-SOURCES.md).
