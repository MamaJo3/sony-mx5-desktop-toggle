# Sony WH-1000XM5 control protocol (notes)

These are working notes on the parts of Sony's headphone protocol this project uses. Credit to
[Gadgetbridge](https://codeberg.org/Freeyourgadget/Gadgetbridge) and
[SonyHeadphonesClient](https://github.com/Plutoberth/SonyHeadphonesClient), where this was
originally reverse-engineered.

## Transport

- Bluetooth Classic **RFCOMM** (SPP-like), connected via the modern WinRT
  `RfcommDeviceService` / `StreamSocket` API (the legacy Winsock `AF_BTH` connect-by-UUID path
  is unreliable here and returns `WSAEADDRNOTAVAIL` / 10049).
- Control service UUID: `956c7b26-d49a-4ba8-b03f-b17d393cb6e2` (shows up in Windows as
  *"Serial HPC"*). We discover the device by this service, so no MAC address is needed.

## Frame format ("MDR")

```
START(0x3E) | escape( DATA_TYPE seq SIZE(4, big-endian) PAYLOAD CHECKSUM ) | END(0x3C)
```

- `DATA_TYPE` for commands: `0x0C` (DATA_MDR).
- `seq`: a 1-bit sequence number that **alternates 0,1,0,1…** per message on a connection, and
  resets to 0 on a new connection. Reusing the same seq makes the headphones treat a message as
  a duplicate and ignore it (this bit matters for a long-lived connection).
- `SIZE`: payload length, 4 bytes big-endian.
- `CHECKSUM`: sum of `DATA_TYPE + seq + SIZE + PAYLOAD`, modulo 256.
- Escaping (applied to everything between START and END): `0x3C → 0x3D 0x2C`,
  `0x3D → 0x3D 0x2D`, `0x3E → 0x3D 0x2E`.
- The headphones ACK with a `DATA_TYPE = 0x01` frame and emit state notifications; an ACK only
  means "frame received", not "command applied".

## Ambient Sound Control (v2)

The XM5 uses the newer ("v2") command. Set payload (wind-noise-capable variant):

```
0x68 0x17 0x01  <ascOnOff>  <ambientFlag>  <wind>  <focusOnVoice>  <level>
```

| byte | meaning |
|---|---|
| `0x68` | NCASM_SET_PARAM |
| `0x17` | v2 sub-type (wind-capable) |
| `0x01` | constant |
| ascOnOff | `0x00` = off, `0x01` = on |
| ambientFlag | `0x01` = ambient sound, `0x00` = noise cancelling |
| wind | `0x02` = normal, `0x03` = wind-noise reduction |
| focusOnVoice | `0x00`/`0x01` |
| level | ambient level `0x00`–`0x14` (0–20) |

Examples (payload only):

```
Ambient, level 20 : 68 17 01 01 01 02 00 14
Noise cancelling  : 68 17 01 01 00 02 00 00
Off               : 68 17 01 00 00 02 00 00
Wind reduction    : 68 17 01 01 00 03 00 00
```

The older "v1" command (`0x68 0x02 …`) is ACK'd by the XM5 but **not applied** — which is why the
archived clients appear to connect yet do nothing.

## State notifications

The headphones push state with command `0x69` (notify) / `0x67` (get-response) and sub-type
`0x17`; byte index 4 of the payload is the ambient flag (`1` ambient / `0` NC). These can lag a
few seconds behind a change, so this project drives the toggle optimistically and only reads the
live state once per connection to set the initial direction.
