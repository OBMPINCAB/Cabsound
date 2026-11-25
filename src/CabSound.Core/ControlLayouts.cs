namespace CabSound.Core;

/// <summary>
/// Ready-made layouts. PinVolKeys = what CabSound uses now: PinVol's own keys, which PinOne sends
/// for its buttons. The joystick layouts are for the Controls screen later (step G).
/// </summary>
public static class ControlLayouts
{
    // Windows virtual-key codes
    private const int VkVolumeUp = 0xAF, VkVolumeDown = 0xAE, VkF4 = 0x73, VkF5 = 0x74,
        VkF8 = 0x77, VkF9 = 0x78, VkI = 0x49, VkG = 0x47, VkEnter = 0x0D;
    private static readonly InputId[] CtrlKeys = [InputId.Key(0xA2), InputId.Key(0xA3)]; // left, right

    /// <summary>
    /// PinVol's keys as set on this cabinet (PinVolSettings.ini). On the cabinet: Ctrl = magna save,
    /// F9 = Start, F8 = Credit, Enter = Launch; PinOne sends Ctrl+I / Ctrl+G for btn 17 / 18.
    /// </summary>
    public static List<Binding> PinVolKeys() =>
    [
        new(CabAction.TableUp, [InputId.Key(VkVolumeUp)]),
        new(CabAction.TableDown, [InputId.Key(VkVolumeDown)]),
        new(CabAction.BackglassUp, [InputId.Key(VkF4)]),
        new(CabAction.BackglassDown, [InputId.Key(VkF5)]),
        .. Ctrl(CabAction.FrontUp, VkI),
        .. Ctrl(CabAction.FrontDown, VkG),
        .. Ctrl(CabAction.RearUp, VkF4),
        .. Ctrl(CabAction.RearDown, VkF5),
        .. Ctrl(CabAction.GlobalUp, VkF9),
        .. Ctrl(CabAction.GlobalDown, VkF8),
        .. Ctrl(CabAction.NightMode, VkEnter),
        .. Ctrl(CabAction.OpenMenu, VkVolumeUp),
    ];

    /// <summary>Keys CabSound keeps from Windows, so Windows doesn't change the volume as well.</summary>
    public static bool IsVolumeKey(int vk) => vk is VkVolumeUp or VkVolumeDown;

    private static IEnumerable<Binding> Ctrl(CabAction action, int vk) =>
        CtrlKeys.Select(ctrl => new Binding(action, [ctrl, InputId.Key(vk)], Combo: true));

    public static readonly InputId VolumeUp = InputId.Joy(20);
    public static readonly InputId VolumeDown = InputId.Joy(21);
    public static readonly InputId BackglassUpButton = InputId.Joy(19);
    public static readonly InputId BackglassDownButton = InputId.Joy(16);
    public static readonly InputId FrontUpButton = InputId.Joy(17);
    public static readonly InputId FrontDownButton = InputId.Joy(18);

    /// <summary>Joystick layout 1: PinVol's jobs on PinOne button numbers; hold Vol Up 1 s = menu, hold Vol Down 2 s = night mode.</summary>
    public static List<Binding> PinVolStyle() =>
    [
        new(CabAction.TableUp, [VolumeUp]),
        new(CabAction.TableDown, [VolumeDown]),
        new(CabAction.BackglassUp, [BackglassUpButton]),
        new(CabAction.BackglassDown, [BackglassDownButton]),
        new(CabAction.FrontUp, [FrontUpButton]),
        new(CabAction.FrontDown, [FrontDownButton]),
        .. Shared(),
    ];

    /// <summary>Joystick layout 2: Volume Up/Down adjust the selected group, Backbox Up cycles groups.</summary>
    public static List<Binding> Simple() =>
    [
        new(CabAction.AdjustUp, [VolumeUp]),
        new(CabAction.AdjustDown, [VolumeDown]),
        new(CabAction.NextGroup, [BackglassUpButton]),
        .. Shared(),
    ];

    private static List<Binding> Shared() =>
    [
        new(CabAction.OpenMenu, [VolumeUp], Hold: true, HoldMs: 1000),
        new(CabAction.NightMode, [VolumeDown], Hold: true, HoldMs: 2000),
    ];
}
