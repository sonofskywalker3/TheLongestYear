using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: the crows swoop in from the sides, not straight down.</summary>
public class CrowSwoopTests
{
    private static readonly (float X, float Y) Start = (0f, 0f);
    private static readonly (float X, float Y) Landing = (640f, 256f);

    [Fact]
    public void The_flight_starts_at_the_edge_and_ends_on_the_crop()
    {
        Assert.Equal(Start, CrowSwoop.At(Start, Landing, 0f));
        Assert.Equal(Landing, CrowSwoop.At(Start, Landing, 1f));
    }

    [Fact]
    public void It_is_fast_on_entry_and_brakes_to_land()
    {
        float early = CrowSwoop.Progress(0.1f) - CrowSwoop.Progress(0f);
        float late = CrowSwoop.Progress(1f) - CrowSwoop.Progress(0.9f);
        Assert.True(early > late * 5, $"early {early}, late {late}");
    }

    [Fact]
    public void It_drops_steeply_first_and_comes_in_nearly_level()
    {
        var a = CrowSwoop.At(Start, Landing, 0.02f);
        var b = CrowSwoop.At(Start, Landing, 0.98f);
        float entrySlope = (a.Y - Start.Y) / (a.X - Start.X);
        float landingSlope = (Landing.Y - b.Y) / (Landing.X - b.X);
        Assert.True(entrySlope > landingSlope * 2, $"entry {entrySlope}, landing {landingSlope}");
        Assert.True(landingSlope > 0, "still descending as it lands");
    }

    [Fact]
    public void It_moves_sideways_the_whole_way_and_never_drops_straight_down()
    {
        float lastX = Start.X;
        for (int i = 1; i <= 20; i++)
        {
            var p = CrowSwoop.At(Start, Landing, i / 20f);
            Assert.True(p.X > lastX, $"step {i}");
            lastX = p.X;
        }
    }

    [Fact]
    public void It_never_dips_below_the_landing()
    {
        for (int i = 0; i <= 20; i++)
            Assert.True(CrowSwoop.At(Start, Landing, i / 20f).Y <= Landing.Y + 0.01f);
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    public void The_flock_comes_in_from_both_sides(int index, bool firstFromLeft, bool fromLeft)
        => Assert.Equal(fromLeft, CrowSwoop.FromLeft(index, firstFromLeft));
}
