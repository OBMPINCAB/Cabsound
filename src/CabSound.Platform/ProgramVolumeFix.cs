using System.Diagnostics;
using CabSound.Core;
using NAudio.CoreAudioApi;

namespace CabSound.Platform;

/// <summary>
/// Windows' volume mixer on the sound card: puts turned-down or muted pinball programs back to 100%.
/// A program only shows up there once it has opened the card, so this is called again and again.
/// Which programs and when: <see cref="ProgramVolumes"/>.
/// </summary>
public static class ProgramVolumeFix
{
    /// <returns>The programs that were wrong, as they were before the fix; empty when all was well.</returns>
    public static List<ProgramVolume> Run(MMDevice device)
    {
        var manager = device.AudioSessionManager;
        manager.RefreshSessions(); // the list is read once per device otherwise
        var sessions = manager.Sessions;

        var found = new List<(ProgramVolume Program, SimpleAudioVolume Volume)>();
        for (var i = 0; i < sessions.Count; i++)
        {
            var volume = sessions[i].SimpleAudioVolume;
            var name = ProcessName((int)sessions[i].GetProcessID);
            found.Add((new ProgramVolume(name, (int)Math.Round(volume.Volume * 100), volume.Mute), volume));
        }

        var wrong = ProgramVolumes.ToFix(found.Select(f => f.Program));
        foreach (var (program, volume) in found)
        {
            if (!wrong.Contains(program)) continue;
            volume.Volume = 1f;
            volume.Mute = false;
        }
        return wrong;
    }

    /// <summary>"" when the process is gone, elevated/inaccessible, or Windows' own (pid 0).</summary>
    private static string ProcessName(int pid)
    {
        if (pid == 0) return "";
        try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { return ""; }
    }
}
