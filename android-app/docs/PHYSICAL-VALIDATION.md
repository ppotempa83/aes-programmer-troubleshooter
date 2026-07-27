# Android physical-validation checklist

Simulation and emulator tests do not validate a physical programming path.

## Device and cable

- [ ] Record Android manufacturer, model, OS version, and security patch.
- [ ] Confirm USB Host/OTG support.
- [ ] Record adapter manufacturer, model, chipset, VID/PID, and serial number.
- [ ] Confirm the adapter is genuine FTDI hardware before using hardware mode.
- [ ] Confirm the adapter is true RS-232, not TTL.
- [ ] Verify AES J1 pins 1–5 end-to-end.
- [ ] Verify J1 pin 6 (+12 V) is isolated.
- [ ] Verify no adjacent-pin shorts.
- [ ] Determine whether a powered USB hub is required.

## Android USB lifecycle

- [ ] First attachment prompts for USB permission.
- [ ] Permission denial leaves the app stable and disconnected.
- [ ] Approved attachment opens at 4800 8-N-1 with flow control off.
- [ ] DTR is asserted and RTS is cleared.
- [ ] Cable removal does not crash or hang the app.
- [ ] Reconnect works without restarting the app.
- [ ] Screen rotation preserves or safely restores session state.
- [ ] Background/foreground transitions do not leave the adapter locked.

## Known-good radios

Run every applicable desktop bench-validation item against both a known-good
7744F and a known-good 7788F, beginning with read-only operations:

- [ ] Local status matches a trusted reference.
- [ ] Full routing table parses correctly.
- [ ] Zone status parses correctly.
- [ ] Monitor start/stop sequencing is correct.
- [ ] Interactive programming preserves blank/unchanged values.
- [ ] Cipher input never appears in Android logs or exports.
- [ ] Safety-critical RF key and RAM reset actions remain blocked until separately
      authorized and validated.

## Contact ID accessories

- [ ] Verify the exact IntelliPro/IntelliTap model, revision, subscriber
      compatibility, and official manual.
- [ ] Place the account on test before capture testing.
- [ ] Confirm representative alarm, trouble, supervisory, and restoral events at
      the central station.
- [ ] Reconnect all accessory wiring before returning the account to service.

Do not describe the Android transport as field-ready until this checklist is
completed and retained with the release record.
