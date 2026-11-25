using System.Runtime.InteropServices;

namespace CabSound.Platform;

/// <summary>Presses a key as if from the keyboard (used to send VPX's pause key).</summary>
public static class KeySender
{
    public static void Tap(int virtualKey)
    {
        var scan = (byte)MapVirtualKey((uint)virtualKey, 0);
        keybd_event((byte)virtualKey, scan, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event((byte)virtualKey, scan, 2 /* KEYEVENTF_KEYUP */, UIntPtr.Zero);
    }

    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
}
