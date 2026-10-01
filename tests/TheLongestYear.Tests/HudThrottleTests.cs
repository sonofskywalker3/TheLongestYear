using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class HudThrottleTests
{
    [Fact]
    public void First_call_fires()
        => Assert.True(new HudThrottle(2000).TryFire(10_000));

    [Fact]
    public void Second_call_inside_the_interval_is_swallowed()
    {
        var t = new HudThrottle(2000);
        Assert.True(t.TryFire(10_000));
        Assert.False(t.TryFire(11_999));
    }

    [Fact]
    public void Call_after_the_interval_fires_again()
    {
        var t = new HudThrottle(2000);
        Assert.True(t.TryFire(10_000));
        Assert.True(t.TryFire(12_000));
        Assert.False(t.TryFire(13_000));
    }

    [Fact]
    public void Clock_going_backwards_fires_instead_of_sticking()
    {
        // A new game session restarts the clock: never stay silent forever.
        var t = new HudThrottle(2000);
        Assert.True(t.TryFire(50_000));
        Assert.True(t.TryFire(100));
    }
}
