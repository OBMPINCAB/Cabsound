using System.Text;

namespace CabSound.Core;

/// <summary>Reads and writes PinVol's tab-separated PinVolTables.ini format.</summary>
public static class PinVolTablesFile
{
    public static List<TableLevel> Parse(string text)
    {
        var result = new List<TableLevel>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var f = line.Split('\t');
            if (f.Length != 6) continue;
            if (!TryInts(f, out var n)) continue;

            result.Add(new TableLevel(f[0], n[0], n[1], n[2], n[3], n[4]));
        }
        return result;
    }

    public static string Format(IEnumerable<TableLevel> tables)
    {
        var sb = new StringBuilder();
        sb.Append("# CabSound volume levels list (PinVol format)\r\n");
        sb.Append($"# Saved {DateTime.Now}\r\n\r\n");
        foreach (var t in tables)
            sb.Append($"{t.Name}\t{t.Volume}\t{t.Volume2}\t{t.BackglassDb}\t{t.RearDb}\t{t.FrontDb}\r\n");
        return sb.ToString();
    }

    private static bool TryInts(string[] f, out int[] n)
    {
        n = new int[5];
        for (var i = 0; i < 5; i++)
            if (!int.TryParse(f[i + 1], out n[i])) return false;
        return true;
    }
}
