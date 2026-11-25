# CabSound — sound setup guide (short)

How to get the best sound out of this cabinet with CabSound. Work top to bottom: each step
depends on the one before. Do it in a quiet room, no table running.

## 1. Check Windows and the card first
If one of these is wrong, nothing CabSound does will sound right.
- **Audio enhancements ON** for the card (Settings → System → Sound → "Speakers (USB Multi-Channel
  Audio Device)"). Off = Equalizer APO is bypassed: levels and low cuts do nothing.
- **C-Media panel** (`Cm106eye.exe`): 8 CH, 7.1 Speakers, 48 kHz, **DSP Mode OFF** (Xear / Virtual
  Speaker Shifter off). A wrong state gives echo and sound smeared over all speakers.
- **Windows speaker setup:** 7.1, Center and Subwoofer **unticked** (the C/SUB jack is not used; the
  amp makes sub + shaker itself).
- **VPX:** `Sound3D = 5` ("7.1 Surround Sound Feedback (enhanced)").
- **Volume mixer:** VPX, Future Pinball, PinUP at 100 % (CabSound fixes this itself every 2 s).
- **CabSound status:** "Equalizer APO: Active" (tray / log). Any warning here: fix it first.

## 2. Speaker check (Sound tab → "Check the speakers…")
- Plays each speaker in turn at your pace; answer Right place / Wrong place / Not heard.
- Goal: **"All correct"**. Expected: backbox = L R, front exciters (near you) = RL RR, rear exciters = SL SR.
- **"Side and Rear jacks swapped" stays OFF** with the C-Media driver. Only if the check says front and
  rear are exchanged (e.g. after a Windows/driver update) let it switch the swap, then run the check again.
- Use the **Rotate** button on the window to read it standing at the front.

## 3. Balance the cabinet (Calibration tab)
1. **"Calibrate with a phone meter…"**: Android sound-meter app at the playing position.
   - Turn the **amp knobs** first (backbox, exciters), then type the readings; the wizard sets the fine trim
     and checks it. Goal: backbox, front and rear equally loud.
   - Overall loudness afterwards with **Master** / the amp, not with the group levels.
2. **"Find the exciter limit…"**: low noise steps down until you hear **rattle**. The result drives the
   rattle warning on the low-cut choices.

## 4. Presets (Presets tab): your sound taste
- Six presets; each = levels + low cuts. A table = preset + its own small adjustment.
- **Low cuts are a trade-off:** a cut protects exciters/backbox from rattle but also takes bass away from
  the amp's **sub (fed by the backbox)** and **shaker (fed by the exciters)**.
  - Want more thump/shaker: lighter or no exciter cut — but stay above the measured exciter limit.
  - Rattles or buzz: raise the cut (or lower that group).
- Make one good preset, then **"All tables on preset"** (menu → Save / reset) so every table starts there.
- **Keep boosts small:** every dB of boost costs a dB of headroom for the whole cabinet. Prefer cutting
  the loud group over boosting the quiet one.

## 5. Per table, while playing
- Keys (PinOne sends them): **Vol Up/Down** table volume · **F4/F5** backbox · **Ctrl+I / Ctrl+G** front
  exciters · **Ctrl+F4 / Ctrl+F5** rear exciters · **Ctrl+F9 / Ctrl+F8** global · **Ctrl+Enter** night mode
  (Ctrl = magna save).
- **Magna + Vol Up** = on-screen menu (table paused): levels, preset, Save / reset page.
- Changes save per table automatically.
- Menu **Save / reset**: *Back to preset* (drop this table's tweaks) · *Save sound to preset* (make this
  table's sound the preset) · *All tables on preset*. Each needs a second press within 3 s; Undo is in the window.
- Typical good result: flipper clack at the front, top-of-playfield sounds at the rear, voices/music in
  the backbox, thumps in the shaker.

## 6. Table sounds themselves (when a table sounds wrong)
- `table-check.bat check "<table.vpx>"` lists sound problems (read-only).
- **Stereo table sounds are fine** for SSF — never make them mono (it only made them ~6 dB quieter).
- **Impact effects belong on the Table output** (only those reach the exciters, sub and shaker);
  voices, callouts and music on the backbox. Fix with `table` / `backglass`.
- Patches need VPX and Popper closed and make a backup first. Follow the `table-sound-fix` routine.

## Quick fixes
| Symptom | Check |
|---|---|
| Level keys / low cuts do nothing | Audio enhancements off |
| No table sound, Popper fine | VPX at 0 % in the volume mixer |
| Echo, sound on every speaker | C-Media DSP Mode on → off + reboot |
| Front and rear swapped | Speaker check → jack swap; after driver updates |
| Flippers heard front *and* rear | Normal for the thump (bass travels through the cabinet); the *clack* should be front only |
| Weak shaker | Exciter low cut too high; amp shaker knob |
| Rattle / buzz | Exciter cut below the measured limit; lower that group |
