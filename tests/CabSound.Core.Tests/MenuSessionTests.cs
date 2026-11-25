using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class MenuSessionTests
{
    private const int LShift = 0xA0, RShift = 0xA1, LCtrl = 0xA2, RCtrl = 0xA3, F9 = 0x78, VolUp = 0xAF;

    private static MenuSession New() =>
        new(new MenuKeys(LShift, RShift, LCtrl, RCtrl, F9, 0x50));

    private static MenuSession Opened()
    {
        var s = New();
        s.Open(pauseTable: false, 0);
        return s;
    }

    private static readonly MenuSignal Pause = new(MenuSignalKind.TapPause);
    private static readonly MenuSignal CloseSignal = new(MenuSignalKind.Close);
    private static MenuSignal Cmd(MenuCommand c) => new(MenuSignalKind.Command, c);

    [Fact]
    public void NotOpen_NoSignals()
    {
        var s = New();

        Assert.Empty(s.Key(LShift, true, 0));
        Assert.Empty(s.Key(F9, true, 10));
        Assert.Empty(s.Key(F9, true, 600)); // Windows' key repeat
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void Open_CtrlDown_PausesOnCtrlUp()
    {
        var s = New();
        s.Key(LCtrl, true, 0);

        Assert.Empty(s.Open(true, 10));
        Assert.True(s.IsOpen);
        Assert.Equal([Pause], s.Key(LCtrl, false, 50));
    }

    [Fact]
    public void Open_NoCtrl_PausesAtOnce()
    {
        var s = New();

        Assert.Equal([Pause], s.Open(true, 0));
    }

    [Fact]
    public void Open_NoPause_ForNonVpx()
    {
        var s = New();

        Assert.Empty(s.Open(false, 0));
        Assert.Equal([CloseSignal], s.Key(F9, true, 10));
    }

    // A held key repeats on Windows' own key repeat (a "down" for the key pressed last), never on a timer:
    // when the key is let go the repeat stops, even if its key-up gets lost.
    [Fact]
    public void HeldFlipper_RepeatsOnWindowsKeyRepeat_AtMostEvery100Ms()
    {
        var s = Opened();

        Assert.Equal([Cmd(MenuCommand.Up)], s.Key(LShift, true, 0));
        Assert.Equal([Cmd(MenuCommand.Up)], s.Key(LShift, true, 500));
        Assert.Empty(s.Key(LShift, true, 530));
        Assert.Empty(s.Key(LShift, true, 560));
        Assert.Equal([Cmd(MenuCommand.Up)], s.Key(LShift, true, 600));
    }

    [Fact]
    public void RepeatOfOpeningKeys_IsIgnored()
    {
        var s = New();
        s.Key(LCtrl, true, 0);
        s.Key(VolUp, true, 0);
        s.Open(false, 10);

        Assert.Empty(s.Key(LCtrl, true, 500));
        Assert.Empty(s.Key(VolUp, true, 500));
        Assert.Empty(s.Key(LCtrl, true, 2000));
        s.Key(LCtrl, false, 2100);
        s.Key(VolUp, false, 2100);

        Assert.Equal([CloseSignal], s.Key(VolUp, true, 2200));
    }

    [Fact]
    public void OpeningMagna_WorksAgainAfterItIsReleased()
    {
        var s = New();
        s.Key(LCtrl, true, 0);
        s.Open(false, 10);
        s.Key(LCtrl, false, 100);

        Assert.Equal([Cmd(MenuCommand.Decrease)], s.Key(LCtrl, true, 200));
    }

    [Fact]
    public void Start_Closes_AndResumes()
    {
        var s = New();
        s.Open(true, 0);

        Assert.Equal([CloseSignal, Pause], s.Key(F9, true, 100));
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void HeldStartKey_DoesNotRepeat()
    {
        var s = Opened();
        s.Key(F9, true, 0);  // closes
        s.Open(false, 100);  // opened again while Start is still down

        Assert.Empty(s.Key(F9, true, 600));
        Assert.True(s.IsOpen);
    }

    [Fact]
    public void Close_WithoutPause_WhenTableGone()
    {
        var s = New();
        s.Open(true, 0);

        Assert.Empty(s.Close(tableStillRunning: false));
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void Close_BeforePauseSent_DropsPause()
    {
        var s = New();
        s.Key(LCtrl, true, 0);
        s.Open(true, 10);

        Assert.Empty(s.Close());
        Assert.Empty(s.Key(LCtrl, false, 50));
    }

    [Fact]
    public void Close_WhenPauseWasSent_Resumes()
    {
        var s = New();
        s.Open(true, 0);

        Assert.Equal([Pause], s.Close());
    }

    [Fact]
    public void Close_WhenAlreadyClosed_IsEmpty()
    {
        Assert.Empty(New().Close());
    }

    [Fact]
    public void Close_StopsRepeats()
    {
        var s = Opened();
        s.Key(LShift, true, 0);
        s.Close();

        Assert.Empty(s.Key(LShift, true, 500));
    }

    // --- Lost key-ups (owner 2026-11-11: the menu scrolled by itself) ---
    // Both flippers are Shift keys. Cabinet log 2026-11-11 12:02:23: with both pressed, Windows sent
    // no key-up for either one, and the old timer repeat ran for 13 s.

    [Fact]
    public void OnlyTheLatestPressRepeats()
    {
        var s = Opened();
        s.Key(LShift, true, 0);
        s.Key(RShift, true, 100);

        Assert.Equal([Cmd(MenuCommand.Down)], s.Key(RShift, true, 600));
    }

    [Fact]
    public void LostKeyUps_OfBothFlippers_NextPressesStillCount()
    {
        var s = Opened();
        s.Key(LShift, true, 0);
        s.Key(RShift, true, 100);
        // both released here, but neither key-up arrives

        Assert.Equal([Cmd(MenuCommand.Up)], s.Key(LShift, true, 1000));
        Assert.Equal([Cmd(MenuCommand.Down)], s.Key(RShift, true, 1050));
    }

    [Fact]
    public void PressOfTheSameKey_AfterItsLostKeyUp_StillCounts()
    {
        var s = Opened();
        s.Key(LShift, true, 0);
        // released here, but the key-up never arrives

        Assert.Equal([Cmd(MenuCommand.Up)], s.Key(LShift, true, 1000));
    }
}
