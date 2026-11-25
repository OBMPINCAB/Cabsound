using CabSound.Core;
using Microsoft.Win32;

namespace CabSound.Platform;

/// <summary>
/// Is Equalizer APO installed, and switched on for the given sound card? (A Windows update can
/// silently switch it off.) Reads the registry only; <see cref="EqApoHealth"/> decides.
/// </summary>
public static class EqApoCheck
{
    public const string ConfiguratorPath = @"C:\Program Files\EqualizerAPO\Configurator.exe";
    private const string RenderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

    public static EqApoStatus Status(string deviceId)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var apo = hklm.OpenSubKey(@"SOFTWARE\EqualizerAPO");

        using var fx = hklm.OpenSubKey($@"{RenderKey}\{EqApoSetup.EndpointGuid(deviceId)}\FxProperties");
        var values = fx?.GetValueNames().ToDictionary(n => n, n => fx.GetValue(n), StringComparer.OrdinalIgnoreCase);
        return EqApoHealth.Status(installed: apo is not null, values);
    }
}
