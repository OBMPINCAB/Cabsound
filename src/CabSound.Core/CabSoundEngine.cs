namespace CabSound.Core;

/// <summary>What the engine needs from the outside world (Windows volume, files, pop-up).</summary>
public interface IEngineOutput
{
    void SetVolume(int percent);
    void WriteInclude(string text);
    void SaveData(CabSoundData data);
    void ShowPopup(PopupInfo info);

    /// <summary>From now on SetVolume goes to this sound card (the owner picked another one on the Sound tab).</summary>
    void UseSoundCard(string id, string name) { }
}

/// <summary>
/// What the quick pop-up shows. Value: "43" for volume, "+3 dB" for an SSF offset.
/// Level: 0..1 for the level bar (volume 0..100, SSF -limit..+limit).
/// </summary>
public sealed record PopupInfo(string Table, string Group, string Value, bool AtLimit, double Level);

/// <summary>
/// CabSound's brain: applies a table's levels when it starts, and button actions (Layout 1)
/// while it runs. Volume is applied at once; SSF changes and saving go through WriteScheduler.
/// Call from one thread only.
/// </summary>
public sealed class CabSoundEngine(CabSoundData data, IEngineOutput output)
{
    private readonly VolumeBook _book = new(data.Tables, data.SsfLimitDb, data.SsfBoostLimitDb);
    private readonly WriteScheduler _writes = new();

    public string CurrentTable { get; private set; } = PinVolImport.SystemRow;

    /// <summary>Levels of the running table (System volume + 0 offsets if it's new).</summary>
    public TableLevel Current => _book.Get(CurrentTable);

    /// <summary>Night mode (PinVol style): the Night level replaces the Global level until toggled off.</summary>
    public bool NightMode { get; private set; }

    public void TableChanged(string key, long nowMs)
    {
        CurrentTable = key;
        if (TableRunning) Calibrating = false; // a table starts: it gets its normal levels
        ApplyVolume();
        output.WriteInclude(Include());
    }

    // --- Speaker check / test mode: while the speaker check window is open ---

    public const string PopperRow = "PinUP Popper";

    /// <summary>
    /// Speaker check window is open: CabSound.txt holds the trial levels only (no table), Windows
    /// volume belongs to the window's slider, and level keys, volume keys and the menu do nothing.
    /// </summary>
    public bool Calibrating { get; private set; }

    /// <summary>A table is running (anything other than System or PinUP Popper).</summary>
    public bool TableRunning => CurrentTable is not (PinVolImport.SystemRow or PopperRow);

    private GroupLevels _trial;
    private int? _trialExciterCut;
    private int? _trialBackboxCut;

    /// <summary>Starts test mode: flat trial, volume 10 %. false (and nothing changes) while a table runs.</summary>
    public bool BeginCalibration()
    {
        if (TableRunning) return false;
        if (Calibrating) return true;
        Calibrating = true;
        (_trial, _trialExciterCut, _trialBackboxCut) = (new GroupLevels(0, 0, 0, 0), null, null);
        output.SetVolume(10);
        output.WriteInclude(Include());
        return true;
    }

    /// <summary>The levels to try (written lowered by the boost limit, as a table's are); written at once.</summary>
    public void SetCalibrationTrial(GroupLevels trial, int? exciterCut = null, int? backboxCut = null)
    {
        if (!Calibrating) return;
        (_trial, _trialExciterCut, _trialBackboxCut) = (trial, exciterCut, backboxCut);
        output.WriteInclude(Include());
    }

    /// <summary>The speaker check window's volume slider, 0..100, applied at once.</summary>
    public void SetCalibrationVolume(int percent)
    {
        if (Calibrating) output.SetVolume(Math.Clamp(percent, 0, 100));
    }

    /// <summary>Leaves test mode: the normal file and the table's volume are written again.</summary>
    public void EndCalibration()
    {
        if (!Calibrating) return;
        Calibrating = false;
        ApplyVolume();
        output.WriteInclude(Include());
    }

    /// <summary>A finished speaker check: its one-line verdict, shown on the Sound tab.</summary>
    public void SaveSpeakerCheck(string summary, DateOnly today, long nowMs)
    {
        data = data with { SpeakerCheck = new SpeakerCheckResult(summary, today.ToString("yyyy-MM-dd")) };
        _writes.DataChanged(nowMs);
    }

    /// <summary>Where the speaker check window was left when it closed, and its rotation.</summary>
    public void SaveToolWindowPlace(ToolWindowPlace place, long nowMs)
    {
        if (data.ToolWindow == place) return;
        data = data with { ToolWindow = place };
        _writes.DataChanged(nowMs);
    }

    public void Handle(CabAction action, long nowMs)
    {
        if (Calibrating) return;
        switch (action)
        {
            case CabAction.TableUp: ChangeVolume(+1, nowMs, true); break;
            case CabAction.TableDown: ChangeVolume(-1, nowMs, true); break;
            case CabAction.BackglassUp: ChangeSsf(SsfGroup.Backglass, +1, nowMs, true); break;
            case CabAction.BackglassDown: ChangeSsf(SsfGroup.Backglass, -1, nowMs, true); break;
            // Front exciters (near the player) are RL RR, as VPX sends them (see EqApoFile)
            case CabAction.FrontUp: ChangeSsf(SsfGroup.Front, +1, nowMs, true); break;
            case CabAction.FrontDown: ChangeSsf(SsfGroup.Front, -1, nowMs, true); break;
            case CabAction.RearUp: ChangeSsf(SsfGroup.Rear, +1, nowMs, true); break;
            case CabAction.RearDown: ChangeSsf(SsfGroup.Rear, -1, nowMs, true); break;
            case CabAction.GlobalUp: ChangeGlobal(+1, nowMs, true); break;
            case CabAction.GlobalDown: ChangeGlobal(-1, nowMs, true); break;
            case CabAction.NightMode: ToggleNightMode(); break;
        }
    }

    /// <summary>One on-screen menu change: same as the buttons, but no pop-up (the menu shows the value itself).</summary>
    public void ApplyMenu(MenuEffect e, IReadOnlyList<MonitorInfo> monitors, long nowMs)
    {
        if (Calibrating) return;
        switch (e.Row)
        {
            case MenuRow.TableVolume: ChangeVolume(e.Direction, nowMs, false); break;
            case MenuRow.Backbox: ChangeSsf(SsfGroup.Backglass, e.Direction, nowMs, false); break;
            case MenuRow.FrontExciters: ChangeSsf(SsfGroup.Front, e.Direction, nowMs, false); break;
            case MenuRow.RearExciters: ChangeSsf(SsfGroup.Rear, e.Direction, nowMs, false); break;
            case MenuRow.BackboxLowCut: StepLowCut(LowCutGroup.Backbox, e.Direction, nowMs); break;
            case MenuRow.ExciterLowCut: StepLowCut(LowCutGroup.Exciters, e.Direction, nowMs); break;
            case MenuRow.RearExciterLowCut: StepLowCut(LowCutGroup.RearExciters, e.Direction, nowMs); break;
            case MenuRow.GlobalVolume: ChangeGlobal(e.Direction, nowMs, false); break;
            case MenuRow.NightMode: SetNightMode(e.Direction > 0); break;
            case MenuRow.Reset: Reset(CurrentTable, nowMs); break;
            default:
                var overlays = data.Overlays ?? OverlayLayout.Defaults([]);
                SetOverlay(e.Window, OverlayEditor.Step(overlays.Get(e.Window), e.Row, e.Direction, monitors), nowMs);
                break;
        }
    }

    // --- Window edits: no pop-up; nothing happens when nothing changed ---

    /// <summary>Sets any table's levels (the table is levels.Name).</summary>
    public void SetLevels(TableLevel levels, long nowMs) => Store([levels], nowMs);

    /// <summary>System volume, own adjustments 0, cabinet's low cuts; Volume2 kept.</summary>
    public void Reset(string table, long nowMs)
    {
        var volume = _book.Get(PinVolImport.SystemRow).Volume;
        Store([_book.Get(table) with
        {
            Volume = volume, BackglassDb = 0, RearDb = 0, FrontDb = 0, SubDb = 0,
            BackboxLowCut = null, ExciterLowCut = null, RearExciterLowCut = null,
        }], nowMs);
    }

    /// <summary>Copies volume, SSF offsets and low cuts; each target keeps its name and Volume2.</summary>
    public void CopyLevels(string from, IEnumerable<string> to, long nowMs)
    {
        var s = _book.Get(from);
        Store(to.Where(name => name != from).Select(name => _book.Get(name) with
        {
            Volume = s.Volume, BackglassDb = s.BackglassDb, RearDb = s.RearDb, FrontDb = s.FrontDb, SubDb = s.SubDb,
            BackboxLowCut = s.BackboxLowCut, ExciterLowCut = s.ExciterLowCut, RearExciterLowCut = s.RearExciterLowCut,
        }).ToList(), nowMs);
    }

    public void SetGlobal(int level, long nowMs) => SetLevel(data with { GlobalVolume = Math.Clamp(level, 0, 100) }, nowMs);

    public void SetNight(int level, long nowMs) => SetLevel(data with { NightVolume = Math.Clamp(level, 0, 100) }, nowMs);

    public void SetNightMode(bool on)
    {
        if (NightMode == on) return;
        NightMode = on;
        ApplyVolume();
        output.WriteInclude(Include());
    }

    /// <summary>One table value from the window, applied to the table as it is now (keeps changes made meanwhile).</summary>
    public void SetTableValue(string table, LevelField field, int value, long nowMs)
    {
        var t = _book.Get(table);
        SetLevels(field switch
        {
            LevelField.Volume => t with { Volume = value },
            LevelField.Backglass => t with { BackglassDb = value },
            LevelField.Rear => t with { RearDb = value },
            _ => t with { FrontDb = value },
        }, nowMs);
    }

    /// <summary>One table's low cut for a group (see LowCutChoices); the file is rewritten only if that table is running.</summary>
    public void SetLowCut(string table, LowCutGroup group, int? value, long nowMs)
    {
        var t = _book.Get(table);
        SetLevels(group switch
        {
            LowCutGroup.Backbox => t with { BackboxLowCut = value },
            LowCutGroup.Exciters => t with { ExciterLowCut = value },
            _ => t with { RearExciterLowCut = value },
        }, nowMs);
    }

    /// <summary>Cabinet sound settings (low cuts, 250 Hz dip, jack swap); nothing happens when unchanged.</summary>
    public void SetSound(SoundSettings sound, long nowMs)
    {
        if (sound == data.SoundOrDefault) return;
        data = data with { Sound = sound };
        // During speaker check, apply immediately so the very next test sound uses the new jack swap
        if (Calibrating) output.WriteInclude(Include());
        else _writes.IncludeChanged(nowMs);
        _writes.DataChanged(nowMs);
    }

    /// <summary>Cabinet control bindings and on-screen menu keys; nothing happens when unchanged.</summary>
    public void SetControls(ControlsSettings controls, long nowMs)
    {
        if (controls.SameAs(data.ControlsOrDefault)) return;
        data = data with { Controls = controls };
        _writes.DataChanged(nowMs);
    }

    /// <summary>
    /// Another sound card output for the table volume (Sound tab); nothing happens when it is the same one.
    /// The remembered driver belonged to the old card, so it is forgotten (see ChannelCheckAdvice).
    /// </summary>
    public void SetSoundCard(string id, string name, long nowMs)
    {
        if (id == data.DeviceId) return;
        data = data with { DeviceId = id, DeviceName = name, DeviceDriver = null };
        output.UseSoundCard(id, name);
        ApplyVolume();
        _writes.DataChanged(nowMs);
    }

    private void StepLowCut(LowCutGroup group, int direction, long nowMs)
    {
        var current = group switch
        {
            LowCutGroup.Backbox => Current.BackboxLowCut,
            LowCutGroup.Exciters => Current.ExciterLowCut,
            _ => Current.RearExciterLowCut,
        };
        SetLowCut(CurrentTable, group, LowCutChoices.Step(current, direction), nowMs);
    }

    /// <summary>Resets cabinet balance (calibration) to 0 / 0 / 0 dB once physical amp gains are balanced.</summary>
    public void ResetCalibration(long nowMs) => SetCalibration(new GroupLevels(0, 0, 0, 0), nowMs);

    /// <summary>One calibration group from the window (keeps the other groups as they are now).</summary>
    public void SetCalibrationGroup(SsfGroup group, int value, long nowMs)
    {
        var c = data.Calibration;
        SetCalibration(group switch
        {
            SsfGroup.Backglass => c with { Backglass = value },
            SsfGroup.Rear => c with { Rear = value },
            SsfGroup.Front => c with { Front = value },
            _ => c with { Sub = value },
        }, nowMs);
    }

    /// <summary>Cabinet calibration, each group -30..+10 dB.</summary>
    public void SetCalibration(GroupLevels c, long nowMs)
    {
        var next = ClampCalibration(c);
        if (next == data.Calibration) return;
        data = data with { Calibration = next };
        _writes.IncludeChanged(nowMs);
        _writes.DataChanged(nowMs);
    }

    /// <summary>One overlay's placement (pop-up or menu); nothing happens when it is unchanged.</summary>
    public void SetOverlay(OverlayKind kind, OverlayPlacement p, long nowMs)
    {
        var current = data.Overlays ?? OverlayLayout.Defaults([]);
        if (current.Get(kind) == p) return;
        data = data with { Overlays = current.With(kind, p) };
        _writes.DataChanged(nowMs);
    }

    /// <summary>The pop-up's shortcut hint line on or off; nothing happens when it is unchanged.</summary>
    public void SetShowHint(bool show, long nowMs)
    {
        var current = data.Overlays ?? OverlayLayout.Defaults([]);
        if (current.ShowHint == show) return;
        data = data with { Overlays = current with { ShowHint = show } };
        _writes.DataChanged(nowMs);
    }

    public EngineState State
    {
        get
        {
            var t = Current;
            var (c, sub, boost) = (data.Calibration, data.SubConnected, data.SsfBoostLimitDb);
            return new EngineState(CurrentTable, t, EqApoFile.AppliedLevels(t, c, sub, boost, NightMode),
                EqApoFile.Headroom(t, c, sub, boost), c, data.SsfLimitDb, boost,
                data.GlobalVolume, data.NightVolume, NightMode, _undo is not null, _book.All(),
                data.Overlays ?? OverlayLayout.Defaults([]), data.SoundOrDefault, data.DeviceId,
                Calibrating, data.DeviceName, data.SpeakerCheck, data.ToolWindow, data.ControlsOrDefault);
        }
    }

    /// <summary>One window gesture's "before": the table rows it touches (null = row didn't exist), or the settings.</summary>
    private sealed record UndoPoint(Dictionary<string, TableLevel?>? Rows,
        (GroupLevels Calibration, int Global, int Night, SoundSettings? Sound, ControlsSettings? Controls)? Settings);

    private UndoPoint? _undo;

    /// <summary>Before a window gesture on these tables (slider drag start, click). Undo restores only these rows.</summary>
    public void SaveUndoPoint(IEnumerable<string> tables) =>
        _undo = new UndoPoint(tables.Distinct().ToDictionary(n => n, n => _book.Find(n)), null);

    /// <summary>Before a window gesture on calibration / global / night levels / sound / controls. Undo restores only those.</summary>
    public void SaveSettingsUndoPoint() => _undo = new UndoPoint(null, SettingsNow());

    private (GroupLevels, int, int, SoundSettings?, ControlsSettings?) SettingsNow() =>
        (data.Calibration, data.GlobalVolume, data.NightVolume, data.Sound, data.Controls);

    /// <summary>One level; button and tray changes made since, elsewhere, stay.</summary>
    public void Undo(long nowMs)
    {
        if (_undo is not { } u) return;
        _undo = null;
        foreach (var (name, row) in u.Rows ?? [])
        {
            if (row is null) _book.Remove(name); else _book.Set(row);
        }
        if (u.Settings is { } s)
        {
            data = data with
            {
                Calibration = s.Calibration, GlobalVolume = s.Global, NightVolume = s.Night, Sound = s.Sound, Controls = s.Controls,
            };
        }
        ApplyVolume();
        _writes.IncludeChanged(nowMs);
        _writes.DataChanged(nowMs);
    }

    private void SetLevel(CabSoundData next, long nowMs)
    {
        if (next == data) return;
        var before = ActiveGlobal;
        data = next;
        if (ActiveGlobal != before) ApplyVolume();
        _writes.DataChanged(nowMs);
    }

    private void Store(IReadOnlyList<TableLevel> levels, long nowMs)
    {
        var changed = levels.Select(t => (Before: _book.Get(t.Name), After: t)).Where(x => x.Before != x.After).ToList();
        if (changed.Count == 0) return;
        foreach (var (_, after) in changed) _book.Set(after);
        _writes.DataChanged(nowMs);

        if (changed.FirstOrDefault(x => x.After.Name == CurrentTable) is { After: not null } cur)
        {
            if (cur.Before.Volume != cur.After.Volume) ApplyVolume();
            if (cur.Before.BackglassDb != cur.After.BackglassDb
                || cur.Before.FrontDb != cur.After.FrontDb
                || cur.Before.RearDb != cur.After.RearDb
                || cur.Before.SubDb != cur.After.SubDb
                || cur.Before.BackboxLowCut != cur.After.BackboxLowCut
                || cur.Before.ExciterLowCut != cur.After.ExciterLowCut
                || cur.Before.RearExciterLowCut != cur.After.RearExciterLowCut)
                _writes.IncludeChanged(nowMs);
        }
    }

    public void Tick(long nowMs) => Write(_writes.Due(nowMs));

    /// <summary>
    /// On exit: always write both files, so a write that failed earlier is repaired too. An open speaker
    /// check ends first, so the normal file and the table's volume are left behind, never the trial state.
    /// </summary>
    public void Flush()
    {
        if (Calibrating)
        {
            Calibrating = false;
            ApplyVolume();
        }
        _writes.TakeAll();
        Write((true, true));
    }

    public CabSoundData Snapshot() => data with { Tables = _book.All() };

    /// <summary>Table volume change per press, 1..10 (saved).</summary>
    public void SetVolumeStep(int step, long nowMs)
    {
        var clamped = Math.Clamp(step, 1, 10);
        if (clamped == data.VolumeStep) return;
        data = data with { VolumeStep = clamped };
        _writes.DataChanged(nowMs);
    }

    private void ChangeVolume(int direction, long nowMs, bool popup)
    {
        var before = Current.Volume;
        var t = _book.ChangeVolume(CurrentTable, direction * data.VolumeStep);
        if (t.Volume != before)
        {
            ApplyVolume();
            _writes.DataChanged(nowMs);
        }
        if (popup) output.ShowPopup(new PopupInfo(CurrentTable, "Table volume", $"{t.Volume}", false, t.Volume / 100.0));
    }

    /// <summary>Global keys change whichever level is active: Night in night mode, else Global.</summary>
    private void ChangeGlobal(int direction, long nowMs, bool popup)
    {
        var before = ActiveGlobal;
        var level = Math.Clamp(before + direction * data.VolumeStep, 0, 100);
        if (level != before)
        {
            data = NightMode ? data with { NightVolume = level } : data with { GlobalVolume = level };
            ApplyVolume();
            _writes.DataChanged(nowMs);
        }
        if (popup) output.ShowPopup(new PopupInfo(CurrentTable, NightMode ? "Night volume" : "Global volume", $"{level}", false, level / 100.0));
    }

    private void ToggleNightMode()
    {
        NightMode = !NightMode;
        ApplyVolume();
        output.WriteInclude(Include());
        output.ShowPopup(new PopupInfo(CurrentTable, "Night mode", NightMode ? "On" : "Off", false, ActiveGlobal / 100.0));
    }

    private int ActiveGlobal => NightMode ? data.NightVolume : data.GlobalVolume;

    /// <summary>Windows volume = table volume x active global level (PinVol's model).</summary>
    private void ApplyVolume()
    {
        if (Calibrating) return; // the volume belongs to the speaker check window's slider
        output.SetVolume((int)Math.Round(_book.Get(CurrentTable).Volume * ActiveGlobal / 100.0, MidpointRounding.AwayFromZero));
    }

    private void ChangeSsf(SsfGroup group, int delta, long nowMs, bool popup)
    {
        var (cut, boost) = (data.SsfLimitDb, data.SsfBoostLimitDb);
        var before = Current;
        var current = Own(before, group);
        var blocked = delta > 0 ? current >= boost : current <= -cut;
        var t = before;
        if (!blocked)
        {
            var pulled = Math.Clamp(current, -cut, boost);
            if (pulled != current) _book.Set(WithOwn(before, group, pulled));
            t = _book.ChangeSsf(CurrentTable, group, delta);
        }
        var value = Math.Clamp(Own(t, group), -cut, boost);
        var name = group switch
        {
            SsfGroup.Backglass => "Backbox",
            SsfGroup.Front => "Front exciters",
            _ => "Rear exciters",
        };
        var level = Math.Clamp((value + cut) / (double)(cut + boost), 0, 1);
        var atLimit = Own(t, group) <= -cut || Own(t, group) >= boost;
        if (popup) output.ShowPopup(new PopupInfo(CurrentTable, name, $"{value:+0;-0;0} dB", atLimit, level));
        if (t != before)
        {
            _writes.IncludeChanged(nowMs);
            _writes.DataChanged(nowMs);
        }
    }

    private static int Own(TableLevel t, SsfGroup group) => group switch
    {
        SsfGroup.Backglass => t.BackglassDb,
        SsfGroup.Front => t.FrontDb,
        SsfGroup.Rear => t.RearDb,
        _ => t.SubDb,
    };

    private static TableLevel WithOwn(TableLevel t, SsfGroup group, int db) => group switch
    {
        SsfGroup.Backglass => t with { BackglassDb = db },
        SsfGroup.Front => t with { FrontDb = db },
        SsfGroup.Rear => t with { RearDb = db },
        _ => t with { SubDb = db },
    };

    private void Write((bool Include, bool Data) due)
    {
        if (due.Include) output.WriteInclude(Include());
        if (due.Data) output.SaveData(Snapshot());
    }

    private static GroupLevels ClampCalibration(GroupLevels c)
    {
        static int Clamp(int v) => Math.Clamp(v, -30, 10);
        return new GroupLevels(Clamp(c.Backglass), Clamp(c.Rear), Clamp(c.Front), Clamp(c.Sub));
    }

    private string Include()
    {
        var sound = data.SoundOrDefault;
        if (Calibrating)
        {
            var boost = data.SsfBoostLimitDb;
            return EqApoFile.BuildCalibration(
                new GroupLevels(_trial.Backglass - boost, _trial.Rear - boost, _trial.Front - boost, 0),
                _trialExciterCut, sound, _trialBackboxCut);
        }
        var t = _book.Get(CurrentTable);
        var rearOverride = t.RearExciterLowCut ?? (sound.RearExciterCut is null ? t.ExciterLowCut : null);
        var withCuts = t with
        {
            BackboxLowCut = LowCutChoices.Effective(sound, LowCutGroup.Backbox, t.BackboxLowCut),
            ExciterLowCut = LowCutChoices.Effective(sound, LowCutGroup.Exciters, t.ExciterLowCut),
            RearExciterLowCut = LowCutChoices.Effective(sound, LowCutGroup.RearExciters, rearOverride),
        };
        return EqApoFile.Build(withCuts, data.Calibration, sound, data.SubConnected, data.SsfBoostLimitDb, NightMode);
    }
}
