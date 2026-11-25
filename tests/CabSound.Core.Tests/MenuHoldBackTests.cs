using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class MenuHoldBackTests
{
    private const int LFlip = 0xA0, LMagna = 0xA2, Start = 0x78, Esc = 0x1B;

    private static MenuHoldBack Make() =>
        new(new MenuKeys(LFlip, 0xA1, LMagna, 0xA3, Start, 0x50));

    [Fact]
    public void Inactive_PassesAll()
    {
        var h = Make();

        Assert.False(h.Decide(LFlip, true));
        Assert.False(h.Decide(LFlip, false));
        Assert.False(h.Decide(Start, true));
        Assert.False(h.Decide(Start, false));
    }

    [Fact]
    public void Active_HoldsMenuKeyDownAndUp()
    {
        var h = Make();
        h.SetActive(true);

        Assert.True(h.Decide(LFlip, true));
        Assert.True(h.Decide(LFlip, true)); // auto-repeat while held
        Assert.True(h.Decide(LFlip, false));
    }

    [Fact]
    public void Active_PassesOtherKeys()
    {
        var h = Make();
        h.SetActive(true);

        Assert.False(h.Decide(Esc, true));
        Assert.False(h.Decide(Esc, false));
    }

    [Fact]
    public void KeyDownBeforeOpen_RepeatsAndUpPass()
    {
        var h = Make();
        h.Decide(LMagna, true);
        h.SetActive(true);

        Assert.False(h.Decide(LMagna, true));
        Assert.False(h.Decide(LMagna, false));
    }

    [Fact]
    public void HeldKey_UpAfterClose_StillHeldBack()
    {
        var h = Make();
        h.SetActive(true);
        Assert.True(h.Decide(Start, true));

        h.SetActive(false);

        Assert.True(h.Decide(Start, false));
        Assert.False(h.Decide(Start, true));
    }

    [Fact]
    public void PressAfterAMissedKeyUp_IsNotHeldBackOnceTheMenuIsClosed()
    {
        // The flipper's key-up was lost while the menu was open; its next press in the game must reach VPX
        const int LShift = 0xA0, RShift = 0xA1;
        var hold = new MenuHoldBack(new MenuKeys(LShift, RShift, 0xA2, 0xA3, 0x78, 0x50));
        hold.SetActive(true);
        Assert.True(hold.Decide(LShift, true));
        Assert.True(hold.Decide(RShift, true));
        Assert.True(hold.Decide(RShift, false)); // LShift's key-up never arrives
        hold.SetActive(false);

        Assert.False(hold.Decide(LShift, true));
        Assert.False(hold.Decide(LShift, false));
    }

    [Fact]
    public void WindowsKeyRepeat_KeepsTheFirstDecision()
    {
        const int LShift = 0xA0;
        var hold = new MenuHoldBack(new MenuKeys(LShift, 0xA1, 0xA2, 0xA3, 0x78, 0x50));
        hold.SetActive(true);
        Assert.True(hold.Decide(LShift, true));
        hold.SetActive(false);

        Assert.True(hold.Decide(LShift, true)); // same press repeating: VPX never saw its down
        Assert.True(hold.Decide(LShift, false));
    }
}
