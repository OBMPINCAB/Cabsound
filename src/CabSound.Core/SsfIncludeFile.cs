namespace CabSound.Core;

/// <summary>
/// Builds the Equalizer APO include file (same format PinVol writes to PinVolSSF.txt),
/// so config.txt keeps working unchanged.
/// </summary>
public static class SsfIncludeFile
{
    public static string Build(TableLevel t) =>
        "# Updated by CabSound Automatically -- do not edit\r\n" +
        "Stage: post-mix\r\n" +
        $"Channel: L R\r\nPreamp: {t.BackglassDb} DB\r\n" +
        $"Channel: RL RR\r\nPreamp: {t.RearDb} DB\r\n" +
        $"Channel: SL SR\r\nPreamp: {t.FrontDb} DB\r\n";
}
