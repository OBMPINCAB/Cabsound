namespace CabSound.Core;

public enum EqApoStatus { NotInstalled, NotOnDevice, EnhancementsOff, Active }

/// <summary>
/// Does Equalizer APO reach the sound? Decided from the sound card's "FxProperties" registry values
/// (read by the Platform's EqApoCheck). Registered is not enough: with Windows' "audio enhancements"
/// off for the card, Windows loads no effect at all and CabSound.txt does nothing (cabinet 2026-11-12).
/// </summary>
public static class EqApoHealth
{
    private const string ApoClsid = "{EC1CC9CE-FAED-4822-828A-82A81A6F018F}";
    /// <summary>Windows' "disable all enhancements" switch for one output (1 = effects bypassed).</summary>
    public const string EnhancementsOffValue ="{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";

    /// <param name="fxProperties">The card's FxProperties values by name; null when the key is missing.</param>
    public static EqApoStatus Status(bool installed, IReadOnlyDictionary<string, object?>? fxProperties)
    {
        if (!installed) return EqApoStatus.NotInstalled;
        if (fxProperties is null || !fxProperties.Values.Any(IsApo)) return EqApoStatus.NotOnDevice;

        var off = fxProperties.Any(p =>
            p.Key.Equals(EnhancementsOffValue, StringComparison.OrdinalIgnoreCase) && p.Value is int and not 0);
        return off ? EqApoStatus.EnhancementsOff : EqApoStatus.Active;
    }

    /// <summary>What to tell the owner; null when all is well.</summary>
    public static string? Warning(EqApoStatus status) => status switch
    {
        EqApoStatus.NotInstalled =>
            "Equalizer APO isn't installed. Levels won't apply until it is (then restart CabSound).",
        EqApoStatus.NotOnDevice =>
            "Equalizer APO isn't active on the sound card. Levels won't apply. Right-click → Fix Equalizer APO.",
        EqApoStatus.EnhancementsOff =>
            "Windows has audio enhancements switched off for the sound card, so levels and low cuts won't apply. "
            + "Windows Settings → System → Sound → the sound card → Audio enhancements: Device Default Effects.",
        _ => null,
    };

    private static bool IsApo(object? value) => value switch
    {
        string s => s.Contains(ApoClsid, StringComparison.OrdinalIgnoreCase),
        string[] list => list.Any(s => s.Contains(ApoClsid, StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };
}
