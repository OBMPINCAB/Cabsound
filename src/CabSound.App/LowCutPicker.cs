using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// A labelled drop-down for one table's low cut: Cabinet's cut (with what that is now) / Off / 40 / 60 / 90 / 120 Hz.
/// </summary>
public sealed class LowCutPicker : DockPanel
{
    public const string RattleWarning =
        "Low cuts under 60 Hz (or Off) send deep bass to the exciters. Listen for rattle and go back up if you hear it.";

    private readonly LowCutGroup _group;
    private readonly ComboBox _box = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private bool _showing;

    /// <summary>The owner picked another choice (not raised by Show).</summary>
    public event Action<int?>? Changed;

    public LowCutPicker(string label, LowCutGroup group)
    {
        _group = group;
        Margin = new Thickness(0, 6, 0, 0);
        Children.Add(new TextBlock { Text = label, Width = 190, VerticalAlignment = VerticalAlignment.Center });
        Children.Add(_box);
        _box.SelectionChanged += (_, _) =>
        {
            if (!_showing && _box.SelectedIndex >= 0) Changed?.Invoke(LowCutChoices.All[_box.SelectedIndex]);
        };
    }

    public void Show(int? value, SoundSettings sound)
    {
        _showing = true;
        var labels = LowCutChoices.All.Select(c => LowCutChoices.TableLabel(sound, _group, c)).ToList();
        // Only when the cabinet's cut changed: a new list would close an open drop-down
        if (_box.ItemsSource is not List<string> shown || !shown.SequenceEqual(labels)) _box.ItemsSource = labels;
        _box.SelectedIndex = LowCutChoices.All.ToList().IndexOf(value); // -1 (blank) for a value not in the list
        _showing = false;
    }

    /// <summary>
    /// Sends a picked low cut to the engine (with an undo point). The first risky exciter choice
    /// (Off or under 60 Hz) shows a one-time rattle warning.
    /// </summary>
    public static void Apply(Workers workers, EngineState state, string table, LowCutGroup group, int? value)
    {
        if (LowCutChoices.IsRisky(state.Sound, group, value)) WarnOnce(workers, state);
        workers.Run((e, now) => { e.SaveUndoPoint([table]); e.SetLowCut(table, group, value, now); });
    }

    /// <summary>Shows the rattle warning the first time only.</summary>
    public static void WarnOnce(Workers workers, EngineState state)
    {
        if (state.Sound.RattleWarned) return;
        MessageBox.Show(RattleWarning, "CabSound", MessageBoxButton.OK, MessageBoxImage.Warning);
        workers.Run((e, now) => e.SetSound(e.State.Sound with { RattleWarned = true }, now));
    }
}
