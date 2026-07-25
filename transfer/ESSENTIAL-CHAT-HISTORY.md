# Essential Chat History

This is a portable, condensed transcript of the decisions that materially
shaped the project. Tool output and temporary credentials are omitted.

## Recovered earlier conversations

The original request was to recover conversations from DavidPC and other
devices concerning a direct USB-to-RJ12 connection for AES 7744F and 7788F
subscribers and an accompanying programmer application.

Relevant earlier conversation titles found in the synced history:

- `RJ11 Chat Recall`
- `AES IntelliNet 7788 Guide`
- `AES 7788 Programmer Guide`

The recovered direction was:

- Create a direct USB-to-6P6C RJ12 programming cable for AES J1.
- Avoid a DB9, breakout board, screw terminals, Tera Term, or a handheld
  programmer in the normal technician workflow.
- Use a real USB-to-RS-232 interface, not USB-to-TTL.
- Start with Windows and share protocol logic with possible later Android
  support.
- Keep J1 pin 6 (+12 V) physically isolated.

Recovered J1 wiring:

| AES J1 | Signal | RS-232 side |
|---|---|---|
| Pin 1 | GND | DB9 pin 5 |
| Pin 2 | AES TXD | DB9 pin 3 |
| Pin 3 | AES RXD | DB9 pin 2 |
| Pin 4 | DTR | DB9 pin 4 |
| Pin 5 | DSR | DB9 pin 6 |
| Pin 6 | +12 V | Do not connect |

RTS/CTS, DB9 pins 7 and 8, were to be internally jumpered. The documented
session is 4800 baud, 8-N-1, flow control off.

An earlier conversation had incorrectly claimed that a functional MVP ZIP
already existed. That was corrected: this workspace initially contained no
functional application, and the real project was built here.

## User request: build the app

> build it!

Outcome:

- Created a .NET 8 WPF desktop solution with a reusable core library.
- Implemented AES command definitions, parsers, serial connection, simulated
  connection, protocol client, diagnostics, reports, and tests.
- Implemented the Windows technician interface.
- Added safety confirmations for RF transmitter keying and RAM reset.
- Added documentation for cable construction, protocol use, and bench
  validation.
- Packaged the first self-contained Windows release.

## User request: expand the desktop app

> make the text in the terminal window a larger font by 3pts. add programming
> template creation. make sure the antenna options are drop downs. Add
> images/thumbnails of the devices, antennas etc. add a simulator section where
> you can add upto 4 simulated radios with customized netcon/link/quality etc to
> simulate a virtual mesh network, and simulate signal sending/reception. in the
> troubleshooter, add this AES map as a variable in troubleshooting
> https://www.emergency24.com/maps/. Also add a new radio check section, where
> you type in an address, it analyzes the building elevation and structure based
> on google maps and google earth data, compares to the aes map
> https://www.emergency24.com/maps/ and suggest a antenna type and location

Outcome:

- Terminal font changed from 13 to 16 points.
- Added reusable programming templates.
- Added AES antenna dropdowns.
- Added hardware gallery cards.
- Added a configurable four-radio virtual mesh with TX/RX/ACK/drop events.
- Added Emergency24 coverage context to troubleshooting.
- Added New Radio Check with optional Google geocoding, elevation, and Solar
  Building Insights data plus links to Google Maps/Earth and Emergency24.

## User request: use real images and create a shareable demo

> do not generate your own images, use real image data of 7744f 7788f and the
> antennas, when you are done with all updates and app is ready, create a web
> based demo i can share with people, but keep it simulation only.

Outcome:

- Used real AES manufacturer product images for 7744F/7788F and AES antennas.
- Documented image sources in `docs/IMAGE-SOURCES.md`.
- No generated device or antenna images are used in either application.
- Built and publicly deployed a simulation-only web demo.

## User request: include training documentation

> dont forget to add the training guides i uploaded into the app as readable
> inside the app training documentation

Outcome:

Three guides were bundled into the Windows app as original PDFs plus searchable
text:

- `AES-7744F-7788F-Complete-Technician-Guide.pdf`
- `AES-7744F-7788F-NETCON-Signal-Survey-and-Antenna-Guide.pdf`
- `AES-7744F-7788F-US232R-Wiring-and-Commands.pdf`

The training reader supports guide selection, searching, reading extracted
text, and opening the original PDF.

## Completed desktop version 0.2.0

The packaged Windows build is:

`artifacts/Superior-AES-Programmer-v0.2.0-win-x64.zip`

SHA-256:

`BD38E95551DF99F95307597CB0F85762E5E1E05E07B654AC06E4DDF858DCB449`

Latest completed validation:

- Build: zero warnings and errors.
- Tests: 16/16 passed.
- Native UI simulation checks: passed.
- Packaged executable startup: passed.

## User request: make the web demo look like the full app

> The web demo should look like the full fledged app and pretend it connected to
> a radio

Additional instruction:

> Make sure url remains the same

Outcome:

- Replaced the marketing-style demo with the full desktop application shell.
- Added all eleven technician sections:
  Dashboard, Program subscriber, Status & routes, RF monitor, Troubleshooter,
  Site survey, New radio check, Mesh simulator, Training guides, Field report,
  and Terminal.
- The web app opens connected to a virtual 7788F subscriber ID 7740 on
  `SIM-USB-RJ12`.
- Added simulated disconnect/reconnect and subscriber handshake behavior.
- Terminal commands such as STAT, ROUTES, and ZONES return simulated data.
- Kept prominent `SIMULATION ONLY` and `0 REAL TRANSMISSIONS` indicators.
- Retained the same URL:
  <https://superior-aes-field-simulator.ppotempa83.chatgpt.site>

## User request: make the web demo responsive

> its not mobile friendly. needs to adjust to desktop and mobile

Outcome:

- Added correct device-width viewport metadata and safe-area support.
- Added a dedicated mobile workspace dropdown.
- Added 44-pixel touch targets.
- Converted cards, forms, terminal, mesh, and training layouts to responsive
  single-column phone views.
- Made wide tables independently scrollable.
- Added layouts down to 360 pixels wide.
- Verified at 390×844 and 1440×900.
- Verified no page-level horizontal overflow at both sizes.
- Deployed over the same public URL.

## Current project invariants

- Do not change the public demo URL.
- The hosted app remains simulation-only.
- Do not add serial APIs or live external data calls to the public demo.
- Do not store cipher values or API keys.
- Do not connect AES J1 pin 6 to the cable.
- Use real manufacturer imagery.
- Preserve training documentation.
- Physical hardware bench validation is required before field use.
