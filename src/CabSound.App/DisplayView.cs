using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Display tab: where the pop-up and the on-screen menu appear (monitor, rotation, position, nudge,
/// size, opacity). Showing a state never sends a change; only the user's edits do.
/// </summary>
public sealed class DisplayView : StackPanel
{
    private static readonly int[] Rotations = [0, 90, 180, 270];
    private static readonly string[] PositionNames = ["Lockdown bar", "Middle", "Backbox"];

    private readonly Workers _workers;
    private readonly Action<OverlayKind> _preview;
    private readonly ComboBox _window = Combo("Pop-up", "Menu");
    private readonly ComboBox _monitor = new() { MinWidth = 260 };
    private readonly ComboBox _rotation = Combo(Rotations.Select(r => $"{r}°").ToArray());
    private readonly ComboBox _position = Combo(PositionNames);
    private readonly Slider _along = Track(-1000, 1000), _toward = Track(-1000, 1000), _size = Track(50, 200), _opacity = Track(30, 100);
    private readonly Dictionary<Slider, TextBlock> _values = [];
    private readonly CheckBox _hint = new() { Content = $"Show the shortcut hint on the pop-up (\"{Popup.MenuHint}\")", Margin = new Thickness(0, 16, 0, 0) };
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private OverlayPlacement? _current;   // what the engine has, plus edits not yet echoed back
    private bool _showing;

    /// <param name="preview">Shows that window on its monitor for a few seconds (the app owns the windows).</param>
    public DisplayView(Workers workers, Action<OverlayKind> preview)
    {
        _workers = workers;
        _preview = preview;
        Margin = new Thickness(16);
        Children.Add(new TextBlock
        {
            Text = "Where the pop-up and the cabinet menu appear. Rotation says where the top of the text points: that edge is the backbox.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });

        Children.Add(Row("Window", _window));
        Children.Add(Row("Monitor", _monitor));
        Children.Add(Row("Rotation", _rotation));
        Children.Add(Row("Position", _position));
        Children.Add(Row("Nudge along (px)", _along));
        Children.Add(Row("Nudge toward backbox (px)", _toward));
        Children.Add(Row("Size (%)", _size));
        Children.Add(Row("Opacity (%)", _opacity));
        var preview5s = new Button { Content = "Preview", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 12, 0, 0) };
        preview5s.Click += (_, _) => _preview(Kind);
        Children.Add(preview5s);
        _hint.Click += (_, _) =>
        {
            if (_showing) return;
            var on = _hint.IsChecked == true;
            _workers.Run((e, now) => e.SetShowHint(on, now));
        };
        Children.Add(_hint);

        _window.SelectedIndex = 0; // Pop-up; set before the handler is wired, so nothing is sent
        _window.SelectionChanged += (_, _) => { if (!_showing) { _current = null; Refresh(); } };
        _monitor.SelectionChanged += (_, _) =>
            Edit(p => _monitor.SelectedItem is ComboBoxItem { Tag: MonitorInfo m } ? p with { Monitor = m.Name } : p);
        _rotation.SelectionChanged += (_, _) => Edit(p => _rotation.SelectedIndex < 0 ? p : p with { Rotation = Rotations[_rotation.SelectedIndex] });
        _position.SelectionChanged += (_, _) => Edit(p => _position.SelectedIndex < 0 ? p : p with { Position = (OverlayPosition)_position.SelectedIndex });
        _along.ValueChanged += (_, _) => Edit(p => p with { Along = (int)_along.Value });
        _toward.ValueChanged += (_, _) => Edit(p => p with { Toward = (int)_toward.Value });
        _size.ValueChanged += (_, _) => Edit(p => p with { SizePercent = (int)_size.Value });
        _opacity.ValueChanged += (_, _) => Edit(p => p with { OpacityPercent = (int)_opacity.Value });
    }

    private OverlayKind Kind => _window.SelectedIndex == 1 ? OverlayKind.Menu : OverlayKind.Popup;

    private EngineState? _state;

    public void ShowState(EngineState s)
    {
        _state = s;
        _current = s.Overlays.Get(Kind);
        Refresh();
    }

    /// <summary>Puts the controls on <see cref="_current"/> (or the latest state) without sending anything.</summary>
    private void Refresh()
    {
        if (_state is null) return;
        var p = _current ??= _state.Overlays.Get(Kind);
        _showing = true;
        try
        {
            var monitors = Monitors.All();
            if (!monitors.Select(m => m.Name).SequenceEqual(_monitors.Select(m => m.Name)))
            {
                _monitors = monitors;
                _monitor.Items.Clear();
                foreach (var m in monitors)
                    _monitor.Items.Add(new ComboBoxItem { Content = $"{m.Name.TrimStart('\\', '.')}  ({m.Width}×{m.Height})", Tag = m });
            }
            _monitor.SelectedIndex = monitors.Count == 0 ? -1 : monitors.ToList().IndexOf(OverlayLayout.FindMonitor(p.Monitor, monitors));
            _rotation.SelectedIndex = Array.IndexOf(Rotations, p.Rotation);
            _position.SelectedIndex = (int)p.Position;
            Show(_along, p.Along);
            Show(_toward, p.Toward);
            Show(_size, p.SizePercent);
            Show(_opacity, p.OpacityPercent);
            _hint.IsChecked = _state.Overlays.ShowHint;
        }
        finally { _showing = false; }
    }

    private void Show(Slider slider, int value)
    {
        if (!slider.IsMouseCaptureWithin) slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum); // not under the user's drag
        _values[slider].Text = $"{value}";
    }

    /// <summary>The user changed a control: apply it to the placement and send it to the engine.</summary>
    private void Edit(Func<OverlayPlacement, OverlayPlacement> change)
    {
        if (_showing || _current is null) return;
        var kind = Kind;
        var p = change(_current);
        if (p == _current) return;
        _current = p;
        _values[_along].Text = $"{p.Along}";
        _values[_toward].Text = $"{p.Toward}";
        _values[_size].Text = $"{p.SizePercent}";
        _values[_opacity].Text = $"{p.OpacityPercent}";
        _workers.Run((e, now) => e.SetOverlay(kind, p, now));
    }

    private FrameworkElement Row(string label, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        var name = new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(control, 1);
        if (control is not Slider) control.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(name);
        grid.Children.Add(control);
        if (control is Slider slider)
        {
            var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
            _values[slider] = value;
        }
        return grid;
    }

    private static ComboBox Combo(params string[] items)
    {
        var box = new ComboBox { MinWidth = 160 };
        foreach (var item in items) box.Items.Add(item);
        return box;
    }

    /// <summary>Slider in steps of 10.</summary>
    private static Slider Track(int min, int max) => new()
    {
        Minimum = min, Maximum = max, TickFrequency = 10, SmallChange = 10, LargeChange = 10,
        IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0),
    };
}
