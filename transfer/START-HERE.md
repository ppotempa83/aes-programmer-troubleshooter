# AES Programmer & Troubleshooter — Codex Transfer

Use this folder to continue the project in Codex on another computer.

## Fastest import method

1. Copy the entire `SFS AES App` project folder to the other computer.
2. Open that folder as the Codex workspace.
3. Attach or paste `transfer/IMPORT-PROMPT.md` into a new Codex task.
4. Tell Codex to read the files named in that prompt before making changes.

Codex does not need the original task database to continue accurately. This
bundle contains the project decisions and history that materially affect future
work.

## Current deliverables

- Windows desktop application: version `0.2.0`
- Windows package:
  `artifacts/Superior-AES-Programmer-v0.2.0-win-x64.zip`
- Windows package SHA-256:
  `BD38E95551DF99F95307597CB0F85762E5E1E05E07B654AC06E4DDF858DCB449`
- Public simulation-only demo: retain the existing Sites project and URL.
- Web source: `web-demo`
- Current web commit:
  `3ea558c2beb8c0c38309f4e6d47041cfe729ed59`
- Sites project ID:
  `appgprj_6a64214aac088191bd4aa80a69b98993`
- Current saved Sites version:
  `appgprj_6a64214aac088191bd4aa80a69b98993~appgver_303c638c41008191a80a43a3dc49eb5d`

The public URL must remain unchanged when updating the hosted demo.

## What is implemented

### Windows application

- AES 7744F and 7788F workflows.
- Serial and simulated connections.
- Standard session: 4800 baud, 8 data bits, no parity, 1 stop bit, flow
  control off.
- Programming, timers, zones, restorals, repeating, trouble reporting, TTL,
  local status, routing table, RF monitoring, zone status, text messaging, and
  raw terminal.
- Safety-gated transmitter key and RAM-reset operations.
- Guided troubleshooting and HTML field reports.
- Terminal font increased from 13 points to 16 points.
- Reusable programming templates; cipher values are never stored in templates.
- AES antenna dropdowns and real AES manufacturer product images.
- Four-radio virtual mesh simulator with editable NETCON, link, Q, online
  status, repeating, ACKs, and drops.
- Emergency24 AES map context in troubleshooting.
- New Radio Check using optional Google geocoding, elevation, and Solar
  Building Insights information plus Emergency24 coverage context.
- Three uploaded technician guides readable and searchable inside the app, with
  the original PDFs bundled.

### Public web demonstration

- Looks like the full Windows application.
- Opens connected to a simulated AES 7788F on `SIM-USB-RJ12`.
- Simulates connect, disconnect, subscriber handshake, terminal commands,
  programming, status, routes, RF monitoring, troubleshooting, site survey,
  radio planning, mesh traffic, training, and report generation.
- Eleven workspace sections matching the desktop navigation.
- Responsive phone and desktop layouts.
- Mobile workspace selector, 44-pixel touch controls, safe-area support,
  scrollable tables, and single-column phone layouts.
- Verified at 390×844 and 1440×900 with no page-level horizontal overflow.
- No real serial access, cipher entry, RF transmission, or live map/API calls.

## Safety and engineering decisions

- AES J1 pin 6 carries +12 V and must not be connected to the USB/serial
  interface.
- Use a true USB-to-RS-232 interface, not an unbuffered TTL cable.
- Hardware RF keying and destructive reset actions require explicit
  confirmations.
- The Windows app is an unsigned engineering build and may trigger Windows or
  antivirus reputation warnings.
- Physical bench validation against known-good 7744F and 7788F units is still
  required before field deployment.
- A Google API key is optional, entered at runtime, and must never be saved or
  logged.
- The web app is intentionally simulation-only.
- Real AES manufacturer images are used. Do not replace them with generated
  device or antenna artwork.
- Do not expose short-lived Sites repository credentials or access tokens.

## Important documentation

- `README.md`
- `docs/CABLE.md`
- `docs/PROTOCOL.md`
- `docs/BENCH-VALIDATION.md`
- `docs/IMAGE-SOURCES.md`
- `transfer/ESSENTIAL-CHAT-HISTORY.md`
- `transfer/THREAD-METADATA.json`

## Build and validation

Windows:

```powershell
.\scripts\build.ps1 -Configuration Release
.\scripts\publish.ps1
```

The latest completed Windows validation was:

- Release build: zero warnings and zero errors.
- Automated tests: 16/16 passed.
- Native UI simulation smoke tests: passed.
- Published executable startup: passed.

Web:

```powershell
cd web-demo
npm ci
npm run lint
npm test
```

The latest web validation was:

- Lint: passed.
- Build: passed.
- Hosted tests: 2/2 passed.
- Desktop and mobile browser interaction checks: passed.

## Relevant original task

- Codex task ID: `019f963f-b24a-7e90-aa50-dbdc476c4441`
- Original title: `Recall AES USB app discussions`
- Original workspace: redacted from the public transfer bundle.

Temporary credentials, private access tokens, and tool transcripts containing
them are deliberately not included in this export.
