namespace CabSound.Core;

/// <summary>Reads PinVol's "key = value" files (PinVolSettings.ini, PinVolVol.ini).</summary>
public static class KeyValueIniFile
{
    public static Dictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            result[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return result;
    }
}
