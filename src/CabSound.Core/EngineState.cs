namespace CabSound.Core;

/// <summary>
/// Everything the CabSound window shows, taken on the engine thread after each change.
/// Applied = dB actually written for the running table (calibration + offset - headroom).
/// Calibrating = speaker check window is open (trial levels are written, keys do nothing).
/// SpeakerCheck = the last finished speaker check (null = never run).
/// </summary>
public sealed record EngineState(
    string Table,
    TableLevel Levels,
    GroupLevels Applied,
    int HeadroomDb,
    GroupLevels Calibration,
    int CutLimitDb,
    int BoostLimitDb,
    int GlobalVolume,
    int NightVolume,
    bool NightMode,
    bool CanUndo,
    IReadOnlyList<TableLevel> Tables,
    OverlaySettings Overlays,
    SoundSettings Sound,
    string? SoundCardId,
    bool Calibrating,
    string SoundCardName,
    SpeakerCheckResult? SpeakerCheck = null,
    ToolWindowPlace? ToolWindow = null,
    ControlsSettings? Controls = null)
{
    public ControlsSettings ControlsOrDefault => Controls ?? ControlsSettings.DefaultPinVol;
    /// <summary>A table is running (anything other than System or PinUP Popper).</summary>
    public bool TableRunning => Table is not (PinVolImport.SystemRow or CabSoundEngine.PopperRow);

    /// <summary>Whether a group's table offset has reached or passed a limit.</summary>
    public bool AtLimit(TableLevel t, SsfGroup group)
    {
        var db = group switch
        {
            SsfGroup.Backglass => t.BackglassDb,
            SsfGroup.Front => t.FrontDb,
            SsfGroup.Rear => t.RearDb,
            _ => t.SubDb,
        };
        return group != SsfGroup.Sub && (db <= -CutLimitDb || db >= BoostLimitDb);
    }
}

/// <summary>A table value the window changes.</summary>
public enum LevelField { Volume, Backglass, Rear, Front }
