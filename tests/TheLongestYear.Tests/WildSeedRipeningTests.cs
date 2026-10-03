using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WildSeedRipeningTests
{
    [Fact]
    public void Wild_seed_crop_on_its_last_phase_becomes_forage()
        => Assert.True(WildSeedRipening.ShouldBecomeForage(true, false, 4, 5));

    [Fact]
    public void Wild_seed_crop_still_growing_stays_a_crop()
        => Assert.False(WildSeedRipening.ShouldBecomeForage(true, false, 2, 5));

    [Fact]
    public void Normal_crop_on_its_last_phase_stays_a_crop()
        => Assert.False(WildSeedRipening.ShouldBecomeForage(false, false, 4, 5));

    [Fact]
    public void Dead_wild_seed_crop_stays_a_crop()
        => Assert.False(WildSeedRipening.ShouldBecomeForage(true, true, 4, 5));

    [Fact]
    public void Crop_with_no_phases_stays_a_crop()
        => Assert.False(WildSeedRipening.ShouldBecomeForage(true, false, 0, 0));
}
