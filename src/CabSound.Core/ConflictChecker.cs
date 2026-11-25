namespace CabSound.Core;

/// <summary>An input another program already uses (e.g. VPX left flipper).</summary>
public sealed record ReservedInput(InputId Input, string Owner, string Name);

/// <summary>One row of Popper's PinUPFunctions table. CntrlCodes = virtual-key code, JoyCodes = joystick button.</summary>
public sealed record PopperControl(string Name, string? KeyCode, string? JoyCode, bool Active);

public static class ConflictChecker
{
    /// <summary>Reads key and joystick assignments from the [Player] section of VPinballX.ini.</summary>
    public static List<ReservedInput> FromVpxIni(string iniText)
    {
        var result = new List<ReservedInput>();
        var inPlayer = false;

        foreach (var raw in iniText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inPlayer = line == "[Player]"; continue; }
            if (!inPlayer) continue;

            var eq = line.IndexOf('=');
            if (eq < 0 || !int.TryParse(line[(eq + 1)..].Trim(), out var value) || value <= 0) continue;
            var name = line[..eq].Trim();

            if (IsVpxKeyboardSetting(name) && DirectInputKeys.ToVirtualKey(value) is int vk)
                result.Add(new ReservedInput(InputId.Key(vk), "VPX", name));
            else if (name.StartsWith("Joy"))
                result.Add(new ReservedInput(InputId.Joy(value), "VPX", name));
        }
        return result;
    }

    public static List<ReservedInput> FromPopper(IEnumerable<PopperControl> rows)
    {
        var result = new List<ReservedInput>();
        foreach (var r in rows.Where(r => r.Active))
        {
            if (int.TryParse(r.KeyCode, out var key) && key > 0)
                result.Add(new ReservedInput(InputId.Key(key), "Popper", r.Name));
            // Popper stores joystick buttons 0-based (-1 = none); Windows/PinOne report them 1-based.
            if (int.TryParse(r.JoyCode, out var joy) && joy >= 0)
                result.Add(new ReservedInput(InputId.Joy(joy + 1), "Popper", r.Name));
        }
        return result;
    }

    public static List<ReservedInput> Find(Binding binding, IEnumerable<ReservedInput> reserved, ButtonTwins? twins = null)
    {
        var inputs = binding.Inputs.SelectMany(i => twins?.Expand(i) ?? new[] { i }).ToHashSet();
        return reserved.Where(r => inputs.Contains(r.Input)).ToList();
    }

    // "JoyCustom1Key" is the keyboard key VPX assigns to custom button 1; other "Joy..." names are joystick buttons.
    private static bool IsVpxKeyboardSetting(string name) =>
        (name.StartsWith("JoyCustom") && name.EndsWith("Key")) ||
        (!name.StartsWith("Joy") && (name.Contains("Key") || name.Contains("MagnaSave")
            || name is "MechTilt" or "VolumeUp" or "VolumeDown"));
}
