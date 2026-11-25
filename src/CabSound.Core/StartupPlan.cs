namespace CabSound.Core;

public enum StartupStep { AddCabSound, RemoveCabSound, ParkPinVol, RestorePinVol }

/// <summary>
/// "Start with Windows": which Startup-folder shortcuts to add, remove or move. PinVol's shortcut
/// is parked in CabSound's backups (not deleted) so it can be put back. File names only.
/// </summary>
public static class StartupPlan
{
    public const string CabSoundLink = "CabSound.lnk";

    public static bool IsEnabled(IEnumerable<string> startupFiles) => startupFiles.Contains(CabSoundLink);

    public static List<StartupStep> Enable(IEnumerable<string> startupFiles, IEnumerable<string> parkedFiles)
    {
        var startup = startupFiles.ToList();
        var steps = new List<StartupStep>();
        if (startup.Any(IsPinVol)) steps.Add(StartupStep.ParkPinVol);
        if (!startup.Contains(CabSoundLink)) steps.Add(StartupStep.AddCabSound);
        return steps;
    }

    public static List<StartupStep> Disable(IEnumerable<string> startupFiles, IEnumerable<string> parkedFiles)
    {
        var startup = startupFiles.ToList();
        var steps = new List<StartupStep>();
        if (startup.Contains(CabSoundLink)) steps.Add(StartupStep.RemoveCabSound);
        if (parkedFiles.Any(IsPinVol) && !startup.Any(IsPinVol)) steps.Add(StartupStep.RestorePinVol);
        return steps;
    }

    public static bool IsPinVol(string fileName) => fileName.Contains("PinVol", StringComparison.OrdinalIgnoreCase);
}
