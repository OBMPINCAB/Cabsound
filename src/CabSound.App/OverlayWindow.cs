using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Shared by the pop-up and the menu: a topmost window that never takes focus or clicks (so the game
/// keeps running), placed in screen pixels.
/// </summary>
public static class OverlayWindow
{
    /// <summary>Borderless, transparent, topmost, not in the taskbar; no focus or clicks once it has a handle.</summary>
    public static void Prepare(Window w)
    {
        w.WindowStyle = WindowStyle.None;
        w.ResizeMode = ResizeMode.NoResize;
        w.AllowsTransparency = true;
        w.Background = Brushes.Transparent;
        w.Topmost = true;
        w.ShowInTaskbar = false;
        w.ShowActivated = false;
        w.Focusable = false;
        w.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
        };
    }

    /// <summary>
    /// Shows the window at its placement without activating it. Pixels, not WPF units, so it lands
    /// exactly where PinVol's pop-up did on a scaled 4K screen. First show is invisible until moved.
    /// </summary>
    public static void ShowAt(Window w, OverlayPlacement placement)
    {
        if (!w.IsVisible) { w.Opacity = 0; w.Show(); }
        var r = OverlayLayout.Place(placement, Monitors.All());
        SetWindowPos(new WindowInteropHelper(w).Handle, HWND_TOPMOST, r.X, r.Y, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        w.Opacity = 1;
    }

    /// <summary>Dark background; the placement's opacity becomes its alpha (text stays solid).</summary>
    public static Brush Backdrop(int opacityPercent) =>
        new SolidColorBrush(Color.FromArgb((byte)Math.Round(opacityPercent * 255 / 100.0), 0x10, 0x10, 0x14));

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
