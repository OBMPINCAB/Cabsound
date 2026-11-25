namespace CabSound.Core;

/// <summary>Human-readable names for Windows virtual-key codes, joystick buttons, and CabSound actions.</summary>
public static class InputNames
{
    public const int VkLeftShift = 0xA0, VkRightShift = 0xA1;
    public const int VkLeftCtrl = 0xA2, VkRightCtrl = 0xA3;
    public const int VkLeftAlt = 0xA4, VkRightAlt = 0xA5;
    public const int VkEscape = 0x1B;

    public static bool IsModifier(int vk) => vk is 0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5;
    public static bool IsCtrl(int vk) => vk is 0x11 or VkLeftCtrl or VkRightCtrl;

    public static string Key(int vk) => vk switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x2E => "Delete",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => $"Num {vk - 0x60}",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        VkLeftShift => "Left Shift",
        VkRightShift => "Right Shift",
        VkLeftCtrl => "Left Ctrl",
        VkRightCtrl => "Right Ctrl",
        VkLeftAlt => "Left Alt",
        VkRightAlt => "Right Alt",
        0xAD => "Mute",
        0xAE => "Vol Down",
        0xAF => "Vol Up",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"Key 0x{vk:X2}",
    };

    public static string Input(InputId id) => id.Kind == InputKind.Key ? Key(id.Code) : $"Joy Btn {id.Code}";

    public static string Format(ActionBinding? binding)
    {
        if (binding is null || binding.Inputs.Count == 0) return "(not bound)";
        var parts = binding.Inputs.Select((id, i) =>
            i == 0 && binding.EitherCtrl && id.Kind == InputKind.Key && IsCtrl(id.Code)
                ? "Ctrl"
                : Input(id));
        return string.Join(" + ", parts);
    }

    public static string ActionLabel(CabAction action) => action switch
    {
        CabAction.TableUp => "Table volume up",
        CabAction.TableDown => "Table volume down",
        CabAction.BackglassUp => "Backbox up",
        CabAction.BackglassDown => "Backbox down",
        CabAction.FrontUp => "Front exciters up",
        CabAction.FrontDown => "Front exciters down",
        CabAction.RearUp => "Rear exciters up",
        CabAction.RearDown => "Rear exciters down",
        CabAction.GlobalUp => "Global volume up",
        CabAction.GlobalDown => "Global volume down",
        CabAction.NightMode => "Night mode",
        CabAction.OpenMenu => "Open on-screen menu",
        _ => action.ToString(),
    };
}
