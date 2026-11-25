using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Quick pop-up (PinVol style): table, group and value, fades after ~2 s. Never takes focus or
/// clicks, so the game keeps running. Position/size in screen pixels; content rotated for the cabinet.
/// </summary>
public sealed class Popup : Window
{
    private OverlayPlacement _placement;
    private readonly Border _box;
    private readonly TextBlock _table = Text(16), _group = Text(22), _value = Text(44), _hint = Text(13);

    /// <summary>The pop-up's shortcut line: how to get from the quick levels to everything else.</summary>
    public const string MenuHint = "Magna + Vol Up: full menu";
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromSeconds(2) };
    private const double BarWidth = 260;
    private readonly ScaleTransform _barScale = new(0, 1);
    private readonly Border _bar;
    private bool _sample;

    public Popup(OverlayPlacement placement)
    {
        _placement = placement;
        _bar = new Border
        {
            Background = Brushes.DeepSkyBlue, CornerRadius = new CornerRadius(7),
            RenderTransform = _barScale, RenderTransformOrigin = new Point(0, 0.5),
        };
        OverlayWindow.Prepare(this);

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(_table);
        panel.Children.Add(_group);
        panel.Children.Add(_value);
        panel.Children.Add(new Grid
        {
            Width = BarWidth, Height = 14, Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new Border { Background = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), CornerRadius = new CornerRadius(7) },
                _bar,
            },
        });
        _hint.Text = MenuHint;
        _hint.Foreground = Brushes.LightGray;
        _hint.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_hint);
        // Content is laid out at the base box size (100 %); the Viewbox scales it to the window
        _box = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Child = panel,
        };
        Content = new Viewbox { Stretch = Stretch.Fill, Child = _box };
        ApplyLook();

        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    public void ShowInfo(PopupInfo info)
    {
        _sample = false;
        _table.Text = info.Table;
        _group.Text = info.Group;
        _value.Text = info.Value;
        _value.Foreground = info.AtLimit ? Brushes.OrangeRed : Brushes.White;
        _bar.Background = info.AtLimit ? Brushes.OrangeRed : Brushes.DeepSkyBlue;
        // Level bar slides to the new value (PinVol style)
        _barScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(info.Level, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase() });

        OverlayWindow.ShowAt(this, _placement);
        _hide.Stop();
        _hide.Start();
    }

    /// <summary>
    /// Sample pop-up for the Display page / preview: fixed text, no fade, stays until switched off.
    /// Follows placement changes. UI thread.
    /// </summary>
    public void ShowSample(bool on)
    {
        if (!on)
        {
            if (_sample) { _sample = false; Hide(); }
            return;
        }
        _hide.Stop();
        _sample = true;
        _table.Text = "Sample";
        _group.Text = "Pop-up position";
        _value.Text = "";
        _value.Foreground = Brushes.White;
        _bar.Background = Brushes.DeepSkyBlue;
        _barScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _barScale.ScaleX = 0.5;
        OverlayWindow.ShowAt(this, _placement);
    }

    /// <summary>Shows or removes the shortcut hint line (Display tab setting). UI thread.</summary>
    public void ShowHint(bool show) => _hint.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>New placement (rotation, size, opacity, position); a visible pop-up moves at once. UI thread.</summary>
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

    private static TextBlock Text(double size) => new()
    {
        FontSize = size, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
}
