using System.Security.Cryptography;
using System.Text;
using CabSound.Core;
using OpenMcdf;

/// <summary>.vpx file access (OLE compound file). Logic lives in CabSound.Core; this only reads and writes streams.</summary>
internal static class VpxFile
{
    public sealed record Contents(IReadOnlyList<(string Stream, VpxSound Sound)> Sounds, string? EmbeddedScript);

    public static Contents Read(string path)
    {
        var cf = new CompoundFile(path, CFSUpdateMode.ReadOnly, CFSConfiguration.Default);
        try
        {
            var g = cf.RootStorage.GetStorage("GameStg");
            var sounds = new List<(string, VpxSound)>();
            for (var i = 0; ; i++)
            {
                var name = "Sound" + i;
                var s = TryStream(g, name);
                if (s is null) break;
                sounds.Add(("GameStg/" + name, VpxSound.Parse(s.GetData())));
            }
            var gameData = TryStream(g, "GameData");
            return new(sounds, gameData is null ? null : VpxGameData.ReadScript(gameData.GetData()));
        }
        finally { cf.Close(); }
    }

    /// <summary>The sidecar next to the table (VPX runs it instead of the embedded script), or null.</summary>
    public static string? SidecarPath(string table)
    {
        var p = Path.ChangeExtension(table, ".vbs");
        return File.Exists(p) ? p : null;
    }

    public static string ReadText(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));

    /// <summary>Writes a copy with the given sound streams replaced. Returns the replaced stream paths.</summary>
    public static void WriteWithSounds(string source, string target, IReadOnlyDictionary<string, VpxSound> replace)
    {
        var cf = new CompoundFile(source, CFSUpdateMode.ReadOnly, CFSConfiguration.Default);
        try
        {
            var g = cf.RootStorage.GetStorage("GameStg");
            foreach (var (stream, sound) in replace) g.GetStream(stream["GameStg/".Length..]).SetData(sound.Write());
            cf.SaveAs(target);
        }
        finally { cf.Close(); }
    }

    /// <summary>Stream path -> SHA-256 of its content, for every stream in the file.</summary>
    public static Dictionary<string, string> StreamHashes(string path)
    {
        var d = new Dictionary<string, string>();
        var cf = new CompoundFile(path, CFSUpdateMode.ReadOnly, CFSConfiguration.Default);
        try
        {
            void Walk(CFStorage s, string prefix) => s.VisitEntries(e =>
            {
                if (e is CFStorage st) Walk(st, prefix + e.Name + "/");
                else d[prefix + e.Name] = Convert.ToHexString(SHA256.HashData(((CFStream)e).GetData()));
            }, false);
            Walk(cf.RootStorage, "");
        }
        finally { cf.Close(); }
        return d;
    }

    public static string FileHash(string path)
    {
        using var f = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(f));
    }

    private static CFStream? TryStream(CFStorage s, string name)
    {
        try { return s.GetStream(name); }
        catch (CFItemNotFound) { return null; }
    }
}
