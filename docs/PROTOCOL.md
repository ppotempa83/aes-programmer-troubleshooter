# Legacy AES PC terminal protocol

The 7744F and 7788F manuals specify:

```text
4800 baud
8 data bits
No parity
1 stop bit
RTS/CTS flow control OFF
ASCII
```

## Command map

| Handheld function | PC byte | App function |
|---|---:|---|
| F1 | Ctrl+Q (`0x11`) | Advanced F1 |
| F2 | Ctrl+R (`0x12`) | Time-to-Live |
| F3 | Ctrl+S (`0x13`) | Advanced F3 |
| F4 | Ctrl+T (`0x14`) | Full routing table |
| F5 | Ctrl+U (`0x15`) | Send text message |
| Ctrl+Z | `0x1A` | Zone status |
| Shift+F1 | `a` | Receive Monitor |
| Shift+F2 | `b` | Transmit Monitor |
| Shift+F3 | `c` | Monitor All |
| Shift+F4 | `d` | Local status |
| Shift+F5 | `e` | Key transmitter |
| Ctrl+F1 | `f` | Program ID and cipher |
| Ctrl+F2 | `g` | Program timers |
| Ctrl+F3 | `h` | Program zones/restorals |
| Ctrl+F4 | `i` | Program operating modes |
| Ctrl+F5 | `j` | Reset RAM |

Interactive answers are terminated by carriage return (`0x0D`). Hexadecimal entries
must use uppercase A–F.

## Model differences

Both models use the same PC command bytes. Important differences include:

- 7788F: eight EOL inputs; operating modes include charger-fault and ground-fault
  suppression prompts.
- 7744F: four EOL inputs and four reversing-voltage inputs; its documented operating
  mode sequence contains repeating and AC-failure suppression prompts.

The app changes the guided operating-mode response count based on the selected model.
Firmware-specific menus remain accessible through Terminal.

## Monitor behavior

Monitor All requires Receive Monitor to be enabled first. The app enforces that order.
All monitor functions should be turned off when testing finishes. Text messages cannot
be received while a monitor function is active.

## Parsing

The app recognizes:

```text
SUB [revision] 7744F|7788F
ID#:NNNN
RT1:NNNN LEVEL:NNN
STAT:NNN NETCON:N
1.AA11,L:01,N:0,Q:03
Zx##,Z1-8:####-####
```

Routing-table entries are normalized to preference order 1–8. STAT values are decoded
as additive hexadecimal bit flags.

