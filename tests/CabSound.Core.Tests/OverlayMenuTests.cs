using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class OverlayMenuTests
{
    private static OverlayMenu Open() => new(nowMs: 0);

    private static OverlayMenu OnDisplayPage()
    {
        var m = Open();
        long now = 1;
        Select(m, MenuRow.Display, ref now);
        m.Handle(MenuCommand.Increase, now);  // opens Display page
        return m;
    }

    private static void Select(OverlayMenu m, MenuRow row, ref long now)
    {
        while (m.Selected != row) m.Handle(MenuCommand.Down, now++);
    }

    [Fact]
    public void Opens_OnTableVolume_MainPage()
    {
        var m = Open();

        Assert.Equal(MenuPage.Main, m.Page);
        Assert.Equal(MenuRow.TableVolume, m.Selected);
        Assert.Equal(OverlayKind.Popup, m.EditingWindow);
        Assert.Equal(GuideTopic.TableTuning, m.Topic);
        Assert.False(m.IsClosed);
    }

    [Fact]
    public void Rows_ListEachPageInOrder()
    {
        Assert.Equal(
            [MenuRow.TableVolume, MenuRow.Backbox, MenuRow.FrontExciters, MenuRow.RearExciters,
             MenuRow.BackboxLowCut, MenuRow.ExciterLowCut, MenuRow.RearExciterLowCut,
             MenuRow.GlobalVolume, MenuRow.NightMode, MenuRow.Reset, MenuRow.Display, MenuRow.Guide],
            OverlayMenu.Rows(MenuPage.Main));
        Assert.Equal(
            [MenuRow.Window, MenuRow.Rotation, MenuRow.Position, MenuRow.NudgeAlong, MenuRow.NudgeToward,
             MenuRow.Size, MenuRow.Opacity, MenuRow.Monitor, MenuRow.Back],
            OverlayMenu.Rows(MenuPage.Display));
        Assert.Equal(
            [MenuRow.GuideTopic, MenuRow.GuideStep1, MenuRow.GuideStep2, MenuRow.GuideStep3,
             MenuRow.GuideStep4, MenuRow.GuideStep5, MenuRow.GuideStep6, MenuRow.GuideBack],
            OverlayMenu.Rows(MenuPage.Guide));
    }

    [Fact]
    public void Flippers_MoveAndWrap()
    {
        var m = Open();

        m.Handle(MenuCommand.Up, 1);
        Assert.Equal(MenuRow.Guide, m.Selected);

        m.Handle(MenuCommand.Down, 2);
        Assert.Equal(MenuRow.TableVolume, m.Selected);

        m.Handle(MenuCommand.Down, 3);
        Assert.Equal(MenuRow.Backbox, m.Selected);
    }

    [Fact]
    public void Magnas_GiveEffects()
    {
        var m = Open();

        Assert.Equal(new MenuEffect(MenuRow.TableVolume, +1, OverlayKind.Popup), m.Handle(MenuCommand.Increase, 1));
        Assert.Equal(new MenuEffect(MenuRow.TableVolume, -1, OverlayKind.Popup), m.Handle(MenuCommand.Decrease, 2));
    }

    [Fact]
    public void DisplayRow_OpensDisplayPage_BackReturns()
    {
        var m = Open();
        long now = 1;
        Select(m, MenuRow.Display, ref now);

        Assert.Null(m.Handle(MenuCommand.Decrease, now++));
        Assert.Equal(MenuPage.Display, m.Page);
        Assert.Equal(MenuRow.Window, m.Selected);

        m.Handle(MenuCommand.Up, now++);
        Assert.Equal(MenuRow.Back, m.Selected);
        Assert.Null(m.Handle(MenuCommand.Increase, now));
        Assert.Equal(MenuPage.Main, m.Page);
        Assert.Equal(MenuRow.Display, m.Selected);
    }

    [Fact]
    public void GuideRow_OpensGuidePage_MagnaSwitchesTopic_BookmarkRemembered_AndIdle15sDoesNotClose()
    {
        var m = Open();
        long now = 1;
        Select(m, MenuRow.Guide, ref now);

        Assert.Null(m.Handle(MenuCommand.Increase, now++));
        Assert.Equal(MenuPage.Guide, m.Page);
        Assert.Equal(MenuRow.GuideTopic, m.Selected);
        Assert.Equal(GuideTopic.TableTuning, m.Topic);

        // 15 s idle does NOT close the Guide page while reading / turning amp knobs (up to 5 min)
        m.Tick(now + 60_000);
        Assert.False(m.IsClosed);

        // Move bookmark to Step 3 and toggle topic with Right Magna (Increase)
        Select(m, MenuRow.GuideStep3, ref now);
        Assert.Null(m.Handle(MenuCommand.Increase, now++));
        Assert.Equal(GuideTopic.AmpSetup, m.Topic);
        Assert.Equal(new GuideBookmark(GuideTopic.AmpSetup, MenuRow.GuideStep3), m.Bookmark);

        // Left Magna (Decrease) on Step 3 jumps straight back to Main controls, keeping Step 3 bookmarked
        var bookmark = m.Bookmark;
        Assert.Null(m.Handle(MenuCommand.Decrease, now++));
        Assert.Equal(MenuPage.Main, m.Page);
        Assert.Equal(MenuRow.Guide, m.Selected);
        Assert.Equal(bookmark, m.Bookmark);

        // Reopening a new menu session with the saved bookmark restores topic and step
        var reopened = new OverlayMenu(100_000, bookmark);
        long t2 = 100_001;
        Select(reopened, MenuRow.Guide, ref t2);
        reopened.Handle(MenuCommand.Increase, t2);
        Assert.Equal(MenuPage.Guide, reopened.Page);
        Assert.Equal(GuideTopic.AmpSetup, reopened.Topic);
        Assert.Equal(MenuRow.GuideStep3, reopened.Selected);
    }

    [Fact]
    public void GuideText_ProvidesSixStepsPerTopic()
    {
        Assert.Equal(6, GuideText.Steps(GuideTopic.TableTuning).Count);
        Assert.Equal(6, GuideText.Steps(GuideTopic.AmpSetup).Count);
        Assert.All(GuideText.Steps(GuideTopic.TableTuning), s => Assert.False(string.IsNullOrWhiteSpace(s.Action)));
        Assert.All(GuideText.Steps(GuideTopic.AmpSetup), s => Assert.False(string.IsNullOrWhiteSpace(s.Action)));
    }

    [Fact]
    public void WindowRow_TogglesEditingWindow_EffectsCarryIt()
    {
        var m = OnDisplayPage();
        long now = 10;

        Assert.Null(m.Handle(MenuCommand.Increase, now++));
        Assert.Equal(OverlayKind.Menu, m.EditingWindow);

        Select(m, MenuRow.Rotation, ref now);
        Assert.Equal(new MenuEffect(MenuRow.Rotation, +1, OverlayKind.Menu), m.Handle(MenuCommand.Increase, now++));

        Select(m, MenuRow.Window, ref now);
        m.Handle(MenuCommand.Decrease, now);
        Assert.Equal(OverlayKind.Popup, m.EditingWindow);
    }

    [Fact]
    public void Reset_NeedsTwoPresses_Within3s()
    {
        var m = Open();
        long now = 1;
        Select(m, MenuRow.Reset, ref now);

        Assert.Null(m.Handle(MenuCommand.Increase, 1000));
        Assert.True(m.ResetArmed);
        Assert.Equal(new MenuEffect(MenuRow.Reset, +1, OverlayKind.Popup), m.Handle(MenuCommand.Decrease, 3000));
        Assert.False(m.ResetArmed);
    }

    [Fact]
    public void Reset_Disarms_AfterMoveOrTimeout()
    {
        var m = Open();
        long now = 1;
        Select(m, MenuRow.Reset, ref now);

        m.Handle(MenuCommand.Increase, 1000);
        m.Handle(MenuCommand.Down, 1100);
        Assert.False(m.ResetArmed);

        m.Handle(MenuCommand.Up, 1200);
        m.Handle(MenuCommand.Increase, 1300);
        Assert.True(m.ResetArmed);
        m.Tick(4301);
        Assert.False(m.ResetArmed);

        m.Handle(MenuCommand.Increase, 4302);
        Assert.True(m.ResetArmed);
        // Pressing after more than 3 s only arms again; it does not reset.
        Assert.Null(m.Handle(MenuCommand.Increase, 7303));
        Assert.True(m.ResetArmed);
    }

    [Fact]
    public void Close_ClosesAndIgnoresLaterInput()
    {
        var m = Open();

        Assert.Null(m.Handle(MenuCommand.Close, 1));
        Assert.True(m.IsClosed);
        Assert.Null(m.Handle(MenuCommand.Increase, 2));
        Assert.Equal(MenuRow.TableVolume, m.Selected);
    }

    [Fact]
    public void Idle15s_Closes()
    {
        var m = Open();

        m.Tick(14_999);
        Assert.False(m.IsClosed);
        m.Tick(15_000);
        Assert.True(m.IsClosed);
    }

    [Fact]
    public void Input_RestartsIdleTimer_AndLateInputCloses()
    {
        var m = Open();
        m.Handle(MenuCommand.Down, 10_000);
        m.Tick(24_999);
        Assert.False(m.IsClosed);

        Assert.Null(m.Handle(MenuCommand.Increase, 40_000));
        Assert.True(m.IsClosed);
    }
}
