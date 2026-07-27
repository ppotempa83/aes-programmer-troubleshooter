# Android architecture

The Android fork remains isolated under `android-app/`, but now references the
platform-neutral command, parser, diagnostics, simulation, site-analysis,
template, and reporting code in `SuperiorAes.Core`.

```text
AppShell and full technician pages
                │
        ICompanionSession
   logging · parsing · exports
      one operation sequence gate
                │
       IAesTransportSelector
           ╱             ╲
SimulatedAesTransport   FtdiUsbTransport
  safe default          explicit bench mode
                       Android USB Host/OTG
```

The Windows `SerialAesConnection` is never instantiated by Android. Windows uses
`System.IO.Ports` and the VCP driver; Android uses its platform USB Host APIs
directly.

## Transport selection and sequencing

`SelectableAesTransport` starts in Simulation mode. Hardware bench mode can be
selected only after the UI records acceptance of the J1 pin 6 / genuine
USB-to-RS-232 warning.

`CompanionSession` owns one asynchronous operation gate. It covers:

- transport selection;
- connect and disconnect;
- individual AES command, raw-line, ENTER, and ESC sends;
- each complete guided command plus prompt-response conversation.

This prevents a disconnect or mode switch during a programming response delay
and ensures one sequence cannot begin on hardware and finish in simulation.

## Shared protocol and evidence

The Android session uses:

- `AesCommands.GetBytes` for the exact command bytes;
- `AesParsers` for local status, routes, and zone replies;
- `DiagnosticEngine` for field findings;
- `SessionExportService` for TXT/XLSX;
- `TroubleshootingReportGenerator` for focused route/path HTML;
- `HtmlReportGenerator` for the printable field report;
- `ProgrammingTemplateStore`, `VirtualMeshSimulator`,
  `GeoapifySiteDataService`, `Emergency24CoverageService`, and
  `RadioRecommendationEngine` for their matching pages.

## Logging and secret boundary

The session owns the ID, start time, selected transport/model, subscriber
metadata, parsed evidence, surveys, latest mapped coverage/building/
recommendation evidence, and activity/terminal entries. Each
individual line is formatted:

```text
[MM-DD-YYYY / HH:MM (AM/PM)] CHANNEL: message
```

Multi-line RX/TX/app output is split into separately timestamped entries. Cipher
values and runtime/imported API credentials are registered as sensitive before
transmission or use, redacted from echoed text, and excluded from exports.

`CredentialMigrationService` accepts a separately distributed file through the
Android picker. Managed provisioning can alternatively stage
`credentials.local.txt` in the app-private data directory; startup registers
each populated value as sensitive, migrates it to Android Secure Storage, and
removes only that private plaintext staging copy. The packaged template contains
placeholders only.

## Packaged documents and imagery

The Android project links the original readable training documents and documented
manufacturer imagery from the Windows asset tree. At runtime, PDFs are copied to
app-owned storage only when the user opens/shares them. Images are loaded as
package assets; expanded views expose a copyable official AES product URL.

## Validation boundary

The USB implementation and complete UI are present, but availability is not the
same as field approval. Simulation remains the default, and retained physical
results against known-good 7744F and 7788F hardware are mandatory before field
deployment.
