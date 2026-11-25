using System;
using System.IO;
using CabSound.Core;

namespace CabSound.App;

/// <summary>Where things live. CabSound's own data sits next to the exe.</summary>
public static class Paths
{
    public static string DataDir => AppContext.BaseDirectory;
    public static string DataFile => Path.Combine(DataDir, CabSoundDataFile.FileName);
    public static string Backups => Path.Combine(DataDir, "backups");
    public static string ParkedStartup => Path.Combine(Backups, "startup");

    public const string EqApoConfigDir = @"C:\Program Files\EqualizerAPO\config";
    public static string EqApoConfig => Path.Combine(EqApoConfigDir, "config.txt");
    public static string Include => Path.Combine(EqApoConfigDir, EqApoFile.FileName);

    /// <summary>PinVol folders on the Desktop (PinVol is usually unzipped there as "PinVol-yyyymmdd"), newest first.</summary>
    public static string[] PinVolDesktopDirs()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!Directory.Exists(desktop)) return [];
        var dirs = Directory.GetDirectories(desktop, "PinVol*");
        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        Array.Reverse(dirs);
        return dirs;
    }
}
