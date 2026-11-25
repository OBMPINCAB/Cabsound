using System.Runtime.InteropServices;

namespace CabSound.Platform;

/// <summary>
/// Global keyboard hook. Every key is passed on to other programs, except while the on-screen menu is
/// open: then <see cref="HoldBack"/> may hold back the flipper/magna/Start keys (keys CabSound sends
/// itself always pass), so VPX, Popper and tables never lose input otherwise.
/// </summary>
public sealed class KeyboardWatcher : IDisposable
{
    public event Action<int, bool, bool>? Key; // virtual-key code, pressed, held back from other programs

    /// <summary>
    /// When set and it returns true for a key (virtual-key code, pressed), that key is held back
    /// from other programs (used only while the on-screen menu is open). Keys CabSound sends itself always pass.
    /// </summary>
    public Func<int, bool, bool>? HoldBack { get; set; }

    private readonly HookProc _proc; // kept alive so the GC can't collect the callback
    private Thread? _thread;
    private volatile uint _threadId;

    public KeyboardWatcher() => _proc = OnHook;

    public void Start()
    {
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            var hook = SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, _proc, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) return;
            try
            {
                while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            }
            finally
            {
                UnhookWindowsHookEx(hook);
            }
        }) { IsBackground = true, Name = "CabSound keyboard" };
        _thread.Start();
    }

    private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var vk = Marshal.ReadInt32(lParam);
            var injected = (Marshal.ReadInt32(lParam, 8) & 0x10) != 0; // LLKHF_INJECTED
            var msg = (int)wParam;
            var down = msg is 0x100 or 0x104;  // WM_KEYDOWN, WM_SYSKEYDOWN
            var up = msg is 0x101 or 0x105;    // WM_KEYUP, WM_SYSKEYUP
            var heldBack = !injected && (down || up) && HoldBack?.Invoke(vk, down) == true;

            if (down || up) Key?.Invoke(vk, down, heldBack);
            if (heldBack) return 1;
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        var id = _threadId;
        if (id != 0) PostThreadMessage(id, 0x12 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(1000);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
