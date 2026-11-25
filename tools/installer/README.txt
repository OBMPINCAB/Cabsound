================================================================================
  CabSound — Per-Table Volume, SSF Levels & Equalizer APO Front End
  for Virtual Pinball Cabinets (Windows 10 / 11, 64-bit)
================================================================================

WHAT IS CABSOUND?
-----------------
CabSound runs quietly in your Windows system tray and automatically adjusts
your cabinet's volume and 7.1 Surround Sound Feedback (SSF) levels for each
Visual Pinball X (VPX) or Future Pinball table you play.

Key features:
  * Per-table Windows master volume + Global / Night mode volume
  * Per-table SSF levels (Backbox, Front exciters, Rear exciters) via Equalizer APO
  * Cabinet-wide and per-table high-pass low cuts (protects exciters from rattle)
  * On-screen cabinet menu (navigate with Flippers & Magna Save; auto-pauses VPX)
  * Guided Speaker Check (verifies all 6 speakers & fixes Side/Rear jack swap)
  * Customizable Controls tab (PinVol Compatible, Minimal 2-button, or Custom
    keys, combos, holds, and joystick buttons, with VPX/Popper conflict check)
  * Automatic Windows Volume Mixer guard (keeps VPX/Popper at 100% in mixer)


REQUIREMENTS
------------
  * Windows 10 or Windows 11 (64-bit). No .NET install required (self-contained).
  * Equalizer APO installed and enabled on your cabinet's 7.1 sound card.
    (Note: in Windows Settings -> System -> Sound -> your sound card, make sure
    "Audio enhancements" is ON, otherwise Equalizer APO is bypassed.)
  * VPX 7.1 SSF audio mode recommended: Sound3D = 5 ("7.1 Surround Sound Feedback
    enhanced").


INSTALLATION
------------
1. Unzip this archive anywhere temporary.
2. Double-click CabSound-Setup.exe.
3. Choose your install folder (default: C:\vPinball\CabSound if C:\vPinball
   exists, otherwise %LOCALAPPDATA%\CabSound).
4. Pick your shortcut options (Desktop, Start Menu, Start with Windows) and
   click Install.
5. Upgrading? Just run CabSound-Setup.exe again into the same folder — your
   existing CabSound.json levels and backups are preserved.


FIRST-TIME SETUP
----------------
When CabSound starts for the first time:
  1. PinVol import (optional): If PinVol is running, CabSound offers to close
     it and import your PinVolTables.ini levels and pop-up position.
     (If you don't use PinVol, you can skip or pick any folder/create fresh
     settings.)
  2. Sound card selection: CabSound detects your USB 7.1 sound card. If not
     found automatically, pick the output connected to your cabinet amp.
  3. Equalizer APO check: if Equalizer APO is missing, not active on the card,
     or Windows "Audio enhancements" are off, a setup window explains the fix
     and does the next step (download page, EQ APO Configurator, or turning
     enhancements back on itself after one administrator prompt, with a
     registry backup in backups\). "Check again" re-tests; "Skip" carries on.
  4. Equalizer APO take-over: CabSound backs up your existing Equalizer APO
     config.txt to the backups\ folder, shows you a preview of what changes,
     and replaces config.txt with a single "Include: CabSound.txt" line.
     Nothing is overwritten without your OK and a backup.


DEFAULT CONTROLS (PINVOL COMPATIBLE)
------------------------------------
Quick hotkeys (work anytime while playing):
  * Vol Up / Vol Down          Table volume up / down
  * F4 / F5                    Backbox level up / down (dB)
  * Ctrl + I / Ctrl + G        Front exciters up / down (dB)
  * Ctrl + F4 / Ctrl + F5      Rear exciters up / down (dB)
  * Ctrl + F9 / Ctrl + F8      Global volume (or Night volume) up / down
  * Ctrl + Enter               Toggle Night Mode on / off
  * Ctrl + Vol Up              Open the on-screen cabinet menu

On-screen cabinet menu (auto-reads your VPX keys from VPinballX.ini):
  * Left / Right Flipper       Move up / down
  * Left / Right Magna Save    Decrease / increase (or confirm Reset)
  * Start (or Vol Up)          Close menu (resumes VPX if paused)

Using a 2-button cabinet or different controller?
  Open CabSound from the system tray -> Controls tab:
  * Pick "Minimal (2 buttons)" to use Tap Vol Up/Down for volume, Hold Vol Up
    1s to open the menu, and Hold Vol Down 2s for Night mode.
  * Or pick "Custom" and click Bind next to any action to press any key,
    key combo, or joystick/PinOne button. Changes take effect immediately.
  * Uncheck "Auto-detect on-screen menu keys from VPX" if you want to set the
    menu navigation keys manually (e.g., Future Pinball-only cabinets).


DESKTOP WINDOW TABS (DOUBLE-CLICK THE TRAY ICON)
------------------------------------------------
  * Now Playing  — Live levels, low cuts, Global/Night volume, Reset & Undo
  * Tables       — Search all tables, edit one, Copy levels to..., Reset
  * Sound        — Cabinet-wide low cuts, 250 Hz dip, Side/Rear jack swap,
                   manual Cabinet balance sliders, Speaker Check, Sound card
  * Controls     — Layout templates, click-to-learn key/button bindings,
                   VPX/Popper conflict check, on-screen menu keys
  * Display      — Monitor, rotation (0/90/180/270), position, size, opacity,
                   and live Preview for both the quick pop-up and cabinet menu


FILES & BACKUPS
---------------
All user data lives right next to CabSound.exe:
  * CabSound.json   All table levels, cabinet balance, controls, and display
  * CabSound.log    Activity and error log (auto-rotates at 2 MB)
  * backups\        Automatic backups of Equalizer APO config.txt & CabSound.json


UNINSTALLING
------------
Open Windows Settings -> Apps -> Installed Apps -> CabSound -> Uninstall
(or run Uninstall-CabSound.ps1 in your install folder). Your backups\ folder
and CabSound.json can be kept or deleted when prompted.
================================================================================
