using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>Tables tab: search all tables, edit one, copy one table's levels to others, reset.</summary>
public sealed class TablesView : DockPanel
{
    private readonly Workers _workers;
    private readonly TextBox _search = new() { Width = 300 };
    private readonly TextBlock _count = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ListView _list = new() { SelectionMode = SelectionMode.Extended };
    private readonly ObservableCollection<TableRow> _rows = [];
    private readonly Dictionary<string, TableRow> _byName = [];
    private readonly TextBlock _selectedName = new() { FontSize = 18, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly LevelSlider _volume = new("Table volume", decibels: false);
    private readonly LevelSlider _backbox = new("Backbox", decibels: true);
    private readonly LevelSlider _front = new("Front exciters", decibels: true);
    private readonly LevelSlider _rear = new("Rear exciters", decibels: true);
    private readonly LowCutPicker _backboxCut = new("Backbox low cut", LowCutGroup.Backbox);
    private readonly LowCutPicker _exciterCut = new("Front exciter low cut", LowCutGroup.Exciters);
    private readonly LowCutPicker _rearExciterCut = new("Rear exciter low cut", LowCutGroup.RearExciters);
    private readonly StackPanel _editor = new() { Width = 520, Margin = new Thickness(16, 0, 0, 0), Visibility = Visibility.Hidden };
    private readonly Button _copy = new() { Content = "Copy levels to…" };
    private readonly Button _reset = new() { Content = "Reset selected", Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _selectAll = new() { Content = "Select all", Margin = new Thickness(12, 0, 0, 0) };
    private EngineState? _state;
    private IReadOnlyList<TableLevel> _shown = [];

    public TablesView(Workers workers)
    {
        _workers = workers;
        Margin = new Thickness(16);

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "Search ", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(_search);
        top.Children.Add(_count);
        top.Children.Add(_selectAll);
        SetDock(top, Dock.Top);
        Children.Add(top);

        foreach (var s in new[] { _volume, _backbox, _front, _rear }) s.Margin = new Thickness(0, 4, 0, 4);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_copy);
        buttons.Children.Add(_reset);
        foreach (UIElement e in new UIElement[] { _selectedName, _volume, _backbox, _front, _rear, _backboxCut, _exciterCut, _rearExciterCut }) _editor.Children.Add(e);
        var right = new StackPanel();
        right.Children.Add(_editor);
        right.Children.Add(buttons);
        SetDock(right, Dock.Right);
        Children.Add(right);

        var grid = new GridView();
        grid.Columns.Add(Column("Table", nameof(TableRow.Display), 340));
        grid.Columns.Add(Column("Volume", nameof(TableRow.Volume), 60));
        grid.Columns.Add(Column("Backbox", nameof(TableRow.Backbox), 70));
        grid.Columns.Add(Column("Front", nameof(TableRow.Front), 60));
        grid.Columns.Add(Column("Rear", nameof(TableRow.Rear), 60));
        _list.View = grid;
        _list.ItemsSource = _rows;
        Children.Add(_list);

        _search.TextChanged += (_, _) => Refilter();
        _list.SelectionChanged += (_, _) => ShowEditor();
        Edit(_volume, LevelField.Volume);
        Edit(_backbox, LevelField.Backglass);
        Edit(_front, LevelField.Front);
        Edit(_rear, LevelField.Rear);
        _backboxCut.Changed += v => { if (Selected is [var row] && _state is { } s) LowCutPicker.Apply(_workers, s, row.Name, LowCutGroup.Backbox, v); };
        _exciterCut.Changed += v => { if (Selected is [var row] && _state is { } s) LowCutPicker.Apply(_workers, s, row.Name, LowCutGroup.Exciters, v); };
        _rearExciterCut.Changed += v => { if (Selected is [var row] && _state is { } s) LowCutPicker.Apply(_workers, s, row.Name, LowCutGroup.RearExciters, v); };
        _selectAll.Click += (_, _) => _list.SelectAll();
        _copy.Click += (_, _) => CopyLevels();
        _reset.Click += (_, _) => ResetSelected();
    }

    public void ShowState(EngineState s)
    {
        _state = s;
        Refilter();
    }

    private static GridViewColumn Column(string header, string property, double width) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new System.Windows.Data.Binding(property) };

    private List<TableRow> Selected => _list.SelectedItems.Cast<TableRow>().ToList();

    private TableLevel? Level(string name) => _state?.Tables.FirstOrDefault(t => t.Name == name);

    /// <summary>Rebuilds the list only when the shown names change; otherwise updates rows in place (keeps selection and scroll).</summary>
    private void Refilter()
    {
        if (_state is not { } s) return;
        _shown = TableSearch.Filter(s.Tables, _search.Text);
        _count.Text = $"{_shown.Count} tables";

        if (!_shown.Select(t => t.Name).SequenceEqual(_rows.Select(r => r.Name)))
        {
            var selected = Selected.Select(r => r.Name).ToHashSet();
            _rows.Clear();
            _byName.Clear();
            foreach (var t in _shown)
            {
                var row = new TableRow(t.Name);
                _rows.Add(row);
                _byName[t.Name] = row;
            }
            foreach (var row in _rows.Where(r => selected.Contains(r.Name))) _list.SelectedItems.Add(row);
        }
        foreach (var t in _shown) _byName[t.Name].Update(t);
        ShowEditor();
    }

    private void ShowEditor()
    {
        var selected = Selected;
        _copy.IsEnabled = selected.Count == 1;
        _reset.IsEnabled = selected.Count > 0;
        if (selected.Count != 1 || _state is not { } s || Level(selected[0].Name) is not { } t)
        {
            _editor.Visibility = Visibility.Hidden;
            return;
        }
        _editor.Visibility = Visibility.Visible;
        _selectedName.Text = TableSearch.DisplayName(t.Name);
        _volume.Show(t.Volume, 0, 100, "");
        _backbox.Show(t.BackglassDb, -s.CutLimitDb, s.BoostLimitDb, "", s.AtLimit(t, SsfGroup.Backglass));
        _front.Show(t.FrontDb, -s.CutLimitDb, s.BoostLimitDb, "", s.AtLimit(t, SsfGroup.Front));
        _rear.Show(t.RearDb, -s.CutLimitDb, s.BoostLimitDb, "", s.AtLimit(t, SsfGroup.Rear));
        _backboxCut.Show(t.BackboxLowCut, s.Sound);
        _exciterCut.Show(t.ExciterLowCut, s.Sound);
        _rearExciterCut.Show(t.RearExciterLowCut, s.Sound);
    }

    /// <summary>Slider edits change one value of the selected table, as it is on the engine thread.</summary>
    private void Edit(LevelSlider slider, LevelField field)
    {
        slider.GestureStarted += () => { if (Selected is [var row]) _workers.Run((e, _) => e.SaveUndoPoint([row.Name])); };
        slider.Changed += v => { if (Selected is [var row]) _workers.Run((e, now) => e.SetTableValue(row.Name, field, v, now)); };
    }

    private void CopyLevels()
    {
        if (Selected is not [var from] || _state is not { } s) return;
        var targets = CopyDialog.Pick(Window.GetWindow(this), s.Tables.Where(t => t.Name != from.Name));
        if (targets.Count == 0) return;
        var answer = MessageBox.Show($"Copy {from.Display}'s levels to {targets.Count} tables?", "CabSound", MessageBoxButton.YesNo);
        if (answer != MessageBoxResult.Yes) return;
        _workers.Run((e, now) => { e.SaveUndoPoint(targets); e.CopyLevels(from.Name, targets, now); });
    }

    private void ResetSelected()
    {
        var names = Selected.Select(r => r.Name).ToList();
        if (names.Count == 0) return;
        var answer = MessageBox.Show($"Reset {names.Count} tables to System volume, 0 dB and the cabinet's low cut?", "CabSound", MessageBoxButton.YesNo);
        if (answer != MessageBoxResult.Yes) return;
        _workers.Run((e, now) => { e.SaveUndoPoint(names); foreach (var n in names) e.Reset(n, now); });
    }

    /// <summary>One list row; 0 dB shows as a dim "·" so changed values stand out.</summary>
    public sealed class TableRow(string name) : INotifyPropertyChanged
    {
        public string Name { get; } = name;
        public string Display { get; } = TableSearch.DisplayName(name);
        public string Volume { get; private set; } = "";
        public string Backbox { get; private set; } = "";
        public string Front { get; private set; } = "";
        public string Rear { get; private set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(TableLevel t)
        {
            static string Db(int v) => v == 0 ? "·" : $"{v:+0;-0;0}";
            (Volume, Backbox, Front, Rear) = ($"{t.Volume}", Db(t.BackglassDb), Db(t.FrontDb), Db(t.RearDb));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
