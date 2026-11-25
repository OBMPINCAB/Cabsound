using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CabSound.App;

/// <summary>
/// Shown at start-up when the saved sound card is gone (e.g. a new driver gave it a new name):
/// the owner picks the output the cabinet's amp is on. Cancel = carry on without table volume.
/// </summary>
public sealed class SoundCardDialog : Window
{
    private readonly ListBox _list = new() { Height = 220 };

    private SoundCardDialog(string missingName, IReadOnlyList<(string Id, string Name)> outputs)
    {
        Title = "CabSound: sound card not found";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true; // at a cabinet boot it must not open behind Popper
        Loaded += (_, _) => Activate();

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = $"CabSound can't find the sound card \"{missingName}\" (a new driver can rename it). " +
                   "Pick the output the cabinet's amp is connected to: normally the one that starts with \"Speakers\", " +
                   "not \"Digital Output\". Cancel: CabSound starts, but table volume won't change.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });
        foreach (var (_, name) in outputs) _list.Items.Add(name);
        panel.Children.Add(_list);

        var ok = new Button { Content = "Use this output", Width = 130, IsDefault = true, IsEnabled = false };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 110, Margin = new Thickness(8, 0, 0, 0) };
        _list.SelectionChanged += (_, _) => ok.IsEnabled = _list.SelectedIndex >= 0;
        ok.Click += (_, _) => DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>The output the owner picked, or null (cancelled, or Windows has no output at all).</summary>
    public static (string Id, string Name)? Pick(string missingName, IReadOnlyList<(string Id, string Name)> outputs)
    {
        if (outputs.Count == 0) return null;
        var dialog = new SoundCardDialog(missingName, outputs);
        return dialog.ShowDialog() == true ? outputs.ElementAt(dialog._list.SelectedIndex) : null;
    }
}
