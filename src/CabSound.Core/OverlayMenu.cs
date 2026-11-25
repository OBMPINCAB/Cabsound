namespace CabSound.Core;

public enum MenuCommand { Up, Down, Decrease, Increase, Close }
public enum MenuPage { Main, Display, Guide }
public enum MenuRow
{
    TableVolume, Backbox, FrontExciters, RearExciters, BackboxLowCut, ExciterLowCut, RearExciterLowCut, GlobalVolume, NightMode, Reset, Display, Guide,
    Window, Rotation, Position, NudgeAlong, NudgeToward, Size, Opacity, Monitor, Back,
    GuideTopic, GuideStep1, GuideStep2, GuideStep3, GuideStep4, GuideStep5, GuideStep6, GuideBack,
}

/// <summary>Remembers which guide topic and step were highlighted when closing/reopening the menu.</summary>
public readonly record struct GuideBookmark(GuideTopic Topic = GuideTopic.TableTuning, MenuRow Row = MenuRow.GuideTopic);

/// <summary>A change the menu asks for: this row, one step down (-1) or up (+1), for this window.</summary>
public sealed record MenuEffect(MenuRow Row, int Direction, OverlayKind Window);

/// <summary>
/// The on-screen menu's navigation only: pages, two-press confirm for Reset, idle close, and Guide bookmark.
/// Main page: the running table's values and Reset table (asked twice).
/// Display page: where the pop-up and the menu sit.
/// Guide page: concise Amp Setup and Table Tuning steps (idle close extended to 5 min so you can read / turn knobs).
/// Levels live in the engine, which applies the returned effects.
/// </summary>
public sealed class OverlayMenu
{
    public const int IdleTimeoutMs = 15_000;
    public const int GuideIdleTimeoutMs = 300_000;
    public const int ResetConfirmMs = 3_000;

    private static readonly MenuRow[] MainRows = [.. Enum.GetValues<MenuRow>().TakeWhile(r => r != MenuRow.Window)];
    private static readonly MenuRow[] DisplayRows = [.. Enum.GetValues<MenuRow>().SkipWhile(r => r != MenuRow.Window).TakeWhile(r => r != MenuRow.GuideTopic)];
    private static readonly MenuRow[] GuideRows = [.. Enum.GetValues<MenuRow>().SkipWhile(r => r != MenuRow.GuideTopic)];

    private long _lastInputMs;
    private long _armedAtMs;
    private GuideTopic _topic;
    private MenuRow _guideRow;

    public OverlayMenu(long nowMs, GuideBookmark bookmark = default)
    {
        _lastInputMs = nowMs;
        _topic = bookmark.Topic;
        _guideRow = Array.IndexOf(GuideRows, bookmark.Row) >= 0 && bookmark.Row != MenuRow.GuideBack
            ? bookmark.Row
            : MenuRow.GuideTopic;
    }

    public MenuPage Page { get; private set; } = MenuPage.Main;
    public MenuRow Selected { get; private set; } = MenuRow.TableVolume;
    public OverlayKind EditingWindow { get; private set; } = OverlayKind.Popup;
    public GuideTopic Topic => _topic;
    public GuideBookmark Bookmark => new(_topic, _guideRow);
    public bool ResetArmed { get; private set; }
    public bool IsClosed { get; private set; }

    public static IReadOnlyList<MenuRow> Rows(MenuPage page) => page switch
    {
        MenuPage.Main => MainRows,
        MenuPage.Display => DisplayRows,
        _ => GuideRows,
    };

    public MenuEffect? Handle(MenuCommand command, long nowMs)
    {
        Tick(nowMs);
        if (IsClosed) return null;
        _lastInputMs = nowMs;

        switch (command)
        {
            case MenuCommand.Close: IsClosed = true; return null;
            case MenuCommand.Up: Move(-1); return null;
            case MenuCommand.Down: Move(+1); return null;
        }

        if (Selected != MenuRow.Reset) ResetArmed = false;

        if (Page == MenuPage.Guide)
        {
            if (Selected == MenuRow.GuideBack)
            {
                Go(MenuPage.Main, MenuRow.Guide);
            }
            else if (Selected != MenuRow.GuideTopic && command == MenuCommand.Decrease)
            {
                _guideRow = Selected;
                Go(MenuPage.Main, MenuRow.Guide);
            }
            else
            {
                if (Selected != MenuRow.GuideBack) _guideRow = Selected;
                _topic = _topic == GuideTopic.TableTuning ? GuideTopic.AmpSetup : GuideTopic.TableTuning;
            }
            return null;
        }

        switch (Selected)
        {
            case MenuRow.Display: Go(MenuPage.Display, MenuRow.Window); return null;
            case MenuRow.Guide: Go(MenuPage.Guide, _guideRow); return null;
            case MenuRow.Back: Go(MenuPage.Main, MenuRow.Display); return null;
            case MenuRow.Window:
                EditingWindow = EditingWindow == OverlayKind.Popup ? OverlayKind.Menu : OverlayKind.Popup;
                return null;
            case MenuRow.Reset:
                if (!ResetArmed) { ResetArmed = true; _armedAtMs = nowMs; return null; }
                ResetArmed = false;
                return new MenuEffect(MenuRow.Reset, +1, EditingWindow);
            default:
                return new MenuEffect(Selected, command == MenuCommand.Increase ? +1 : -1, EditingWindow);
        }
    }

    public void Tick(long nowMs)
    {
        if (IsClosed) return;
        var timeout = Page == MenuPage.Guide ? GuideIdleTimeoutMs : IdleTimeoutMs;
        if (nowMs - _lastInputMs >= timeout) IsClosed = true;
        if (ResetArmed && nowMs - _armedAtMs > ResetConfirmMs) ResetArmed = false;
    }

    private void Move(int step)
    {
        ResetArmed = false;
        var rows = Rows(Page);
        var index = ((IList<MenuRow>)rows).IndexOf(Selected);
        Selected = rows[(index + step + rows.Count) % rows.Count];
        if (Page == MenuPage.Guide && Selected != MenuRow.GuideBack) _guideRow = Selected;
    }

    private void Go(MenuPage page, MenuRow selected)
    {
        Page = page;
        Selected = selected;
    }
}
