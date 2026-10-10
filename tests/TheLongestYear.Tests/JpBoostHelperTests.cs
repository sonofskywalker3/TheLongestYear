using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpBoostHelperTests
{
    private static MetaState Owning(params string[] upgrades)
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.AddRange(upgrades);
        return meta;
    }

    [Fact]
    public void No_tier_owned_pays_the_base()
    {
        var meta = Owning();
        Assert.Equal(0, JpBoostHelper.HighestTier(meta));
        Assert.Equal(100, JpBoostHelper.Apply(meta, 100));
    }

    [Fact]
    public void Tier_one_adds_five_percent()
        => Assert.Equal(105, JpBoostHelper.Apply(Owning("jp_boost_1"), 100));

    [Fact]
    public void Tier_five_adds_twenty_five_percent()
    {
        var meta = Owning("jp_boost_1", "jp_boost_2", "jp_boost_3", "jp_boost_4", "jp_boost_5");
        Assert.Equal(JpBoostHelper.MaxTier, JpBoostHelper.HighestTier(meta));
        Assert.Equal(125, JpBoostHelper.Apply(meta, 100));
    }

    [Fact]
    public void Highest_tier_wins_without_stacking()
    {
        // Owning only tier 3 is +15%, not +5 +10 +15.
        var meta = Owning("jp_boost_3");
        Assert.Equal(3, JpBoostHelper.HighestTier(meta));
        Assert.Equal(115, JpBoostHelper.Apply(meta, 100));
    }

    [Theory]
    [InlineData("jp_boost_1", 10, 11)]   // 10.5 rounds away from zero
    [InlineData("jp_boost_5", 30, 38)]   // 37.5 rounds away from zero
    [InlineData("jp_boost_2", 7, 8)]     // 7.7 rounds to nearest
    public void Rounds_half_away_from_zero(string upgrade, long baseAmount, long expected)
        => Assert.Equal(expected, JpBoostHelper.Apply(Owning(upgrade), baseAmount));

    [Theory]
    [InlineData(0)]
    [InlineData(-40)]
    public void Zero_and_negative_bases_pass_through(long baseAmount)
        => Assert.Equal(baseAmount, JpBoostHelper.Apply(Owning("jp_boost_5"), baseAmount));

    [Fact]
    public void Null_meta_pays_the_base_and_has_no_tier()
    {
        Assert.Equal(50, JpBoostHelper.Apply(null!, 50));
        Assert.Equal(0, JpBoostHelper.HighestTier(null!));
    }
}
