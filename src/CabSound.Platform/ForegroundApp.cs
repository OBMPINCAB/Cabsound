using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CabSound.Platform;

/// <summary>Which program has the keyboard focus (used to send the pause key only to VPX).</summary>
public static class ForegroundApp
{
    /// <summary>True if the foreground window belongs to a VPinball* process (VPinballX, VPinballX64, VPinballX_GL...).</summary>
    public static bool IsVpx()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out var pid);
            if (pid == 0) return false;
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.StartsWith("VPinball", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; } // the process exited meanwhile
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
