using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ButtonTwinsTests
{
    [Fact]
    public void Learn_KeyAndButtonPressedTogether_ArePaired()
    {
        var t = new ButtonTwins();

        t.Learn(InputId.Key(0xA0), 1000);
        t.Learn(InputId.Joy(4), 1020);   // 20 ms later: same physical button

        Assert.Equal(new[] { InputId.Joy(4), InputId.Key(0xA0) }, t.Expand(InputId.Joy(4)));
    }

    [Fact]
    public void Learn_FarApartPresses_AreNotPaired()
    {
        var t = new ButtonTwins();

        t.Learn(InputId.Key(0xA0), 1000);
        t.Learn(InputId.Joy(20), 1500);

        Assert.Equal(new[] { InputId.Joy(20) }, t.Expand(InputId.Joy(20)));
    }

    [Fact]
    public void Learn_TwoJoystickButtons_AreNeverPaired()
    {
        // Pressing both flippers at once is two buttons, not one
        var t = new ButtonTwins();

        t.Learn(InputId.Joy(3), 1000);
        t.Learn(InputId.Joy(4), 1002);

        Assert.Equal(new[] { InputId.Joy(4) }, t.Expand(InputId.Joy(4)));
    }

    [Fact]
    public void Expand_WorksFromEitherSide()
    {
        var t = new ButtonTwins();
        t.Pair(InputId.Joy(20), InputId.Key(0xAF));

        Assert.Contains(InputId.Joy(20), t.Expand(InputId.Key(0xAF)));
    }
}
