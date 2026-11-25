namespace CabSound.Core;

/// <summary>One program's own volume in Windows' volume mixer, on the sound card.</summary>
public record ProgramVolume(string Program, int Percent, bool Muted);

/// <summary>
/// Windows keeps a volume per program per sound card (volume mixer) and remembers it. A pinball
/// program left at 0% there is silent while everything else plays (cabinet 2026-11-12: Visual Pinball
/// at 0%, Popper fine). CabSound's table volume is the card's volume, so the pinball programs
/// must stay at 100% and unmuted. Read and set by the Platform's ProgramVolumeFix.
/// </summary>
public static class ProgramVolumes
{
    /// <summary>Process names (no ".exe") start with one of these: Visual Pinball, Future Pinball, PinUP.</summary>
    private static readonly string[] Prefixes = ["VPinballX", "Future Pinball", "PinUp"];

    public static bool IsPinball(string process) =>
        Prefixes.Any(p => process.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>The pinball programs that are turned down or muted.</summary>
    public static List<ProgramVolume> ToFix(IEnumerable<ProgramVolume> programs) =>
        programs.Where(p => IsPinball(p.Program) && (p.Muted || p.Percent < 100)).ToList();

    /// <summary>What to tell the owner (log and tray) after the fix.</summary>
    public static string Message(IReadOnlyList<ProgramVolume> wereWrong) =>
        "Windows' volume mixer had "
        + string.Join(", ", wereWrong.Select(p => $"{p.Program} at {p.Percent}%{(p.Muted ? " (muted)" : "")}"))
        + ". Set back to 100%.";
}
