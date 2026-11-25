namespace CabSound.Core;

/// <summary>Finding and naming tables for the window. Keys look like "VP.Name" (VPX) or "FP.Name" (Future Pinball).</summary>
public static class TableSearch
{
    /// <summary>Case-insensitive "contains" on the name without its VP./FP. prefix; empty text = all. Sorted by display name.</summary>
    public static IReadOnlyList<TableLevel> Filter(IEnumerable<TableLevel> tables, string text)
    {
        var q = text.Trim();
        return tables
            .Where(t => q.Length == 0 || DisplayName(t.Name).Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => DisplayName(t.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string DisplayName(string key) =>
        key.StartsWith("VP.") || key.StartsWith("FP.") ? key[3..] : key;

    public static string Source(string key) => key switch
    {
        _ when key.StartsWith("VP.") => "VPX",
        _ when key.StartsWith("FP.") => "Future Pinball",
        "PinUP Popper" => "Popper",
        _ => "System",
    };
}
