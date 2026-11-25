using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CabSound.App;

/// <summary>
/// One slider row: label · slider · value · what is actually written. Showing a state never reports a
/// change (so opening the window can't rewrite levels). A value beyond the slider's range shows the
/// slider at its end plus a "beyond limit" badge; it only changes when the user moves the slider.
/// </summary>
public sealed class LevelSlider : Grid
{
    private readonly Slider _slider = new()
    {
        IsSnapToTickEnabled = true, TickFrequency = 1, SmallChange = 1, LargeChange = 1,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0),
    };
    private readonly TextBlock _value = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 16 };
    private readonly TextBlock _written = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _badge = new() { Foreground = Brushes.OrangeRed, FontSize = 11, Visibility = Visibility.Collapsed };
    private readonly bool _decibels;
    private bool _showing, _dragging;

    /// <summary>The user moved the slider to this value.</summary>
    public event Action<int>? Changed;

    /// <summary>A user gesture starts (drag start, or a click/key step outside a drag): time for an undo point.</summary>
    public event Action? GestureStarted;

    public LevelSlider(string label, bool decibels)
    {
        _decibels = decibels;
        Margin = new Thickness(0, 6, 0, 6);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = label, FontSize = 14 });
        name.Children.Add(_badge);
        Add(name, 0);
        Add(_slider, 1);
        Add(_value, 2);
        Add(_written, 3);

        _slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => { _dragging = true; GestureStarted?.Invoke(); }));
        _slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => _dragging = false));
        _slider.ValueChanged += (_, e) =>
        {
            if (_showing) return;
            var v = (int)Math.Round(e.NewValue);
            ShowValue(v);
            if (!_dragging) GestureStarted?.Invoke();
            Changed?.Invoke(v);
        };
    }

    /// <summary>Shows a value (no Changed event). While the user drags, the slider position is left alone.</summary>
    /// <param name="atLimit">Also orange: preset + this value has reached a limit (the value itself may be inside its range).</param>
    public void Show(int value, int min, int max, string written, bool atLimit = false)
    {
        _showing = true;
        _slider.Minimum = min;
        _slider.Maximum = max;
        if (!_dragging) _slider.Value = Math.Clamp(value, min, max);
        _showing = false;

        ShowValue(value);
        _written.Text = written;
        var beyond = value < min || value > max;
        _badge.Text = beyond ? $"beyond limit ({Format(value)})" : "";
        _badge.Visibility = beyond ? Visibility.Visible : Visibility.Collapsed;
        if (_decibels && (value <= min || value >= max || atLimit)) _value.Foreground = Brushes.OrangeRed;
        else _value.ClearValue(TextBlock.ForegroundProperty); // theme colour
    }

    private void ShowValue(int v) => _value.Text = Format(v);

    private string Format(int v) => _decibels ? $"{v:+0;-0;0} dB" : $"{v}";

    private void Add(UIElement e, int column)
    {
        SetColumn(e, column);
        Children.Add(e);
    }
}
