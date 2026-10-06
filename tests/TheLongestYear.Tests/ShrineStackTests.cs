using System;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ShrineStackTests
{
    [Fact]
    public void Without_a_basis_it_is_one_scaled_by_the_stack_factor()
    {
        var profile = new DifficultyProfile { StackFactor = 3.0 };
        Assert.Equal(StackScaling.ScaleStack(1, 3.0), ShrineStack.For(null, profile, new Random(1), x => x));
    }

    [Fact]
    public void A_basis_rolls_in_the_band_and_clamps_apply_then_1_to_99()
    {
        var profile = new DifficultyProfile();
        int rolled = ShrineStack.For(10, profile, new Random(5), x => x);
        Assert.Equal(AskBands.Roll(10, profile, new Random(5)), rolled);
        Assert.Equal(1, ShrineStack.For(10, profile, new Random(5), _ => 1));
        Assert.Equal(99, ShrineStack.For(10, profile, new Random(5), _ => 500));
        Assert.Equal(1, ShrineStack.For(null, profile, new Random(5), _ => -4));
    }
}
