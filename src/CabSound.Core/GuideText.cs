namespace CabSound.Core;

public enum GuideTopic { TableTuning, AmpSetup }

public sealed record GuideStep(string Title, string Action, bool Highlight = false);

/// <summary>
/// Concise tuning &amp; debug checklists shared between the in-cabinet overlay Guide page and the desktop Sound tab.
/// Kept to 6 rows per topic so every line fits inside the rotated 900x700 playfield overlay at full font size.
/// </summary>
public static class GuideText
{
    private static readonly IReadOnlyList<GuideStep> TableSteps =
    [
        new("1. Overall loudness", "Table Vol first · keep dB sliders near 0"),
        new("2. Music too loud?", "Lower ROM vol (End → 8/9) · BB dB drops Sub!"),
        new("3. Front vs Rear", "Equal or Rear +1..2 dB (Front > Rear over-shakes)"),
        new("4. Weak shaker?", "Both Exciter Cuts 40–50 Hz (90+ kills it)", Highlight: true),
        new("5. Muddy or buzz?", "Mud → lower Sub Freq · Buzz → Cut 60 Hz"),
        new("6. Still no shaker?", "Night Mode Off · check silent/DOF-only WAV"),
    ];

    private static readonly IReadOnlyList<GuideStep> AmpSteps =
    [
        new("1. Sound tab base", "Cuts: BB 40 · Fr 40 · Rr 50 Hz | Bal: 0 dB"),
        new("2. Amp crossovers", "Switch: 7.1 · Sub ~85 Hz · Shaker ~65 Hz", Highlight: true),
        new("3. Exciters first", "BB/Sub/Shk low · set Front, Back slightly +"),
        new("4. Shaker second", "Raise Shaker Vol till bumpers thump cleanly"),
        new("5. Backbox third", "Raise Speaker Vol to match flipper snap"),
        new("6. Punchy Sub last", "Raise Sub Vol for kick snap (boomy = lower Hz)"),
    ];

    public static string TopicLabel(GuideTopic topic) => topic switch
    {
        GuideTopic.TableTuning => "◄ Table Tuning ►",
        _ => "◄ Initial & Amp Setup ►",
    };

    public static IReadOnlyList<GuideStep> Steps(GuideTopic topic) =>
        topic == GuideTopic.TableTuning ? TableSteps : AmpSteps;
}
