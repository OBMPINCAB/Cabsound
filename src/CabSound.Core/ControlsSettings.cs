using System.Text.Json.Serialization;

namespace CabSound.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ControlPreset>))]
public enum ControlPreset { PinVol, Minimal, Custom }

/// <summary>
/// One configurable action binding saved in CabSound.json.
/// EitherCtrl = true means a leading Ctrl key matches both Left Ctrl and Right Ctrl (Magna Save).
/// </summary>
public sealed record ActionBinding(
    CabAction Action,
    List<InputId> Inputs,
    bool Hold = false,
    int HoldMs = 0,
    bool Combo = false,
    bool EitherCtrl = false)
{
    /// <summary>Expands into runtime bindings for ButtonPresses (two bindings when EitherCtrl is set).</summary>
    public IEnumerable<Binding> ToRuntimeBindings()
    {
        if (Inputs.Count == 0) yield break;
        if (EitherCtrl && Inputs.Count >= 2 && Inputs[0].Kind == InputKind.Key && InputNames.IsCtrl(Inputs[0].Code))
        {
            var rest = Inputs.Skip(1).ToList();
            yield return new Binding(Action, [InputId.Key(InputNames.VkLeftCtrl), .. rest], Hold, HoldMs, Combo);
            yield return new Binding(Action, [InputId.Key(InputNames.VkRightCtrl), .. rest], Hold, HoldMs, Combo);
            yield break;
        }
        yield return new Binding(Action, Inputs, Hold, HoldMs, Combo);
    }
}

/// <summary>
/// Saved cabinet control configuration: preset template, per-action bindings, and on-screen menu keys.
/// </summary>
public sealed record ControlsSettings(
    ControlPreset Layout,
    List<ActionBinding> Bindings,
    bool AutoMenuKeys = true,
    MenuKeys? CustomMenuKeys = null)
{
    public static readonly IReadOnlyList<CabAction> ConfigurableActions =
    [
        CabAction.TableUp, CabAction.TableDown,
        CabAction.BackglassUp, CabAction.BackglassDown,
        CabAction.FrontUp, CabAction.FrontDown,
        CabAction.RearUp, CabAction.RearDown,
        CabAction.GlobalUp, CabAction.GlobalDown,
        CabAction.NightMode, CabAction.OpenMenu,
    ];

    /// <summary>Standard VPX defaults used as fallback when CustomMenuKeys is first enabled.</summary>
    public static readonly MenuKeys DefaultMenuKeys = new(
        LeftFlipper: InputNames.VkLeftShift,
        RightFlipper: InputNames.VkRightShift,
        LeftMagna: InputNames.VkLeftCtrl,
        RightMagna: InputNames.VkRightCtrl,
        Start: 0x31, // '1'
        Pause: 0x50  // 'P'
    );

    private const int VkVolumeUp = 0xAF, VkVolumeDown = 0xAE, VkF4 = 0x73, VkF5 = 0x74,
        VkF8 = 0x77, VkF9 = 0x78, VkI = 0x49, VkG = 0x47, VkEnter = 0x0D;

    /// <summary>Shared default PinVol instance so ControlsOrDefault doesn't allocate a new object on every read.</summary>
    public static readonly ControlsSettings DefaultPinVol = PinVol();

    /// <summary>Value equality (unlike record ==, compares the Bindings list and each binding's Inputs by value).</summary>
    public bool SameAs(ControlsSettings? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || Layout != other.Layout || AutoMenuKeys != other.AutoMenuKeys
            || CustomMenuKeys != other.CustomMenuKeys || Bindings.Count != other.Bindings.Count)
            return false;
        for (var i = 0; i < Bindings.Count; i++)
        {
            var a = Bindings[i];
            var b = other.Bindings[i];
            if (a.Action != b.Action || a.Hold != b.Hold || a.HoldMs != b.HoldMs
                || a.Combo != b.Combo || a.EitherCtrl != b.EitherCtrl
                || !a.Inputs.SequenceEqual(b.Inputs))
                return false;
        }
        return true;
    }

    /// <summary>PinVol-compatible layout (default): Vol keys + F4/F5 + Ctrl combos.</summary>
    public static ControlsSettings PinVol(bool autoMenuKeys = true, MenuKeys? customMenuKeys = null) => new(
        ControlPreset.PinVol,
        [
            Tap(CabAction.TableUp, VkVolumeUp),
            Tap(CabAction.TableDown, VkVolumeDown),
            Tap(CabAction.BackglassUp, VkF4),
            Tap(CabAction.BackglassDown, VkF5),
            CtrlCombo(CabAction.FrontUp, VkI),
            CtrlCombo(CabAction.FrontDown, VkG),
            CtrlCombo(CabAction.RearUp, VkF4),
            CtrlCombo(CabAction.RearDown, VkF5),
            CtrlCombo(CabAction.GlobalUp, VkF9),
            CtrlCombo(CabAction.GlobalDown, VkF8),
            CtrlCombo(CabAction.NightMode, VkEnter),
            CtrlCombo(CabAction.OpenMenu, VkVolumeUp),
        ],
        autoMenuKeys, customMenuKeys);

    /// <summary>Minimal 2-button layout: Tap Vol Up/Down for volume; Hold Vol Up 1s = Menu, Hold Vol Down 2s = Night mode.</summary>
    public static ControlsSettings Minimal(bool autoMenuKeys = true, MenuKeys? customMenuKeys = null) => new(
        ControlPreset.Minimal,
        [
            Tap(CabAction.TableUp, VkVolumeUp),
            Tap(CabAction.TableDown, VkVolumeDown),
            new(CabAction.OpenMenu, [InputId.Key(VkVolumeUp)], Hold: true, HoldMs: 1000),
            new(CabAction.NightMode, [InputId.Key(VkVolumeDown)], Hold: true, HoldMs: 2000),
        ],
        autoMenuKeys, customMenuKeys);

    public static ControlsSettings ForPreset(ControlPreset preset, ControlsSettings current) => preset switch
    {
        ControlPreset.PinVol => PinVol(current.AutoMenuKeys, current.CustomMenuKeys),
        ControlPreset.Minimal => Minimal(current.AutoMenuKeys, current.CustomMenuKeys),
        _ => current with { Layout = ControlPreset.Custom },
    };

    public ActionBinding? Find(CabAction action) => Bindings.FirstOrDefault(b => b.Action == action);

    public List<Binding> ToRuntimeBindings() => Bindings.SelectMany(b => b.ToRuntimeBindings()).ToList();

    public bool UsesJoystick() => Bindings.Any(b => b.Inputs.Any(i => i.Kind == InputKind.Joystick));

    /// <summary>Replaces (or adds) the binding for one action and switches to Custom layout.</summary>
    public ControlsSettings WithBinding(ActionBinding binding)
    {
        var list = Bindings.Where(b => b.Action != binding.Action).ToList();
        if (binding.Inputs.Count > 0) list.Add(binding);
        return this with { Layout = ControlPreset.Custom, Bindings = list };
    }

    /// <summary>Clears one action's binding and switches to Custom layout.</summary>
    public ControlsSettings ClearAction(CabAction action) =>
        this with { Layout = ControlPreset.Custom, Bindings = Bindings.Where(b => b.Action != action).ToList() };

    /// <summary>Changes Tap / Hold trigger for an existing action binding.</summary>
    public ControlsSettings WithTrigger(CabAction action, bool hold, int holdMs)
    {
        var existing = Find(action);
        if (existing is null) return this;
        var updated = existing with
        {
            Hold = hold,
            HoldMs = hold ? holdMs : 0,
            // Hold doesn't combine with Combo
            Combo = !hold && existing.Inputs.Count >= 2,
        };
        return WithBinding(updated);
    }

    /// <summary>Creates a binding from captured inputs (1 input = tap/hold, 2 inputs = combo).</summary>
    public static ActionBinding FromCaptured(CabAction action, IReadOnlyList<InputId> inputs, ActionBinding? existing)
    {
        var list = inputs.ToList();
        var isCombo = list.Count >= 2;
        var hold = !isCombo && (existing?.Hold ?? false);
        var holdMs = hold ? (existing?.HoldMs ?? 1000) : 0;
        var eitherCtrl = isCombo && list[0].Kind == InputKind.Key && InputNames.IsCtrl(list[0].Code);
        return new ActionBinding(action, list, Hold: hold, HoldMs: holdMs, Combo: isCombo, EitherCtrl: eitherCtrl);
    }

    /// <summary>Resolves the active on-screen menu keys: VPX's keys when AutoMenuKeys is on, else CustomMenuKeys.</summary>
    public MenuKeys? ResolveMenuKeys(MenuKeys? vpxKeys) =>
        AutoMenuKeys ? vpxKeys : (CustomMenuKeys ?? vpxKeys ?? DefaultMenuKeys);

    private static ActionBinding Tap(CabAction action, int vk) =>
        new(action, [InputId.Key(vk)]);

    private static ActionBinding CtrlCombo(CabAction action, int vk) =>
        new(action, [InputId.Key(InputNames.VkLeftCtrl), InputId.Key(vk)], Combo: true, EitherCtrl: true);
}
