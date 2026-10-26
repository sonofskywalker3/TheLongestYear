using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class KeenEdgeGuardTests
{
    private sealed class Weapon
    {
        public int Min = 2, Max = 5;
    }

    private static KeenEdgeGuard.State Begin(KeenEdgeGuard g, Weapon w, int tier, int ruby)
        => g.Begin(() => (GearPowers.ExtraDamage(tier, ruby, 2), GearPowers.ExtraDamage(tier, ruby, 5)),
            (min, max) => { w.Min += min; w.Max += max; });

    private static void End(KeenEdgeGuard g, Weapon w, KeenEdgeGuard.State s)
        => g.End(s, (min, max) => { w.Min -= min; w.Max -= max; });

    [Fact]
    public void Ruby_covered_weapon_does_not_stick_the_guard()
    {
        var g = new KeenEdgeGuard();
        var covered = new Weapon();
        var s1 = Begin(g, covered, tier: 2, ruby: 2);
        Assert.Equal((2, 5), (covered.Min, covered.Max));
        End(g, covered, s1);

        var plain = new Weapon();
        var s2 = Begin(g, plain, tier: 2, ruby: 0);
        Assert.Equal((4, 7), (plain.Min, plain.Max));
        End(g, plain, s2);
        Assert.Equal((2, 5), (plain.Min, plain.Max));
    }

    [Fact]
    public void Nested_call_does_not_double_add_and_outer_end_restores()
    {
        var g = new KeenEdgeGuard();
        var w = new Weapon();
        var outer = Begin(g, w, 1, 0);
        var inner = Begin(g, w, 1, 0);
        Assert.False(inner.Owner);
        Assert.Equal((3, 6), (w.Min, w.Max));
        End(g, w, inner);
        Assert.Equal((3, 6), (w.Min, w.Max));
        End(g, w, outer);
        Assert.Equal((2, 5), (w.Min, w.Max));

        var again = Begin(g, w, 1, 0);
        Assert.Equal((3, 6), (w.Min, w.Max));
        End(g, w, again);
    }
}
