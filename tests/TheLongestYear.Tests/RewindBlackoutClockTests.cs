using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class RewindBlackoutClockTests
{
    [Fact]
    public void Time_on_a_bare_black_screen_counts()
        => Assert.Equal(116f, RewindBlackoutClock.HeldAfter(100f, 16f, menuOpen: false));

    [Fact]
    public void Time_in_a_menu_does_not_count()
        => Assert.Equal(100f, RewindBlackoutClock.HeldAfter(100f, 16f, menuOpen: true));

    [Fact]
    public void Three_minutes_in_the_shrine_never_reach_the_backstop()
    {
        float held = 0f;
        for (int tick = 0; tick < 60 * 60 * 3; tick++)   // three minutes at 60 ticks a second
            held = RewindBlackoutClock.HeldAfter(held, 1000f / 60f, menuOpen: true);
        Assert.Equal(0f, held);
    }
}
