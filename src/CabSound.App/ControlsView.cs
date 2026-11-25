using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Controls tab: choose a cabinet layout template (PinVol Compatible, Minimal 2-button, Custom),
/// bind any action to a key, combo, hold, or joystick button, and optionally override VPX menu keys.
/// </summary>
public sealed class ControlsView : StackPanel
{
    private readonly Workers _workers;
    private readonly RadioButton _pinVol = new() { Content = "PinVol Compatible — Vol keys + F4/F5 + Ctrl combos (best if upgrading from PinVol)", GroupName = "layout", Margin = new Thickness(0, 4, 0, 0) };
    private readonly RadioButton _minimal = new() { Content = "Minimal (2 buttons) — Tap Vol Up/Down for volume; Hold Vol Up 1s = Menu, Hold Vol Down 2s = Night mode", GroupName = "layout", Margin = new Thickness(0, 4, 0, 0) };
    private readonly RadioButton _custom = new() { Content = "Custom — pick your own keys, combos, holds, or joystick buttons", GroupName = "layout", Margin = new Thickness(0, 4, 0, 0) };
    private readonly Expander _expander = new() { Header = "Customize individual bindings", Margin = new Thickness(0, 12, 0, 0) };
    private readonly Dictionary<CabAction, ActionRow> _rows = [];
    private readonly TextBlock _conflictSummary = new() { Opacity = 0.8, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CheckBox _autoMenu = new() { Content = "Auto-detect on-screen menu keys from VPX (VPinballX.ini)", Margin = new Thickness(0, 16, 0, 4) };
    private readonly TextBlock _menuNote = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 6) };
    private readonly Grid _menuGrid = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Dictionary<MenuKeySlot, MenuKeyRow> _menuRows = [];
    private readonly Button _reset = new() { Content = "Reset to defaults" };
    private readonly Button _undo = new() { Content = "Undo", Margin = new Thickness(8, 0, 0, 0) };

    private readonly List<ReservedInput> _reserved;
    private readonly MenuKeys? _vpxMenuKeys;
    private EngineState? _state;
    private bool _showing;
    private object? _capturing; // CabAction or MenuKeySlot currently waiting for input

    private enum MenuKeySlot { LeftFlipper, RightFlipper, LeftMagna, RightMagna, Start, Pause }

    private sealed record ActionRow(ComboBox Trigger, TextBlock BindingText, Button BindButton, Button ClearButton, TextBlock Warning);
    private sealed record MenuKeyRow(TextBlock KeyText, Button BindButton);

    public ControlsView(Workers workers)
    {
        _workers = workers;
        _reserved = Workers.ReadReservedInputs();
        _vpxMenuKeys = Workers.ReadVpxMenuKeys();
        Margin = new Thickness(16);

        Children.Add(new TextBlock { Text = "Cabinet layout", FontSize = 16, Margin = new Thickness(0, 0, 0, 4) });
        Children.Add(_pinVol);
        Children.Add(_minimal);
        Children.Add(_custom);

        _expander.Content = BuildBindingsTable();
        Children.Add(_expander);
        Children.Add(_conflictSummary);

        Children.Add(new TextBlock { Text = "On-screen menu navigation", FontSize = 16, Margin = new Thickness(0, 20, 0, 4) });
        Children.Add(_autoMenu);
        Children.Add(_menuNote);
        BuildMenuKeysTable();
        Children.Add(_menuGrid);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(_reset);
        buttons.Children.Add(_undo);
        Children.Add(buttons);

        _pinVol.Click += (_, _) => PickPreset(ControlPreset.PinVol);
        _minimal.Click += (_, _) => PickPreset(ControlPreset.Minimal);
        _custom.Click += (_, _) => { _expander.IsExpanded = true; PickPreset(ControlPreset.Custom); };
        _autoMenu.Click += (_, _) => ToggleAutoMenu();
        _reset.Click += (_, _) => ResetDefaults();
        _undo.Click += (_, _) => { CancelCapture(); _workers.Run((e, now) => e.Undo(now)); };
    }

    public void CancelCapture()
    {
        if (_capturing is null) return;
        _capturing = null;
        _workers.CancelCapture();
        if (_state is { } s) ShowState(s);
    }

    private UIElement BuildBindingsTable()
    {
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rowIdx = 0;
        foreach (var action in ControlsSettings.ConfigurableActions)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = InputNames.ActionLabel(action), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
            var trigger = new ComboBox { ItemsSource = new[] { "Tap", "Hold 1s", "Hold 2s" }, Width = 85, Margin = new Thickness(0, 3, 8, 3) };
            var bindingText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 8, 3) };
            var bind = new Button { Content = "Bind", MinWidth = 70, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 3, 6, 3) };
            var clear = new Button { Content = "Clear", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 3, 8, 3) };
            var warn = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.OrangeRed, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 3) };

            Place(grid, label, rowIdx, 0);
            Place(grid, trigger, rowIdx, 1);
            Place(grid, bindingText, rowIdx, 2);
            Place(grid, bind, rowIdx, 3);
            Place(grid, clear, rowIdx, 4);
            Place(grid, warn, rowIdx, 5);

            var capturedAction = action;
            trigger.SelectionChanged += (_, _) => OnTriggerChanged(capturedAction, trigger.SelectedIndex);
            bind.Click += (_, _) => StartActionCapture(capturedAction);
            clear.Click += (_, _) => ClearAction(capturedAction);

            _rows[action] = new ActionRow(trigger, bindingText, bind, clear, warn);
            rowIdx++;
        }
        return grid;
    }

    private void BuildMenuKeysTable()
    {
        _menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        _menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        _menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var items = new (MenuKeySlot Slot, string Label)[]
        {
            (MenuKeySlot.LeftFlipper, "Move up (Left Flipper)"),
            (MenuKeySlot.RightFlipper, "Move down (Right Flipper)"),
            (MenuKeySlot.LeftMagna, "Decrease (Left Magna)"),
            (MenuKeySlot.RightMagna, "Increase (Right Magna)"),
            (MenuKeySlot.Start, "Close menu (Start)"),
            (MenuKeySlot.Pause, "Pause VPX"),
        };

        for (var i = 0; i < items.Length; i++)
        {
            _menuGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (slot, text) = items[i];
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
            var keyText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 8, 3) };
            var bind = new Button { Content = "Bind", MinWidth = 70, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 3, 0, 3) };
            bind.Click += (_, _) => StartMenuKeyCapture(slot);

            Place(_menuGrid, label, i, 0);
            Place(_menuGrid, keyText, i, 1);
            Place(_menuGrid, bind, i, 2);
            _menuRows[slot] = new MenuKeyRow(keyText, bind);
        }
    }

    private static void Place(Grid grid, UIElement el, int row, int col)
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        grid.Children.Add(el);
    }

    private void PickPreset(ControlPreset preset)
    {
        if (_showing || _state is null) return;
        CancelCapture();
        _workers.Run((e, now) =>
        {
            e.SaveSettingsUndoPoint();
            e.SetControls(ControlsSettings.ForPreset(preset, e.State.ControlsOrDefault), now);
        });
    }

    private void OnTriggerChanged(CabAction action, int index)
    {
        if (_showing || _state is null || index < 0) return;
        var (hold, ms) = index switch { 1 => (true, 1000), 2 => (true, 2000), _ => (false, 0) };
        _workers.Run((e, now) =>
        {
            e.SaveSettingsUndoPoint();
            e.SetControls(e.State.ControlsOrDefault.WithTrigger(action, hold, ms), now);
        });
    }

    private void StartActionCapture(CabAction action)
    {
        if (_capturing is CabAction a && a == action) { CancelCapture(); return; }
        CancelCapture();
        _capturing = action;
        if (_state is { } s) ShowState(s);

        _workers.BeginCapture(allowCombo: true, allowJoystick: true, captured =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _capturing = null;
                if (captured is { Count: > 0 })
                {
                    _workers.Run((e, now) =>
                    {
                        var cur = e.State.ControlsOrDefault;
                        e.SaveSettingsUndoPoint();
                        e.SetControls(cur.WithBinding(ControlsSettings.FromCaptured(action, captured, cur.Find(action))), now);
                    });
                }
                else if (_state is { } st) ShowState(st);
            });
        });
    }

    private void ClearAction(CabAction action)
    {
        CancelCapture();
        _workers.Run((e, now) =>
        {
            e.SaveSettingsUndoPoint();
            e.SetControls(e.State.ControlsOrDefault.ClearAction(action), now);
        });
    }

    private void ToggleAutoMenu()
    {
        if (_showing || _state is null) return;
        CancelCapture();
        var auto = _autoMenu.IsChecked == true;
        _workers.Run((e, now) =>
        {
            var cur = e.State.ControlsOrDefault;
            var custom = cur.CustomMenuKeys ?? _vpxMenuKeys ?? ControlsSettings.DefaultMenuKeys;
            e.SaveSettingsUndoPoint();
            e.SetControls(cur with { AutoMenuKeys = auto, CustomMenuKeys = custom }, now);
        });
    }

    private void StartMenuKeyCapture(MenuKeySlot slot)
    {
        if (_capturing is MenuKeySlot s0 && s0 == slot) { CancelCapture(); return; }
        CancelCapture();
        _capturing = slot;
        if (_state is { } s) ShowState(s);

        _workers.BeginCapture(allowCombo: false, allowJoystick: false, captured =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _capturing = null;
                if (captured is [{ Kind: InputKind.Key, Code: var vk }])
                {
                    _workers.Run((e, now) =>
                    {
                        var cur = e.State.ControlsOrDefault;
                        var m = cur.CustomMenuKeys ?? _vpxMenuKeys ?? ControlsSettings.DefaultMenuKeys;
                        var updated = slot switch
                        {
                            MenuKeySlot.LeftFlipper => m with { LeftFlipper = vk },
                            MenuKeySlot.RightFlipper => m with { RightFlipper = vk },
                            MenuKeySlot.LeftMagna => m with { LeftMagna = vk },
                            MenuKeySlot.RightMagna => m with { RightMagna = vk },
                            MenuKeySlot.Start => m with { Start = vk },
                            _ => m with { Pause = vk },
                        };
                        e.SaveSettingsUndoPoint();
                        e.SetControls(cur with { AutoMenuKeys = false, CustomMenuKeys = updated }, now);
                    });
                }
                else if (_state is { } st) ShowState(st);
            });
        });
    }

    private void ResetDefaults()
    {
        CancelCapture();
        _workers.Run((e, now) =>
        {
            e.SaveSettingsUndoPoint();
            e.SetControls(ControlsSettings.PinVol(), now);
        });
    }

    public void ShowState(EngineState s)
    {
        _state = s;
        var c = s.ControlsOrDefault;
        _showing = true;

        _pinVol.IsChecked = c.Layout == ControlPreset.PinVol;
        _minimal.IsChecked = c.Layout == ControlPreset.Minimal;
        _custom.IsChecked = c.Layout == ControlPreset.Custom;
        if (c.Layout == ControlPreset.Custom) _expander.IsExpanded = true;

        var totalConflicts = 0;
        foreach (var action in ControlsSettings.ConfigurableActions)
        {
            var row = _rows[action];
            var binding = c.Find(action);
            var isCapturing = _capturing is CabAction a && a == action;

            row.Trigger.IsEnabled = binding is not null && !binding.Combo;
            row.Trigger.SelectedIndex = binding is null || !binding.Hold ? 0 : binding.HoldMs >= 2000 ? 2 : 1;
            row.BindingText.Text = isCapturing ? "Press key / button…" : InputNames.Format(binding);
            row.BindingText.Opacity = binding is null && !isCapturing ? 0.5 : 1.0;
            row.BindButton.Content = isCapturing ? "Cancel (Esc)" : "Bind";
            row.ClearButton.IsEnabled = binding is not null;

            var conflict = ConflictNote(binding);
            row.Warning.Text = conflict;
            if (conflict.Length > 0) totalConflicts++;
        }

        _conflictSummary.Text = _reserved.Count == 0
            ? "Conflict check: VPX / Popper configs not found on this PC."
            : totalConflicts == 0
                ? "✓ No single-key conflicts with VPX or PinUP Popper."
                : $"⚠ {totalConflicts} binding(s) share an input with VPX or PinUP Popper.";

        _autoMenu.IsChecked = c.AutoMenuKeys;
        var activeMenu = c.ResolveMenuKeys(_vpxMenuKeys);
        _menuNote.Text = c.AutoMenuKeys
            ? (activeMenu is { } m
                ? $"Detected from VPX: {InputNames.Key(m.LeftFlipper)} / {InputNames.Key(m.RightFlipper)} (move), " +
                  $"{InputNames.Key(m.LeftMagna)} / {InputNames.Key(m.RightMagna)} (change), " +
                  $"{InputNames.Key(m.Start)} (close), {InputNames.Key(m.Pause)} (pause)."
                : "VPinballX.ini not found — uncheck above to set menu keys manually.")
            : "Custom menu keys (held back from VPX while the on-screen menu is open):";

        _menuGrid.Visibility = c.AutoMenuKeys ? Visibility.Collapsed : Visibility.Visible;
        var customMenu = c.CustomMenuKeys ?? _vpxMenuKeys ?? ControlsSettings.DefaultMenuKeys;
        ShowMenuSlot(MenuKeySlot.LeftFlipper, customMenu.LeftFlipper);
        ShowMenuSlot(MenuKeySlot.RightFlipper, customMenu.RightFlipper);
        ShowMenuSlot(MenuKeySlot.LeftMagna, customMenu.LeftMagna);
        ShowMenuSlot(MenuKeySlot.RightMagna, customMenu.RightMagna);
        ShowMenuSlot(MenuKeySlot.Start, customMenu.Start);
        ShowMenuSlot(MenuKeySlot.Pause, customMenu.Pause);

        _undo.IsEnabled = s.CanUndo;
        _showing = false;
    }

    private void ShowMenuSlot(MenuKeySlot slot, int vk)
    {
        var row = _menuRows[slot];
        var isCapturing = _capturing is MenuKeySlot s && s == slot;
        row.KeyText.Text = isCapturing ? "Press a key…" : InputNames.Key(vk);
        row.BindButton.Content = isCapturing ? "Cancel (Esc)" : "Bind";
    }

    private string ConflictNote(ActionBinding? binding)
    {
        if (binding is null || binding.Inputs.Count == 0 || _reserved.Count == 0) return "";
        // Combos require holding a modifier (e.g. Ctrl + F4), so ignore single-key conflicts on the modifier itself
        var checkInputs = binding.Combo
            ? binding.Inputs.Where(i => !(i.Kind == InputKind.Key && InputNames.IsModifier(i.Code))).ToList()
            : binding.Inputs;
        if (checkInputs.Count == 0) return "";
        var hits = ConflictChecker.Find(new Binding(binding.Action, checkInputs), _reserved);
        if (hits.Count == 0) return "";
        var first = hits[0];
        return $"⚠ {first.Owner}: {first.Name}";
    }
}
