using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Shown before CabSound takes over Equalizer APO's config.txt: the file found, what is carried over,
/// what is dropped and what would be lost. Nothing changes unless the owner clicks Take over.
/// </summary>
public sealed class TakeOverDialog : Window
{
    private TakeOverDialog(string configText, FoundConfig found, string backupDir)
    {
        Title = "CabSound: take over Equalizer APO";
        Width = 760;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true; // at a cabinet boot it must not open behind Popper
        Loaded += (_, _) => Activate();

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(Wrapped("CabSound will replace config.txt with one line that includes its own file (CabSound.txt). " +
                                   $"The file below is saved first as a backup in {backupDir}."));
        foreach (var note in found.Notes) panel.Children.Add(Wrapped(note, Brushes.Orange));

        Section(panel, "CabSound carries over", found.Carried.Count > 0 ? found.Carried : ["Nothing (CabSound's defaults are used)"], null);
        if (found.NotUnderstood.Count > 0)
            Section(panel, "Not understood: these lines will be LOST", found.NotUnderstood, Brushes.OrangeRed);
        Section(panel, "Dropped (replaced by CabSound, or did nothing)", found.Dropped.Count > 0 ? found.Dropped : ["Nothing"], null);
        panel.Children.Add(Heading("config.txt found"));
        panel.Children.Add(new TextBox
        {
            Text = configText, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), Height = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        var ok = new Button { Content = "Take over", Width = 110 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, IsDefault = true, Width = 110, Margin = new Thickness(8, 0, 0, 0) };
        ok.Click += (_, _) => DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>True = the owner clicked Take over. Cancel, Esc, Enter or closing the window = false.</summary>
    public static bool Confirm(string configText, FoundConfig found, string backupDir) =>
        new TakeOverDialog(configText, found, backupDir).ShowDialog() == true;

    private static void Section(Panel panel, string title, IEnumerable<string> lines, Brush? colour)
    {
        panel.Children.Add(Heading(title));
        var text = Wrapped(string.Join("\n", lines.Select(l => "•  " + l)), colour);
        text.FontFamily = new FontFamily("Consolas");
        panel.Children.Add(text);
    }

    private static TextBlock Heading(string text) =>
        new() { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4) };

    private static TextBlock Wrapped(string text, Brush? colour = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        if (colour is not null) block.Foreground = colour;
        return block;
    }
}
