using System.Text.Json;
using System.Text.Json.Serialization;

namespace CabSound.Core;

/// <summary>The last finished speaker check: its one-line verdict and the date ("yyyy-MM-dd").</summary>
public sealed record SpeakerCheckResult(string Summary, string CheckedOn);

/// <summary>Rotation (0 / 90 / 180 / 270) and top-left of the speaker check window.</summary>
public sealed record ToolWindowPlace(int Rotation, int X, int Y);

/// <summary>Pop-up position/size in screen pixels, and rotation (0/90/180/270) for a rotated cabinet screen.</summary>
public sealed record DisplaySettings(int X, int Y, int Width, int Height, int Rotation)
{
    public static DisplaySettings Default => new(0, 0, 400, 120, 0);
}

/// <summary>Read-only shape of old preset entries in CabSound.json, used only for one-time migration.</summary>
public sealed record LegacyPreset(string Name = "", int BackboxDb = 0, int FrontDb = 0, int RearDb = 0, int? BackboxCut = null, int? ExciterCut = null);

/// <summary>
/// Everything CabSound remembers: cabinet calibration, settings and per-table offsets.
/// DeviceId/DeviceName pick the USB card (ID first, name if the ID changed).
/// DeviceDriver = the card's driver at the last start (null until first seen); a change means "check the channels".
/// SwitchOverBackup = config.txt as it was before CabSound took over (kept; going back is done by hand).
/// VolumeStep = table volume change per press (1 precise ... 5 fast).
/// SsfLimitDb = how far a table can cut a group; SsfBoostLimitDb = how far it can boost one
/// (every dB of boost costs a dB of headroom for the whole cabinet).
/// GlobalVolume / NightVolume = PinVol's Global and Night levels (0..100): Windows volume =
/// table volume x the active one / 100.
/// Overlays = pop-up and menu placement (null until first saved; old files load as null).
/// Sound = cabinet-wide low cuts, 250 Hz dip and jack swap written into CabSound.txt (null in old files = defaults).
/// SpeakerCheck = the last finished speaker check (null until one was run).
/// </summary>
public sealed record CabSoundData(
    GroupLevels Calibration,
    bool SubConnected,
    int SsfLimitDb,
    List<TableLevel> Tables,
    string? DeviceId = null,
    string DeviceName = "USB Sound Device",
    DisplaySettings? Display = null,
    string? SwitchOverBackup = null,
    int VolumeStep = 1,
    int SsfBoostLimitDb = 6,
    int GlobalVolume = 100,
    int NightVolume = 100,
    OverlaySettings? Overlays = null,
    SoundSettings? Sound = null,
    string? DeviceDriver = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<LegacyPreset>? Presets = null,
    SpeakerCheckResult? SpeakerCheck = null,
    ToolWindowPlace? ToolWindow = null,
    ControlsSettings? Controls = null)
{
    public static CabSoundData Defaults() => new(new GroupLevels(0, 0, 0, 0), false, 10, []);

    [JsonIgnore]
    public DisplaySettings DisplayOrDefault => Display ?? DisplaySettings.Default;

    [JsonIgnore]
    public SoundSettings SoundOrDefault => Sound ?? new SoundSettings();

    [JsonIgnore]
    public ControlsSettings ControlsOrDefault => Controls ?? ControlsSettings.DefaultPinVol;
}

/// <summary>
/// One-time migration for CabSound.json files saved when presets existed: bakes each table's effective
/// levels (preset + own adjustment, clamped to limits) and low cuts into the table row, sets the cabinet
/// low cuts from the first preset, and removes the Presets list.
/// </summary>
public static class LegacyPresetMigration
{
    public static (CabSoundData Data, bool Migrated) Apply(CabSoundData data)
    {
        if (data.Presets is not { Count: > 0 } presets)
            return data.Presets is null ? (data, false) : (data with { Presets = null }, true);

        var cabBackboxCut = presets[0].BackboxCut;
        var cabExciterCut = presets[0].ExciterCut;
        var sound = data.SoundOrDefault with
        {
            LowCut = LowCutMode.Original,
            BackboxCut = cabBackboxCut,
            ExciterCut = cabExciterCut,
        };

        var tables = data.Tables.Select(t =>
        {
            var p = presets[t.Preset >= 0 && t.Preset < presets.Count ? t.Preset : 0];
            int Clamp(int v) => Math.Clamp(v, -data.SsfLimitDb, data.SsfBoostLimitDb);
            var effBackboxCut = t.BackboxLowCut ?? p.BackboxCut;
            var effExciterCut = t.ExciterLowCut ?? p.ExciterCut;
            return t with
            {
                BackglassDb = Clamp(p.BackboxDb + t.BackglassDb),
                FrontDb = Clamp(p.FrontDb + t.FrontDb),
                RearDb = Clamp(p.RearDb + t.RearDb),
                BackboxLowCut = effBackboxCut == cabBackboxCut ? null : effBackboxCut,
                ExciterLowCut = effExciterCut == cabExciterCut ? null : effExciterCut,
                Preset = 0,
            };
        }).ToList();

        return (data with { Sound = sound, Tables = tables, Presets = null }, true);
    }
}

/// <summary>
/// CabSound.json, kept next to the .exe (the App picks the folder). Readable JSON, saved with
/// SafeFileWriter so a crash mid-save can't corrupt it. Missing file = defaults; unreadable file
/// throws (never silently replaced, so no levels are lost).
/// </summary>
public static class CabSoundDataFile
{
    public const string FileName = "CabSound.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static CabSoundData Load(string path)
    {
        if (!File.Exists(path)) return CabSoundData.Defaults();
        try
        {
            return JsonSerializer.Deserialize<CabSoundData>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException($"{path} is empty");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path} can't be read: {e.Message}", e);
        }
    }

    public static void Save(string path, CabSoundData data) =>
        SafeFileWriter.Write(path, JsonSerializer.Serialize(data, Options));
}
