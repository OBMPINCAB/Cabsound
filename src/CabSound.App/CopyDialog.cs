using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>Pick tables (search + multi-select) to copy levels to. Returns the chosen table keys, or none.</summary>
public sealed class CopyDialog : Window
{
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple, Height = 360 };
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 8) };
    private readonly List<TableLevel> _all;
    private readonly HashSet<string> _picked = [];
    private bool _filling;

    private CopyDialog(IEnumerable<TableLevel> tables)
    {
        _all = tables.ToList();
        Title = "Copy levels to…";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = "Copy", IsDefault = true, Width = 90 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 90, Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Search, then click the tables to copy to:", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        panel.Children.Add(buttons);
        Content = panel;

        _search.TextChanged += (_, _) => Fill();
        // Picks survive a new search
        _list.SelectionChanged += (_, e) =>
        {
            if (_filling) return;
            foreach (Item i in e.AddedItems) _picked.Add(i.Name);
            foreach (Item i in e.RemovedItems) _picked.Remove(i.Name);
        };
        ok.Click += (_, _) => DialogResult = true;
        Fill();
        _search.Focus();
    }

    public static IReadOnlyList<string> Pick(Window? owner, IEnumerable<TableLevel> tables)
    {
        var d = new CopyDialog(tables) { Owner = owner };
        return d.ShowDialog() == true ? d._picked.ToList() : [];
    }

    private void Fill()
    {
        _filling = true;
        _list.Items.Clear();
        foreach (var t in TableSearch.Filter(_all, _search.Text))
        {
            var item = new Item(t.Name);
            _list.Items.Add(item);
            if (_picked.Contains(t.Name)) _list.SelectedItems.Add(item);
        }
        _filling = false;
    }

    private sealed record Item(string Name)
    {
        public override string ToString() => TableSearch.DisplayName(Name);
    }
}
