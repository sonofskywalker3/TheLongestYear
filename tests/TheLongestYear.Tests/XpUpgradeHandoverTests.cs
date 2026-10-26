using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The 2026-10-26 hand-over from the per-skill XP chains and Junimo Insight to Exp Boost
/// and Mastery Boost: at the next rewind the old ids are removed and refunded at the catalog base
/// price times the loop's shrine price factor, once.</summary>
public class XpUpgradeHandoverTests
{
    private static MetaState With(long jp, params string[] upgrades)
    {
        var meta = new MetaState { JunimoPoints = jp };
        foreach (string u in upgrades) meta.OwnedUpgrades.Add(u);
        return meta;
    }

    [Theory]
    [InlineData("xp_mult_farming_1", true)]
    [InlineData("xp_mult_combat_4", true)]
    [InlineData("xp_mult_all", true)]
    [InlineData("xp_mult_farming_5", false)]
    [InlineData("xp_mult_cooking_1", false)]
    [InlineData("exp_boost_1", false)]
    [InlineData("mastery_boost_1", false)]
    [InlineData("jp_boost_1", false)]
    public void IsRetired_KnowsExactlyTheOldIds(string id, bool expected)
        => Assert.Equal(expected, XpUpgradeHandover.IsRetired(id));

    [Theory]
    [InlineData("xp_mult_farming_1", 150)]
    [InlineData("xp_mult_fishing_2", 350)]
    [InlineData("xp_mult_mining_3", 650)]
    [InlineData("xp_mult_combat_4", 1000)]
    [InlineData("xp_mult_all", 3000)]
    [InlineData("xp_mult_farming_9", 0)]
    public void BaseCost_IsTheRetiredCatalogPrice(string id, long expected)
        => Assert.Equal(expected, XpUpgradeHandover.BaseCostOf(id));

    [Fact]
    public void NothingOwed_DoesNothing()
    {
        var meta = With(42, "jp_boost_1", "exp_boost_1");
        Assert.False(XpUpgradeHandover.IsOwed(meta));
        var result = XpUpgradeHandover.Apply(meta, 1.0);
        Assert.Equal(0, result.Refund);
        Assert.Empty(result.Removed);
        Assert.Equal(42, meta.JunimoPoints);
        Assert.Null(meta.PendingXpHandoverNotice);
    }

    /// <summary>A full farming chain plus fishing I: 150+350+650+1000 + 150 = 2,300 JP back,
    /// every old id gone, everything else kept, and a notice waiting.</summary>
    [Fact]
    public void RemovesAndRefunds_AtBasePrice_KeepingEverythingElse()
    {
        var meta = With(10,
            "xp_mult_farming_1", "xp_mult_farming_2", "xp_mult_farming_3", "xp_mult_farming_4",
            "xp_mult_fishing_1", "jp_boost_1", "exp_boost_1");

        var result = XpUpgradeHandover.Apply(meta, 1.0);

        Assert.Equal(2300, result.Refund);
        Assert.Equal(5, result.Removed.Count);
        Assert.Equal(2310, meta.JunimoPoints);
        Assert.Equal(new[] { "jp_boost_1", "exp_boost_1" }, meta.OwnedUpgrades);
        Assert.False(XpUpgradeHandover.IsOwed(meta));

        var notice = meta.PendingXpHandoverNotice;
        Assert.NotNull(notice);
        Assert.Equal(2300, notice!.Refund);
        Assert.False(notice.HadInsight);
        // Highest tier per skill only, so the notice names "Farming +100%", not all four rungs.
        Assert.Equal(new[] { "xp_mult_farming_4", "xp_mult_fishing_1" }, notice.Retired);
    }

    [Fact]
    public void Insight_IsRefundedAndFlagged()
    {
        string[] full = new[] { "farming", "fishing", "foraging", "mining", "combat" }
            .SelectMany(s => Enumerable.Range(1, 4).Select(t => $"xp_mult_{s}_{t}"))
            .Append("xp_mult_all").ToArray();
        var meta = With(0, full);

        var result = XpUpgradeHandover.Apply(meta, 1.0);

        Assert.Equal(5 * 2150 + 3000, result.Refund);
        Assert.Equal(21, result.Removed.Count);
        Assert.Empty(meta.OwnedUpgrades);
        Assert.True(meta.PendingXpHandoverNotice!.HadInsight);
        Assert.Equal(6, meta.PendingXpHandoverNotice.Retired.Count);
        Assert.Equal("xp_mult_all", meta.PendingXpHandoverNotice.Retired.Last());
    }

    /// <summary>The price factor rounds each row the way a purchase does (UpgradePricing), so
    /// 150 at x1.25 is 188 (187.5 away from zero) and 350 is 438 (437.5).</summary>
    [Fact]
    public void PriceFactor_RoundsPerRow_LikeAPurchase()
    {
        var meta = With(0, "xp_mult_farming_1", "xp_mult_farming_2");
        var result = XpUpgradeHandover.Apply(meta, 1.25);
        Assert.Equal(188 + 438, result.Refund);
        Assert.Equal(626, meta.JunimoPoints);
    }

    [Fact]
    public void RunsOnce_SecondCallRefundsNothing()
    {
        var meta = With(0, "xp_mult_mining_2", "xp_mult_mining_1");
        XpUpgradeHandover.Apply(meta, 1.0);
        var again = XpUpgradeHandover.Apply(meta, 1.0);
        Assert.Equal(0, again.Refund);
        Assert.Equal(500, meta.JunimoPoints);
        Assert.Equal(500, meta.PendingXpHandoverNotice!.Refund);
    }

    [Fact]
    public void UnknownFamilyIds_AreLeftAlone_NeverMintPoints()
    {
        var meta = With(0, "xp_mult_farming_9");
        Assert.False(XpUpgradeHandover.IsOwed(meta));
        Assert.Equal(0, XpUpgradeHandover.Apply(meta, 1.0).Refund);
        Assert.Contains("xp_mult_farming_9", meta.OwnedUpgrades);
    }

    [Fact]
    public void Notice_IsTakenOnce()
    {
        var meta = With(0, "xp_mult_combat_1");
        XpUpgradeHandover.Apply(meta, 1.0);
        Assert.NotNull(XpUpgradeHandover.TakeNotice(meta));
        Assert.Null(XpUpgradeHandover.TakeNotice(meta));
    }
}
