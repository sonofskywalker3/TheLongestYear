using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The one-time respec for the 2026-08-30 XP rebalance: refund at the OLD prices, take
/// the tiers back, never pay out twice.</summary>
public class XpLadderRespecTests
{
    private static MetaState With(long jp, params string[] upgrades)
    {
        var meta = new MetaState { JunimoPoints = jp };
        foreach (string u in upgrades) meta.OwnedUpgrades.Add(u);
        return meta;
    }

    /// <summary>gazumbrado's actual purchase: tier 1 in all five skills at the old 100 JP each.
    /// He gets 500 JP back and an empty chain, which under the new ladder buys three tier 1s.</summary>
    [Fact]
    public void RefundsEveryTierAtItsOldPrice_AndClearsThem()
    {
        var meta = With(0,
            "xp_mult_farming_1", "xp_mult_fishing_1", "xp_mult_foraging_1",
            "xp_mult_mining_1", "xp_mult_combat_1");

        long refund = XpLadderRespec.Respec(meta, out int cleared);

        Assert.Equal(500, refund);
        Assert.Equal(5, cleared);
        Assert.Equal(500, meta.JunimoPoints);
        Assert.Empty(meta.OwnedUpgrades);
    }

    [Fact]
    public void RefundsAFullChainAndTheCapstone()
    {
        var meta = With(25,
            "xp_mult_farming_1", "xp_mult_farming_2", "xp_mult_farming_3", "xp_mult_farming_4",
            XpMultiplierRules.CapstoneId);

        long refund = XpLadderRespec.Respec(meta, out int cleared);

        Assert.Equal(100 + 200 + 350 + 550 + 3000, refund);
        Assert.Equal(5, cleared);
        Assert.Equal(25 + 4200, meta.JunimoPoints);
    }

    [Fact]
    public void LeavesEveryOtherUpgradeAlone()
    {
        var meta = With(0, "xp_mult_mining_2", "keep_bus_unlocked", "early_horse");

        long refund = XpLadderRespec.Respec(meta, out int cleared);

        Assert.Equal(200, refund);
        Assert.Equal(1, cleared);
        Assert.Equal(new[] { "keep_bus_unlocked", "early_horse" }, meta.OwnedUpgrades);
    }

    [Fact]
    public void RunsOnce_EvenOnASaveThatOwnedNothing()
    {
        var meta = With(0);
        Assert.True(XpLadderRespec.IsOwed(meta));

        Assert.Equal(0, XpLadderRespec.Respec(meta, out int cleared));
        Assert.Equal(0, cleared);
        Assert.False(XpLadderRespec.IsOwed(meta));

        // A tier bought AFTER the respec is a new-ladder purchase and must not be refunded again.
        meta.OwnedUpgrades.Add("xp_mult_farming_1");
        Assert.Equal(0, XpLadderRespec.Respec(meta, out int clearedAgain));
        Assert.Equal(0, clearedAgain);
        Assert.Equal(0, meta.JunimoPoints);
        Assert.Contains("xp_mult_farming_1", meta.OwnedUpgrades);
    }

    /// <summary>An id in the family that this build does not know refunds nothing rather than
    /// guessing a price, so a mod-added tier cannot mint points.</summary>
    [Theory]
    [InlineData("xp_mult_farming_1", 100)]
    [InlineData("xp_mult_combat_4", 550)]
    [InlineData("xp_mult_all", 3000)]
    [InlineData("xp_mult_farming_9", 0)]
    [InlineData("xp_mult_cooking_1", 0)]
    [InlineData("keep_bus_unlocked", 0)]
    [InlineData(null, 0)]
    public void OldCostOf_KnowsTheOldLadderOnly(string? id, long expected)
        => Assert.Equal(expected, XpLadderRespec.OldCostOf(id));
}
