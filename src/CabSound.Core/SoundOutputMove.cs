namespace CabSound.Core;

/// <summary>Moves sounds between Table output (exciters + shaker) and Backglass output (backbox).
/// Backbox sounds do not drive the sub/shaker (owner, 2026-11-14): impact effects belong on the table.</summary>
public static class SoundOutputMove
{
    /// <summary>Sounds matching the patterns that are not yet on <paramref name="to"/>, with that output.</summary>
    public static IReadOnlyList<(string Stream, VpxSound Sound)> Plan(
        IEnumerable<(string Stream, VpxSound Sound)> sounds, IReadOnlyList<string> patterns, SoundOutput to) =>
        sounds.Where(s => s.Sound.Output != to && VpxSound.NameMatches(s.Sound.Name, patterns))
              .Select(s => (s.Stream, s.Sound.WithOutput(to)))
              .ToList();

    /// <summary>Patterns that match no sound in the table.</summary>
    public static IReadOnlyList<string> Unmatched(IEnumerable<(string Stream, VpxSound Sound)> sounds, IReadOnlyList<string> patterns)
    {
        var names = sounds.Select(s => s.Sound.Name).ToList();
        return patterns.Where(p => !names.Any(n => VpxSound.NameMatches(n, [p]))).ToList();
    }
}
