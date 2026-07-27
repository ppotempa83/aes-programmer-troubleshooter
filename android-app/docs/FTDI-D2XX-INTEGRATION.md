# Android FTDI USB-host transport

The Android app implements a focused FTDI SIO transport directly with Android USB
Host APIs. It does **not** bundle FTDI Java D2XX, a JAR/AAR, native library, or
the Windows VCP installer.

## Supported device allowlist

| Vendor | Product | Typical device |
|---|---|---|
| `0403` | `6001` | FTDI FT232R/FT232-class USB serial adapter |

Other FTDI product IDs and non-FTDI adapters are rejected until they receive
their own electrical/protocol review and physical validation. A matching ID does
not prove a device is genuine.

## Required transport configuration

| Setting | Required value |
|---|---:|
| Baud | 4800 |
| Data bits | 8 |
| Parity | None |
| Stop bits | 1 |
| Flow control | Off |
| DTR | Asserted |
| RTS | Cleared |
| Encoding | ASCII |
| Interactive line terminator | Carriage return (`0x0D`) |
| 7794 menu ESC | Raw `0x1B`, no carriage return |

The implementation uses FTDI vendor control requests to reset/purge the device,
set 4800 8-N-1, disable flow control, set DTR/RTS, and set the latency timer. It
strips each FTDI two-byte modem-status header from bulk-IN packets before
forwarding ASCII to the session parser.

## Android lifecycle

- Hardware mode is explicit; Simulation is selected on startup.
- Android USB permission is requested for the attached allowlisted adapter.
- Permission denial leaves the session disconnected.
- A background bulk-IN loop forwards received data.
- Writes are serialized.
- USB detach cancels the reader and transitions to disconnected.
- Session-level operation serialization prevents transport changes during a
  guided programming conversation.

## Hardware warning

Android may power the USB bus. This does not change the AES cable rules:

- J1 pin 6 carries +12 V and must not be connected.
- Use a genuine USB-to-RS-232 adapter and correct AES cable.
- A bare FTDI USB-to-TTL cable is electrically wrong.
- Verify pins 1–5 end-to-end and pin 6 isolation before connection.

## Validation gate

The implementation is a hardware **bench** path, not a field-validation claim.
Complete and retain every item in `PHYSICAL-VALIDATION.md` with known-good 7744F
and 7788F subscribers before field deployment. At minimum validate permission
denial, attach/detach, reconnect, suspend/resume, exact 4800 8-N-1 signaling,
read-only status/routes/zones, every guided programming conversation, monitor
sequencing, secret redaction, RF-key abort, and RAM-reset safeguards.

