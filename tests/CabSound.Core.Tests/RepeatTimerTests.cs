using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class RepeatTimerTests
{
    [Fact]
    public void Tick_BeforeFirstDelay_Nothing()
    {
        var r = new RepeatTimer<CabAction>();
        r.Start(CabAction.TableUp, 0);

        Assert.Empty(r.Tick(399));
    }

    [Fact]
    public void Tick_AfterFirstDelay_RepeatsEveryInterval()
    {
        var r = new RepeatTimer<CabAction>();
        r.Start(CabAction.TableUp, 0);

        Assert.Equal([CabAction.TableUp], r.Tick(400));
        Assert.Empty(r.Tick(450));
        Assert.Equal([CabAction.TableUp], r.Tick(500));
    }

    [Fact]
    public void Tick_LateTick_FiresOncePerTick()
    {
        var r = new RepeatTimer<CabAction>();
        r.Start(CabAction.TableUp, 0);

        Assert.Equal([CabAction.TableUp], r.Tick(1000));
    }

    [Fact]
    public void Stop_EndsRepeat()
    {
        var r = new RepeatTimer<CabAction>();
        r.Start(CabAction.TableUp, 0);
        r.Stop(CabAction.TableUp);

        Assert.Empty(r.Tick(1000));
    }

    [Fact]
    public void TwoActions_RepeatIndependently()
    {
        var r = new RepeatTimer<CabAction>();
        r.Start(CabAction.TableUp, 0);
        r.Start(CabAction.FrontUp, 200);

        Assert.Equal([CabAction.TableUp], r.Tick(400));
        Assert.Contains(CabAction.FrontUp, r.Tick(600));
    }

    [Fact]
    public void WorksWithAnyKey()
    {
        var r = new RepeatTimer<int>();
        r.Start(7, 0);

        Assert.Empty(r.Tick(399));
        Assert.Equal([7], r.Tick(400));
    }
}
