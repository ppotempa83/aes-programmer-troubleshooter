# Direct USB-to-RJ12 cable

The project requirement is a single technician cable:

```text
Windows USB-A / USB-C
        │
USB-to-RS-232 interface
        │
6P6C RJ12 plug
        │
AES 7744F / 7788F J1
```

Use a genuine, driver-supported USB-to-RS-232 interface. A bare FT232/FT231
3.3 V or 5 V TTL cable is not electrically equivalent to an RS-232 cable.

## Recovered user-verified mapping

| AES J1 pin | Signal | PC DB9 equivalent |
|---:|---|---:|
| 1 | Ground | 5 |
| 2 | AES TXD | 3 |
| 3 | AES RXD | 2 |
| 4 | DTR | 4 |
| 5 | DSR | 6 |
| 6 | +12 V | **Not connected** |

RTS and CTS, DB9 pins 7 and 8, were specified as internally jumpered on the PC side.
The software opens the serial port at 4800-8-N-1, disables hardware flow control,
asserts DTR, and leaves RTS disabled.

## Mandatory fabrication checks

Before connecting a subscriber:

1. Identify RJ12 pin numbering from the contact side and document plug orientation.
2. Perform an end-to-end continuity test on every conductor.
3. Confirm J1 pin 6 has no continuity to the USB/RS-232 interface.
4. Confirm there are no adjacent-pin shorts.
5. Confirm TX and RX cross to the correct PC-side signals.
6. Confirm any RTS/CTS jumper is on the PC/interface side only.
7. Label the cable for **AES 7744F/7788F J1 ONLY**.
8. Bench-test with a protected, known-good subscriber before field use.

Telephone cords can reverse conductor order depending on how the modular plugs were
installed. Never assume that jacket colors or a commercial RJ11/RJ12 cable are
straight-through.

The AES manuals identify the 7043E as the supported PC programming cable. A custom
cable should be treated as an engineering substitute and validated accordingly.

