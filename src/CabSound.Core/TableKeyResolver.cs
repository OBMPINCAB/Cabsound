using System.Text.RegularExpressions;

namespace CabSound.Core;

/// <summary>
/// Works out the PinVol-style table key from a running program's name and command line.
/// VPX: "VP." + table file name; Future Pinball: "FP." + table file name.
/// </summary>
public static class TableKeyResolver
{
    private static readonly Regex Token = new("\"([^\"]*)\"|(\\S+)");

    public static string? FromProcess(string exeName, string commandLine)
    {
        var exe = exeName.ToLowerInvariant();

        if (exe.StartsWith("vpinballx"))
            return Key("VP.", FindFile(commandLine, ".vpx")) ?? "Visual Pinball";

        if (exe == "future pinball.exe")
            return Key("FP.", FindFile(commandLine, ".fpt")) ?? "Future Pinball";

        if (exe == "pinupmenu.exe")
            return "PinUP Popper";

        return null;
    }

    private static string? Key(string prefix, string? file) =>
        file is null ? null : prefix + Path.GetFileNameWithoutExtension(file.Replace('\\', '/').Split('/')[^1]);

    private static string? FindFile(string commandLine, string extension)
    {
        foreach (Match m in Token.Matches(commandLine))
        {
            var value = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return null;
    }
}
