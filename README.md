# XM5 Ambient Control

Control your **Sony WH-1000XM5** noise-cancelling and ambient sound from **Windows**, with an
instant global keyboard shortcut. Toggle between modes, or hold a key to hear your surroundings
push-to-talk style.

Sony ships no desktop app, and the XM5's newer ("v2") Bluetooth protocol isn't handled by the
older open-source PC clients (they fail to apply commands). This project talks to the headphones
directly over Bluetooth RFCOMM using the modern WinRT API, speaking the v2 *Ambient Sound Control*
command — and keeps the connection warm so each key press takes **~0–2 ms** instead of ~1 second.

> Not affiliated with or endorsed by Sony. Use at your own risk.

## Features

- **Global hotkey** to switch Ambient Sound Control, configurable to any Ctrl/Alt/Shift/Win combo.
- **Toggle** mode (each press flips between two modes) or **Hold** mode (one mode while held,
  another on release — like Quick Attention).
- Choose what the two modes are: **Ambient Sound** (with level 0–20), **Noise Cancelling**,
  **Off**, or **Wind Noise Reduction**.
- **Settings GUI** — no config files to hand-edit.
- **Instant** — a small background daemon keeps the Bluetooth link warm.
- **Auto-discovery** — finds your headphones by their control service (no MAC address to configure).

## Requirements

- Windows 10 (build 19041+) or Windows 11.
- A Sony **WH-1000XM5**, paired and connected to the PC over Bluetooth.
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run, or the
  .NET 8 SDK to build.

> Other Sony models may work if they expose the same control service, but only the WH-1000XM5
> has been tested. See [docs/PROTOCOL.md](docs/PROTOCOL.md).

## Install

```powershell
git clone <your-fork-url> sony-xm5-ambient
cd sony-xm5-ambient
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

This builds the apps, installs them to `%LOCALAPPDATA%\XM5Ambient`, starts the daemon, sets it to
auto-start at login, and adds an **"XM5 Ambient Settings"** shortcut to your Desktop.

Then press **Ctrl+Alt+A** to toggle ambient ↔ noise cancelling, or open the Settings app to
customise the shortcut and behaviour.

To remove everything:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## Build only (no install)

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1          # outputs to .\dist
# or
dotnet build SonyXm5Ambient.sln -c Release
```

## Usage

Open **XM5 Ambient Settings** (desktop shortcut) to configure:

| Setting | What it does |
|---|---|
| **Shortcut** | Click the box and press your combo (needs Ctrl/Alt/Shift). |
| **Behavior — Toggle** | Each press switches between *Mode 1* and *Mode 2*. |
| **Behavior — Hold** | *While held* uses the first mode; *on release* the second. |
| **Modes** | Each slot: Ambient Sound / Noise Cancelling / Off / Wind Noise Reduction. |
| **Ambient level** | 0–20, used when a mode is Ambient. |

Click **Save & Apply** — settings are written and the daemon restarts immediately.

### Command line (optional)

```powershell
sony-ambient-daemon --once toggle      # flip between the two configured modes
sony-ambient-daemon --once amb         # ambient
sony-ambient-daemon --once nc          # noise cancelling
sony-ambient-daemon --once off
sony-ambient-daemon --once wind
```

## How it works

```
SonyXm5.Core      protocol framing, v2 Ambient Sound Control command, BT service discovery, config
SonyXm5.Daemon    resident app: keeps the connection warm, low-level keyboard hook, toggle/hold
SonyXm5.Settings  WinForms settings GUI (writes config.json, restarts the daemon)
```

- The daemon connects once and holds the RFCOMM control socket open, so commands fire instantly.
  The first press after the headphones (re)connect or after boot may take ~1 s to re-establish.
- It reads the headphones' state once per connection to sync the toggle direction.
- Config lives in `config.json` next to the executables (in the install dir).

## Configuration file

`config.json` (managed by the GUI, but human-editable):

```json
{
  "hotkey": "CTRL+ALT+A",
  "behavior": "toggle",
  "modeA": "amb",
  "modeB": "nc",
  "ambientLevel": 20
}
```

`hotkey`: modifiers (`CTRL`/`ALT`/`SHIFT`/`WIN`) + one key (`A`–`Z`, `0`–`9`, `F1`–`F24`,
`SPACE`, `ENTER`, `TAB`, `ESC`, `INSERT`, `DELETE`, `HOME`, `END`, `PAGEUP`, `PAGEDOWN`,
`UP`/`DOWN`/`LEFT`/`RIGHT`). `behavior`: `toggle` or `hold`. `modeA`/`modeB`: `amb`/`nc`/`off`/`wind`.

## Limitations

- The control service UUID is for Sony's current headphones; very old models won't match.
- "Hold" mode needs a key you can hold (a letter or F-key with a modifier works best).
- The hotkey is "swallowed" while it fires, so it won't also type into the focused app.
- Bluetooth must be connected to *this* PC (not only to your phone via multipoint).

## Credits

The Sony control protocol was reverse-engineered by, and this implementation is derived from:

- [**Gadgetbridge**](https://codeberg.org/Freeyourgadget/Gadgetbridge) — the v2 Ambient Sound
  Control command (AGPL-3.0).
- [**SonyHeadphonesClient**](https://github.com/Plutoberth/SonyHeadphonesClient) — the MDR framing,
  checksums, and service UUID (GPL-3.0).

## License

GPL-3.0. See [LICENSE](LICENSE).
