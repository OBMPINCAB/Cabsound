using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Rotatable reference window showing the exact same Initial &amp; Amp Setup and Table Tuning steps
/// as the in-cabinet overlay menu's "Guide ›" page.
/// </summary>
public sealed class GuideWindow : Window
{
    public GuideWindow(ToolWindowPlace? savedPlace, Action<ToolWindowPlace> savePlace)
    {
        Title = "CabSound: tuning guide";

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Tip: While playing any table, open the on-screen menu (Magna + Vol Up) and pick \"Guide ›\" " +
                   "to read these steps right on the playfield.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(0, 0, 0, 12),
        });
        panel.Children.Add(Section("Initial & Amp Setup (do once)", GuideTopic.AmpSetup));
        panel.Children.Add(Section("Per-Table Tuning & Shaker Debug", GuideTopic.TableTuning));

        var close = new Button { Content = "Close", Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);

        ToolWindowFrame.Attach(this, panel, savedPlace, savePlace);
    }

    private static StackPanel Section(string heading, GuideTopic topic)
    {
        var box = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        box.Children.Add(new TextBlock { Text = heading, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        foreach (var step in GuideText.Steps(topic))
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(175) },
                    new ColumnDefinition(),
                },
            };
            var left = new TextBlock { Text = step.Title, FontWeight = FontWeights.Medium };
            var right = new TextBlock { Text = step.Action, TextWrapping = TextWrapping.Wrap };
            if (step.Highlight) right.Foreground = Brushes.OrangeRed;
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            box.Children.Add(grid);
        }
        return box;
    }
}
