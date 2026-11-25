namespace CabSound.Core;

/// <summary>Brings PinVol's on-screen display position and rotation over (PinVolSettings.ini).</summary>
public static class PinVolSettingsImport
{
    /// <summary>"OSDPos = x,y,w,h" and "OSDRotation = r"; null when OSDPos is missing or malformed.</summary>
    public static DisplaySettings? Display(string pinVolSettingsText)
    {
        var ini = KeyValueIniFile.Parse(pinVolSettingsText);
        if (!ini.TryGetValue("OSDPos", out var pos)) return null;

        var parts = pos.Split(',');
        var n = new int[4];
        if (parts.Length != 4 || !parts.Select((p, i) => int.TryParse(p.Trim(), out n[i])).All(ok => ok)) return null;

        var rotation = ini.TryGetValue("OSDRotation", out var r) && int.TryParse(r, out var rot) ? rot : 0;
        // PinVol turns counter-clockwise, CabSound (WPF) clockwise
        return new DisplaySettings(n[0], n[1], n[2], n[3], (360 - rotation % 360) % 360);
    }
}
