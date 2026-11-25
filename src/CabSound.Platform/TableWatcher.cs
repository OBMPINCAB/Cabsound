using System.Diagnostics;
using System.Management;
using CabSound.Core;

namespace CabSound.Platform;

/// <summary>Finds which table is running by reading VPX / Future Pinball / Popper command lines.</summary>
public static class TableWatcher
{
    private const string Query =
        "SELECT Name, CommandLine FROM Win32_Process WHERE Name LIKE 'VPinballX%' " +
        "OR Name = 'Future Pinball.exe' OR Name = 'PinUpMenu.exe'";

    private static int[]? _lastPids;
    private static string _lastKey = "System";

    /// <summary>
    /// Current key: a running table wins over Popper; "System" when nothing is running.
    /// Caches the matching PIDs so WMI is only queried when a pinball process starts or exits.
    /// </summary>
    public static string CurrentKey()
    {
        var pids = MatchingPids();
        if (_lastPids is not null && pids.SequenceEqual(_lastPids)) return _lastKey;

        if (pids.Length == 0)
        {
            _lastPids = pids;
            _lastKey = "System";
            return _lastKey;
        }

        var keys = new List<string>();
        using var searcher = new ManagementObjectSearcher(Query);
        using var results = searcher.Get();
        foreach (var p in results)
        {
            using (p)
            {
                var key = TableKeyResolver.FromProcess(
                    p["Name"]?.ToString() ?? "", p["CommandLine"]?.ToString() ?? "");
                if (key is not null) keys.Add(key);
            }
        }

        _lastPids = pids;
        _lastKey = keys.FirstOrDefault(k => k.StartsWith("VP.") || k.StartsWith("FP."))
            ?? keys.FirstOrDefault(k => k != "PinUP Popper")
            ?? keys.FirstOrDefault()
            ?? "System";
        return _lastKey;
    }

    private static int[] MatchingPids()
    {
        var pids = new List<int>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                var name = p.ProcessName;
                if (name.StartsWith("VPinballX", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Future Pinball", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("PinUpMenu", StringComparison.OrdinalIgnoreCase))
                    pids.Add(p.Id);
            }
        }
        pids.Sort();
        return [.. pids];
    }
}
