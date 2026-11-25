namespace CabSound.Core;

/// <summary>After patching a .vpx: did exactly the intended streams change? Inputs are stream path -> content hash.</summary>
public static class VpxPatchCheck
{
    public static IReadOnlyList<string> Problems(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, IReadOnlyCollection<string> intended)
    {
        var problems = new List<string>();
        foreach (var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            var had = before.TryGetValue(path, out var b);
            var has = after.TryGetValue(path, out var a);
            if (!has) problems.Add("removed: " + path);
            else if (!had) problems.Add("added: " + path);
            else if (intended.Contains(path) && a == b) problems.Add("not changed: " + path);
            else if (!intended.Contains(path) && a != b) problems.Add("changed: " + path);
        }
        return problems;
    }
}
