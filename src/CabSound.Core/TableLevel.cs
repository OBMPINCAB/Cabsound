using System.Text.Json.Serialization;

namespace CabSound.Core;

/// <summary>
/// One table's levels. The first six fields match a row of PinVolTables.ini
/// (Volume2 is PinVol's second level column, kept as-is so nothing is lost on import).
/// SSF levels are in dB: Backglass = backbox, Front = exciters near the player, Rear = exciters near
/// the backbox. Which channels those are is decided in EqApoFile (Front = RL RR, as VPX sends them).
/// SubDb is new in CabSound (sub + bass shaker); PinVol has no equivalent, so imports start at 0.
/// BackboxLowCut / ExciterLowCut / RearExciterLowCut: this table's own low cut (see LowCutChoices); null = cabinet's cut.
/// Preset is only read from legacy CabSound.json files during one-time migration and omitted once 0.
/// </summary>
public sealed record TableLevel(
    string Name,
    int Volume,
    int Volume2,
    int BackglassDb,
    int RearDb,
    int FrontDb,
    int SubDb = 0,
    int? BackboxLowCut = null,
    int? ExciterLowCut = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Preset = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RearExciterLowCut = null);
