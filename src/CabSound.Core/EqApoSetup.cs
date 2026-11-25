using System.Text.RegularExpressions;

namespace CabSound.Core;

public enum EqApoSetupAction { OpenDownloadPage, OpenConfigurator, TurnOnEnhancements }

public sealed record EqApoSetupStep(string Title, string Text, string Button, EqApoSetupAction Action);

/// <summary>
/// First-run Equalizer APO setup: what is missing and what the setup window offers to do about it.
/// CabSound turns Windows' audio enhancements back on itself (elevated helper run of CabSound.exe);
/// installing Equalizer APO and ticking the card in its Configurator stay with the user.
/// </summary>
public static partial class EqApoSetup
{
    public const string DownloadPage = "https://sourceforge.net/projects/equalizerapo/";
    public const string FixEnhancementsArg = "--fix-enhancements";

    public static EqApoSetupStep? StepFor(EqApoStatus status, string cardName) => status switch
    {
        EqApoStatus.NotInstalled => new(
            "Install Equalizer APO",
            "CabSound sets table levels and low cuts through Equalizer APO (free). It isn't installed.\n\n"
            + "1. Click Open download page and install Equalizer APO.\n"
            + $"2. In the Configurator that opens at the end, tick only \"{cardName}\".\n"
            + "3. Restart Windows, then start CabSound again.",
            "Open download page", EqApoSetupAction.OpenDownloadPage),
        EqApoStatus.NotOnDevice => new(
            "Turn on Equalizer APO for the sound card",
            $"Equalizer APO is installed but not active on \"{cardName}\".\n\n"
            + "1. Click Open Configurator.\n"
            + $"2. On the Playback devices tab, tick \"{cardName}\" and click OK.\n"
            + "3. Restart Windows, then start CabSound again.",
            "Open Configurator", EqApoSetupAction.OpenConfigurator),
        EqApoStatus.EnhancementsOff => new(
            "Turn on Windows audio enhancements",
            $"Windows has audio enhancements switched off for \"{cardName}\", so Equalizer APO is bypassed: "
            + "levels and low cuts do nothing.\n\n"
            + "Click Fix it: Windows asks for administrator rights once, CabSound backs up the setting "
            + "and switches enhancements back on.",
            "Fix it", EqApoSetupAction.TurnOnEnhancements),
        _ => null,
    };

    /// <summary>Command line for the elevated helper run that turns enhancements on for one card.</summary>
    public static string[] FixEnhancementsArgs(string deviceId) => [FixEnhancementsArg, deviceId];

    /// <summary>The card to fix when CabSound was started as the helper; null otherwise or for a bad id.</summary>
    public static string? DeviceToFix(IReadOnlyList<string> args) =>
        args is [FixEnhancementsArg, var id] && EndpointId().IsMatch(id) ? id : null;

    /// <summary>Endpoint ID "{0.0.0.00000000}.{guid}" → "{guid}", the card's registry key name.</summary>
    public static string EndpointGuid(string deviceId) => deviceId[(deviceId.LastIndexOf('.') + 1)..];

    [GeneratedRegex(@"^\{[0-9.]+\}\.\{[0-9A-Fa-f-]{36}\}$")]
    private static partial Regex EndpointId();
}
