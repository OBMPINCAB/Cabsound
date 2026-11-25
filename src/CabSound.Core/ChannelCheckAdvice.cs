namespace CabSound.Core;

/// <summary>
/// A new sound-card driver (or a different card) can move the Side and Rear jacks: on 2026-11-10 a
/// Windows update swapped them, on 2026-11-11 the C-Media driver replaced Microsoft's. CabSound can't
/// hear which jack is which, so at start-up it tells the owner to run the speaker check (Sound tab).
/// </summary>
public static class ChannelCheckAdvice
{
    /// <summary>What to tell the owner, or null when nothing changed (or the driver isn't known yet / can't be read).</summary>
    public static string? For(string? savedDriver, string? currentDriver, bool cardChanged)
    {
        var driverChanged = savedDriver is not null && currentDriver is not null && savedDriver != currentDriver;
        if (!driverChanged && !cardChanged) return null;

        var what = driverChanged
            ? $"The sound card's driver changed:\n\n  was: {savedDriver}\n  now: {currentDriver}"
            : "CabSound is using a different sound card output than last time.";
        return what + "\n\nThe Side and Rear jacks may have moved. Open the Sound tab and press " +
               "\"Check the speakers…\": it tells you if the jack swap must change.";
    }
}
