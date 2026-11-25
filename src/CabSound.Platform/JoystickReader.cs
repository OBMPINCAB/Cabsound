using System.Runtime.InteropServices;

namespace CabSound.Platform;

/// <summary>
/// Polls joysticks (PinOne shows up as one) and reports button presses, 1-based.
/// onlyId: poll just that joystick (also avoids slow calls on unplugged IDs).
/// </summary>
public sealed class JoystickReader(int? onlyId = null)
{
    private const int MaxJoysticks = 16;
    private readonly uint[] _last = new uint[MaxJoysticks];

    public event Action<int, int, bool>? Button; // joystick id, button (1-32), pressed

    public static List<(int Id, string Name)> List()
    {
        var found = new List<(int, string)>();
        for (var id = 0; id < MaxJoysticks; id++)
        {
            var info = NewInfo();
            if (joyGetPosEx(id, ref info) != 0) continue;
            var caps = new JOYCAPS();
            var name = joyGetDevCaps(id, ref caps, Marshal.SizeOf<JOYCAPS>()) == 0 ? caps.szPname : "Joystick";
            found.Add((id, $"{name} ({caps.wNumButtons} buttons)"));
        }
        return found;
    }

    /// <summary>Call ~50 times a second.</summary>
    public void Poll()
    {
        for (var id = 0; id < MaxJoysticks; id++)
        {
            if (onlyId is not null && id != onlyId) continue;
            var info = NewInfo();
            if (joyGetPosEx(id, ref info) != 0) continue;

            var changed = info.dwButtons ^ _last[id];
            for (var b = 0; b < 32; b++)
                if ((changed & (1u << b)) != 0)
                    Button?.Invoke(id, b + 1, (info.dwButtons & (1u << b)) != 0);
            _last[id] = info.dwButtons;
        }
    }

    private static JOYINFOEX NewInfo() => new() { dwSize = Marshal.SizeOf<JOYINFOEX>(), dwFlags = 0x80 /* JOY_RETURNBUTTONS */ };

    [StructLayout(LayoutKind.Sequential)]
    private struct JOYINFOEX
    {
        public int dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
        public uint dwButtons;
        public int dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOYCAPS
    {
        public ushort wMid, wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public int wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax,
            wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
    }

    [DllImport("winmm.dll")] private static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "joyGetDevCapsW")]
    private static extern int joyGetDevCaps(int uJoyID, ref JOYCAPS pjc, int cbjc);
}
