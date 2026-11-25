using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// The on-screen cabinet menu: table name, one line per menu row with its value, selected row
/// highlighted. Like the pop-up it never takes focus or clicks; position/size in screen pixels,
/// content rotated for the cabinet. The input thread drives it; this only draws.
/// </summary>
public sealed class MenuWindow : Window
{
    private OverlayPlacement _placement;
    private readonly Border _box;
    private readonly TextBlock _header = Text(30);
    private readonly TextBlock _footer = Text(20);
    private readonly StackPanel _rows = new();

    public MenuWindow(OverlayPlacement placement)
    {
        _placement = placement;
        OverlayWindow.Prepare(this);

        _footer.Text = "Flippers: move · Magna: change · Start or Vol Up: close";
        _footer.Foreground = Brushes.LightGray;
        _footer.HorizontalAlignment = HorizontalAlignment.Center;
        _header.HorizontalAlignment = HorizontalAlignment.Center;
        _header.Margin = new Thickness(0, 0, 0, 12);

        var layout = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(_footer, Dock.Bottom);
        layout.Children.Add(_header);
        layout.Children.Add(_footer);
        layout.Children.Add(_rows);

        // Content is laid out at the base box size (100 %); the Viewbox scales it to the window
        _box = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(24), Child = layout };
        Content = new Viewbox { Stretch = Stretch.Fill, Child = _box };
        ApplyLook();
    }

    /// <summary>Draws the menu with the engine's current values and shows it at its placement. UI thread.</summary>
    public void Show(OverlayMenu menu, EngineState s)
    {
        _header.Text = menu.Page == MenuPage.Guide
            ? $"Tuning Guide — {TableSearch.DisplayName(s.Table)}"
            : TableSearch.DisplayName(s.Table);
        _footer.Text = menu.Page == MenuPage.Guide
            ? "Flippers: step · L-Magna: ‹ controls · R-Magna: switch guide · Vol Up: play"
            : "Flippers: move · Magna: change · Start or Vol Up: close";
        _rows.Children.Clear();
        foreach (var row in OverlayMenu.Rows(menu.Page))
        {
            var (value, warn) = Value(row, menu, s);
            _rows.Children.Add(Line(Label(row, menu, s), value, warn, row == menu.Selected));
        }
        OverlayWindow.ShowAt(this, _placement);
    }

    public void HideMenu() => Hide();

    /// <summary>New placement (rotation, size, opacity, position); a visible menu moves at once. UI thread.</summary>
    public void Place(OverlayPlacement p)
    {
        if (p == _placement) return;
        _placement = p;
        ApplyLook();
        if (IsVisible) OverlayWindow.ShowAt(this, _placement);
    }

    private void ApplyLook()
    {
        _box.Width = _placement.Width;
        _box.Height = _placement.Height;
        _box.LayoutTransform = new RotateTransform(_placement.Rotation);
        _box.Background = OverlayWindow.Backdrop(_placement.OpacityPercent);
    }

    private static string Label(MenuRow row, OverlayMenu menu, EngineState s)
    {
        if (row is >= MenuRow.GuideStep1 and <= MenuRow.GuideStep6)
            return GuideText.Steps(menu.Topic)[row - MenuRow.GuideStep1].Title;

        return row switch
        {
            MenuRow.TableVolume => "Table volume",
            MenuRow.Backbox => "Backbox",
            MenuRow.FrontExciters => "Front exciters",
            MenuRow.RearExciters => "Rear exciters",
            MenuRow.BackboxLowCut => "Backbox low cut",
            MenuRow.ExciterLowCut => "Front exciter low cut",
            MenuRow.RearExciterLowCut => "Rear exciter low cut",
            MenuRow.GlobalVolume => s.NightMode ? "Night volume" : "Global volume",
            MenuRow.NightMode => "Night mode",
            MenuRow.Reset => "Reset table",
            MenuRow.Display => "Display ›",
            MenuRow.Guide => "Guide ›",
            MenuRow.Window => "Window",
            MenuRow.Rotation => "Rotation",
            MenuRow.Position => "Position",
            MenuRow.NudgeAlong => "Nudge along",
            MenuRow.NudgeToward => "Nudge toward backbox",
            MenuRow.Size => "Size",
            MenuRow.Opacity => "Opacity",
            MenuRow.Monitor => "Monitor",
            MenuRow.GuideTopic => "Topic",
            MenuRow.GuideBack => "‹ Back to controls",
            _ => "‹ Back",
        };
    }

    /// <summary>The row's value; warn = shown in orange (SSF at its limit, reset armed, key tuning reminder).</summary>
    private static (string Value, bool Warn) Value(MenuRow row, OverlayMenu menu, EngineState s)
    {
        if (row is >= MenuRow.GuideStep1 and <= MenuRow.GuideStep6)
        {
            var step = GuideText.Steps(menu.Topic)[row - MenuRow.GuideStep1];
            return (step.Action, step.Highlight);
        }

        var t = s.Levels;
        var p = s.Overlays.Get(menu.EditingWindow);
        (string, bool) Db(int db, SsfGroup group) => ($"{db:+0;-0;0} dB", s.AtLimit(t, group));

        return row switch
        {
            MenuRow.TableVolume => ($"{t.Volume}", false),
            MenuRow.Backbox => Db(t.BackglassDb, SsfGroup.Backglass),
            MenuRow.FrontExciters => Db(t.FrontDb, SsfGroup.Front),
            MenuRow.RearExciters => Db(t.RearDb, SsfGroup.Rear),
            MenuRow.BackboxLowCut => (LowCutChoices.TableLabel(s.Sound, LowCutGroup.Backbox, t.BackboxLowCut), false),
            MenuRow.ExciterLowCut => LowCutChoices.IsRisky(s.Sound, LowCutGroup.Exciters, t.ExciterLowCut)
                ? (LowCutChoices.TableLabel(s.Sound, LowCutGroup.Exciters, t.ExciterLowCut) + " (listen for rattle)", true)
                : (LowCutChoices.TableLabel(s.Sound, LowCutGroup.Exciters, t.ExciterLowCut), false),
            MenuRow.RearExciterLowCut => LowCutChoices.IsRisky(s.Sound, LowCutGroup.RearExciters, t.RearExciterLowCut)
                ? (LowCutChoices.TableLabel(s.Sound, LowCutGroup.RearExciters, t.RearExciterLowCut) + " (listen for rattle)", true)
                : (LowCutChoices.TableLabel(s.Sound, LowCutGroup.RearExciters, t.RearExciterLowCut), false),
            MenuRow.GlobalVolume => ($"{(s.NightMode ? s.NightVolume : s.GlobalVolume)}", false),
            MenuRow.NightMode => (s.NightMode ? "On" : "Off", false),
            MenuRow.Reset => menu.ResetArmed ? ("Press again to reset", true) : ("System volume, 0 dB", false),
            MenuRow.Guide => ("Amp & table tips", false),
            MenuRow.GuideTopic => (GuideText.TopicLabel(menu.Topic), false),
            MenuRow.GuideBack => ("Keep step bookmark", false),
            MenuRow.Window => (menu.EditingWindow == OverlayKind.Popup ? "Pop-up" : "Menu", false),
            MenuRow.Rotation => ($"{p.Rotation}°", false),
            MenuRow.Position => (p.Position switch
            {
                OverlayPosition.LockdownBar => "Lockdown bar",
                OverlayPosition.Middle => "Middle",
                _ => "Backbox",
            }, false),
            MenuRow.NudgeAlong => ($"{p.Along:+0;-0;0} px", false),
            MenuRow.NudgeToward => ($"{p.Toward:+0;-0;0} px", false),
            MenuRow.Size => ($"{p.SizePercent} %", false),
            MenuRow.Opacity => ($"{p.OpacityPercent} %", false),
            MenuRow.Monitor => (p.Monitor?.TrimStart('\\', '.') ?? "Primary", false),
            _ => ("", false),
        };
    }

    private static Border Line(string label, string value, bool warn, bool selected)
    {
        var name = Text(28);
        name.Text = label;
        var shown = Text(28);
        shown.Text = value;
        shown.HorizontalAlignment = HorizontalAlignment.Right;
        if (warn) shown.Foreground = Brushes.OrangeRed;

        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        Grid.SetColumn(shown, 1);
        grid.Children.Add(name);
        grid.Children.Add(shown);
        return new Border
        {
            Child = grid,
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(8),
            Background = selected ? new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0x8C, 0xC8)) : Brushes.Transparent,
        };
    }

    private static TextBlock Text(double size) => new()
    {
        FontSize = size, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
