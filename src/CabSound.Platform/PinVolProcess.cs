using System.Diagnostics;

namespace CabSound.Platform;

/// <summary>Finds, closes and starts PinVol (only needed while CabSound replaces it).</summary>
public static class PinVolProcess
{
    public static Process? Running() => Process.GetProcessesByName("PinVol").FirstOrDefault();

    public static string? ExePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch { return null; }
    }

    /// <summary>Asks PinVol to close; kills it if it is still running after 3 s.</summary>
    public static void Close(Process p)
    {
        if (p.CloseMainWindow() && p.WaitForExit(3000)) return;
        if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
    }

    public static void Start(string exePath) =>
        Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = Path.GetDirectoryName(exePath), UseShellExecute = true });
}
