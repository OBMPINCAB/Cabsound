using System.Management;
using CabSound.Core;
using NAudio.CoreAudioApi;

namespace CabSound.Platform;

/// <summary>
/// The cabinet's USB sound card, found by endpoint ID (stable) or, if Windows gave it a new ID
/// (e.g. after a USB port change), by name. Choice rules: SoundCardChoice.
/// </summary>
public static class AudioDevice
{
    public static MMDevice? Find(string? id, string nameHint)
    {
        var active = Active();
        try
        {
            var picked = SoundCardChoice.Pick(active.Select(d => (d.ID, d.FriendlyName)).ToList(), id, nameHint);
            MMDevice? match = null;
            foreach (var d in active)
            {
                if (match is null && d.ID == picked) match = d;
                else d.Dispose();
            }
            return match;
        }
        catch
        {
            foreach (var d in active) d.Dispose();
            throw;
        }
    }

    /// <summary>Every output Windows can play on right now, for the owner to pick the card from.</summary>
    public static List<(string Id, string Name)> Outputs()
    {
        var active = Active();
        try { return active.Select(d => (d.ID, d.FriendlyName)).ToList(); }
        finally { foreach (var d in active) d.Dispose(); }
    }

    /// <summary>
    /// The card's driver as "provider version" (e.g. "C-Media Inc. 10.0.8.2167"), or null when Windows
    /// doesn't tell. Only compared with the last start's text (ChannelCheckAdvice).
    /// </summary>
    public static string? Driver(MMDevice device)
    {
        try
        {
            var card = SoundCardChoice.NameHint(device.FriendlyName);
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DriverProviderName, DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceClass = 'MEDIA'");
            using var results = searcher.Get();
            foreach (var driver in results)
            {
                using (driver)
                {
                    if (string.Equals((driver["DeviceName"] as string)?.Trim(), card, StringComparison.OrdinalIgnoreCase))
                        return $"{driver["DriverProviderName"]} {driver["DriverVersion"]}".Trim();
                }
            }
            return null;
        }
        catch (Exception e) when (e is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void SetVolume(MMDevice device, int percent) =>
        device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;

    private static List<MMDevice> Active()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
    }
}
