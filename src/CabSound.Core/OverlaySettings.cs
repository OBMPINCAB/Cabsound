using System.Text.Json.Serialization;

namespace CabSound.Core;

/// <summary>A Windows monitor, by device name and its desktop rectangle in pixels.</summary>
public sealed record MonitorInfo(string Name, int X, int Y, int Width, int Height, bool Primary);

/// <summary>Where an overlay sits on its screen: the edge band, the middle, or the backbox.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OverlayPosition>))]
public enum OverlayPosition { LockdownBar, Middle, Backbox }

/// <summary>The two on-screen overlays: the pop-up (volume/SSF strip) and the cabinet menu.</summary>
public enum OverlayKind { Popup, Menu }

/// <summary>
/// Placement of one overlay. Monitor = device name (null = primary). Rotation 0/90/180/270.
/// Width/Height = unrotated base box in pixels; drawn size = base x SizePercent / 100.
/// Along = pixels along the lockdown bar (positive = player's right); Toward = pixels toward the backbox.
/// OpacityPercent = how see-through the overlay is (30..100).
/// </summary>
public sealed record OverlayPlacement(string? Monitor, int Rotation, OverlayPosition Position, int Along, int Toward, int Width, int Height, int SizePercent = 100, int OpacityPercent = 80);

/// <summary>
/// Placement of both overlays, saved in CabSound.json. ShowHint = the pop-up's small line that says
/// how to open the full menu (on unless switched off; files saved before it existed load as on).
/// </summary>
public sealed record OverlaySettings(OverlayPlacement Popup, OverlayPlacement Menu, bool ShowHint = true)
{
    public OverlayPlacement Get(OverlayKind k) => k == OverlayKind.Popup ? Popup : Menu;

    public OverlaySettings With(OverlayKind k, OverlayPlacement p) =>
        k == OverlayKind.Popup ? this with { Popup = p } : this with { Menu = p };
}
