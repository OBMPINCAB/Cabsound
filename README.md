# CabSound

**Per-table volume, 7.1 Surround Sound Feedback (SSF) levels and an Equalizer APO front end for virtual pinball cabinets.**

CabSound runs in the Windows system tray. When you start a Visual Pinball X or Future Pinball table (directly or from PinUP Popper), it sets that table's volume and its backbox / front-exciter / rear-exciter levels, and remembers any change you make while playing. It is a drop-in replacement for PinVol: it imports PinVol's table levels and keeps PinVol's default buttons.

> **Requires [Equalizer APO](https://sourceforge.net/projects/equalizerapo/)** (free). CabSound writes its levels and low cuts as an Equalizer APO filter file. Without Equalizer APO only the table volume works. See [Equalizer APO](#equalizer-apo) below. CabSound checks this on start-up and walks you through any fix.

## Features

**Per-table sound**
- Table detected automatically from VPX / Future Pinball / Popper command lines.
- Per-table Windows volume, plus Global volume and a Night mode volume.
- Per-table SSF levels (dB) for Backbox (L R), Front exciters and Rear exciters.
- Per-table and cabinet-wide low cuts (high-pass) to stop exciter rattle and buzz.
- Changes are saved as you play; Reset and Undo per table; copy one table's levels to others.

**Equalizer APO front end**
- Start-up check: Equalizer APO installed? Active on the cabinet's sound card? Windows "Audio enhancements" on?
- First-run setup window for each problem: opens the download page, opens EQ APO's Configurator naming the card to tick, or turns Windows audio enhancements back on itself (one administrator prompt, registry backed up first).
- Takes over EQ APO's `config.txt` safely: shows a preview, backs it up, and replaces it with one `Include: CabSound.txt` line. Settings found in the old file (PinVol's or hand-written) are carried over.
- Option for a sound card driver that swaps the Side and Rear jacks.

**Cabinet controls and overlays**
- PinVol-compatible hotkeys out of the box (volume, backbox, exciters, global / night).
- Controls tab: presets (PinVol compatible, Minimal 2-button) or Custom bindings — keys, combos, holds, joystick / PinOne buttons, learned by pressing them. Warns about conflicts with VPX and Popper bindings.
- On-screen cabinet menu over the playfield, driven by flippers and Magna Saves; pauses VPX while open.
- Quick level pop-up; both overlays can be placed, sized, rotated (0/90/180/270) and faded per monitor.

**Setup and check tools**
- Speaker check: names each speaker in turn so you can confirm the wiring, and detects swapped Side / Rear jacks.
- Bass check: test bursts for setting an SSF amp's subwoofer and bass-shaker knobs.
- Tuning guide: short amp-setup and per-table tuning checklists, on the desktop and in the cabinet menu.
- Volume mixer guard: puts VPX / Popper back to 100% if Windows' volume mixer turned them down.

**Desktop window** (double-click the tray icon): Now Playing · Tables · Sound · Controls · Display.

**Extra tools (for advanced users)**
- `tools/CabSound.TableCheck` — scans `.vpx` tables for sound-routing problems for SSF (sounds sent to the backbox, mono / stereo) and applies safe, backed-up sound patches.
- `tools/CabSound.Probe` — console test tool: voice check per channel, per-channel loopback levels after Equalizer APO, test tones and noise.

## Requirements

- Windows 10 or 11, 64-bit.
- A 7.1 sound card (typically USB) feeding the cabinet's SSF amplifier.
- **Equalizer APO**, installed and enabled for that sound card, with Windows *Audio enhancements* ON for the card.
- Recommended VPX audio setting: *7.1 Surround Sound Feedback (enhanced)* (`Sound3D = 5` in `VPinballX.ini`).
- Building from source: [.NET 10 SDK](https://dotnet.microsoft.com/download).

## Equalizer APO

CabSound does not process audio itself: it writes `CabSound.txt` into `C:\Program Files\EqualizerAPO\config\` and Equalizer APO applies it to the sound card.

1. Download and install Equalizer APO from <https://sourceforge.net/projects/equalizerapo/>.
2. In the Configurator at the end of the install, tick **only** the cabinet's 7.1 output (normally "Speakers (…)"), then restart Windows.
3. In Windows Settings → System → Sound → that card, make sure **Audio enhancements** is on (*Device Default Effects*). When it is off, Windows bypasses Equalizer APO: levels and low cuts do nothing.
4. Start CabSound. It checks all of the above and, if something is missing, opens a setup window with the fix. It then asks before taking over `config.txt` (backup in `backups\`).

Windows updates and new sound card drivers can switch Equalizer APO off for a card; CabSound re-checks at every start and warns in the tray.

## Install

Download the latest release, unzip, run `CabSound-Setup.exe`, pick the folder and shortcuts (optionally *Start with Windows*). Running it again upgrades in place and keeps your levels. See [`tools/installer/README.txt`](tools/installer/README.txt) for the full user guide, default hotkeys and uninstall steps.

## First start

1. If PinVol is running, CabSound offers to close it and import its table levels and pop-up position (or start fresh).
2. Finds the sound card, or asks you to pick the output the amp is connected to.
3. Equalizer APO check and setup (above).
4. Preview and take-over of Equalizer APO's `config.txt`.

All data lives next to `CabSound.exe`: `CabSound.json` (levels, controls, display), `CabSound.log`, and `backups\`.

## Build from source

```powershell
dotnet test tests\CabSound.Core.Tests
```
```powershell
dotnet build src\CabSound.App -c Release
```

Release package: `tools\installer\build-release.ps1`. Helper scripts: `run-tests.bat`, `publish-app.bat`, `run-probe.bat`, `table-check.bat`.

| Project | What it is |
|---|---|
| `src/CabSound.Core` | Pure logic, fully unit-tested: PinVol import, table keys, levels, Equalizer APO file builder and config reader, bindings, menus, checks. |
| `src/CabSound.Platform` | Windows parts: process watcher, joystick and keyboard hooks, system volume, Equalizer APO checks, audio playback. |
| `src/CabSound.App` | WPF tray app and windows. |
| `tests/CabSound.Core.Tests` | xUnit tests. |
| `tools/` | TableCheck, Probe, installer. |

## Safety

CabSound never changes Equalizer APO's `config.txt`, table files or the registry without showing what will change and making a backup first.

## License

[MIT](LICENSE)
