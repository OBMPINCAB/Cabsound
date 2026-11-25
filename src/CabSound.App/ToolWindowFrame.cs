using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// The frame of the three cabinet windows (speaker check, calibration wizard, exciter limit check): no
/// Windows title bar but an own bar (title, Rotate, X) that turns together with the content, so the window
/// can be read from the playing position on the turned playfield screen. Drag the bar to move it. Rotation
/// and place are shared by the three windows and saved when one closes (<see cref="ToolWindowLayout"/>).
/// Known limits: the place is in WPF units = pixels at 100 % Windows scaling (the cabinet's setting);
/// Windows' own message boxes stay upright; the mouse still moves the screen's way.
/// </summary>
public static class ToolWindowFrame
{
    private const double ContentWidth = 640;

    // The place the last window was left at in this run: the engine's state a view holds can be one step
    // behind when the next window opens right after (wizard → limit check).
    private static ToolWindowPlace? _last;

    public static void Attach(Window window, FrameworkElement content, ToolWindowPlace? saved, Action<ToolWindowPlace> save)
    {
        var place = _last ?? saved;
        var rotation = ToolWindowLayout.Rotation(place);

        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var rotate = new Button { Content = "Rotate", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0) };
        var close = new Button { Content = "X", Width = 40 };
        var title = new TextBlock { Text = window.Title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var bar = new DockPanel { Background = Brushes.Transparent, Margin = new Thickness(16, 10, 10, 0), LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(rotate, Dock.Right);
        bar.Children.Add(close);
        bar.Children.Add(rotate);
        bar.Children.Add(title);

        var inner = new DockPanel { Width = ContentWidth };
        DockPanel.SetDock(bar, Dock.Top);
        inner.Children.Add(bar);
        inner.Children.Add(content);
        var root = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = inner,
            LayoutTransform = new RotateTransform(rotation),
        };
        window.Content = root;

        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) window.DragMove();
        };
        close.Click += (_, _) => window.Close();
        rotate.Click += (_, _) =>
        {
            var (x, y) = ToolWindowLayout.AfterTurn((int)window.Left, (int)window.Top, (int)window.ActualWidth, (int)window.ActualHeight);
            rotation = ToolWindowLayout.NextRotation(rotation);
            root.LayoutTransform = new RotateTransform(rotation);
            (window.Left, window.Top) = (x, y);
        };
        var placed = false;
        window.Loaded += (_, _) =>
        {
            (window.Left, window.Top) = ToolWindowLayout.TopLeft(place, (int)window.ActualWidth, (int)window.ActualHeight, Monitors.All());
            placed = true;
        };
        // Turned 90° / 180°, the window grows from the far side of the bar: move it so the bar stays put.
        // Not after Rotate (width and height just swap; AfterTurn already placed it).
        window.SizeChanged += (_, e) =>
        {
            var (was, now) = (e.PreviousSize, e.NewSize);
            if (!placed || was.Width == 0 || (was.Width == now.Height && was.Height == now.Width)) return;
            (window.Left, window.Top) = ToolWindowLayout.AfterResize(rotation, (int)window.Left, (int)window.Top,
                (int)e.PreviousSize.Width, (int)e.PreviousSize.Height, (int)e.NewSize.Width, (int)e.NewSize.Height);
        };
        window.Closed += (_, _) =>
        {
            _last = new ToolWindowPlace(rotation, (int)window.Left, (int)window.Top);
            save(_last);
        };
    }
}
