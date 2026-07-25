# Bench-validation plan

Do not release the cable or application to technicians until each applicable step is
recorded against known-good 7744F and 7788F units.

## 1. Cable and isolation

- [ ] Photograph and record RJ12 plug orientation.
- [ ] Verify J1 pins 1–5 end-to-end with a meter.
- [ ] Verify J1 pin 6 (+12 V) is isolated.
- [ ] Verify no adjacent-pin shorts.
- [ ] Verify USB adapter enumerates reliably after repeated insertion.
- [ ] Record adapter manufacturer, chipset, USB VID/PID, and driver version.

## 2. Read-only commands

- [ ] Local status (`d`) matches the handheld display.
- [ ] Full routing table (`Ctrl+T`) matches the subscriber.
- [ ] Zone status (`Ctrl+Z`) reports all eight physical states correctly.
- [ ] 7744F model and firmware are parsed.
- [ ] 7788F model and firmware are parsed.
- [ ] STAT combinations such as `084`, `101`, and `300` decode correctly.

## 3. Monitor functions

- [ ] RX Monitor toggles on and off.
- [ ] TX Monitor toggles on and off.
- [ ] Monitor All is rejected or guided correctly when RX Monitor is off.
- [ ] Stop All leaves every monitor disabled.
- [ ] Packet text remains readable during a sustained capture.
- [ ] Normal text-message reception resumes after monitors are disabled.

## 4. Programming

Record the original programming first. Use non-production bench subscribers.

- [ ] ID is preserved when ENTER is sent without a replacement.
- [ ] Cipher is preserved when ENTER is sent without a replacement.
- [ ] New test ID and cipher are accepted.
- [ ] Timer values are accepted and read back or otherwise verified.
- [ ] 7788F zone and restoral strings are accepted.
- [ ] 7744F zone and restoral strings are accepted.
- [ ] Physical board RESET correctly reinitializes zones.
- [ ] 7788F four-prompt mode sequence completes.
- [ ] 7744F two-prompt mode sequence completes.
- [ ] TTL interactive menu can be completed from Terminal.
- [ ] Text message arrives at the central station.
- [ ] RAM reset requires both app confirmations and produces documented defaults.

## 5. RF transmitter test

- [ ] Account is on test and RF operation is authorized.
- [ ] Correct antenna, dummy load, or wattmeter is connected.
- [ ] `e` keys the transmitter for the documented interval.
- [ ] ENTER aborts an active key test.
- [ ] Forward and reflected readings can be recorded.
- [ ] More than 10% reflected power creates a critical finding.
- [ ] Voltage drop is calculated correctly.

## 6. Troubleshooter

Create controlled simulated or bench cases:

- [ ] RT1 `XXXX` and LEVEL `255` produce not-enrolled guidance.
- [ ] NETCON 0–5 passes the legacy fire target.
- [ ] NETCON 6 creates corrective-investigation guidance.
- [ ] NETCON 7 creates a critical finding.
- [ ] Strong local Q with peer N6/N7 identifies an upstream mesh issue.
- [ ] Q81–Q83 with poor NETCON identifies likely local RF weakness.
- [ ] Only one route produces a route-diversity warning.
- [ ] Outside improvement produces a building-attenuation inference.

## 7. Reports and recovery

- [ ] HTML report opens and prints without clipped tables.
- [ ] Raw transcript contains commands and received text.
- [ ] Cipher input is masked in the transcript.
- [ ] Cable removal does not crash the application.
- [ ] Reconnect works without restarting the application.
- [ ] App closes cleanly with an open port.
- [ ] IntelliTap/accessory cable is reconnected after programming.
- [ ] Alarm, trouble, supervisory, and every required restoral are confirmed at the
      central station before returning the account to service.

