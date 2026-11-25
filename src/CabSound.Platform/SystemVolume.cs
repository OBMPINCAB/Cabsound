using NAudio.CoreAudioApi;

namespace CabSound.Platform;

/// <summary>Windows master volume of the default output (your USB Sound Device), 0-100.</summary>
public static class SystemVolume
{
    public static string DeviceName()
    {
        using var d = Device();
        return d.FriendlyName;
    }

    public static int Get()
    {
        using var d = Device();
        return (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
    }

    public static void Set(int percent)
    {
        using var d = Device();
        d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;
    }

    private static MMDevice Device()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }
}
