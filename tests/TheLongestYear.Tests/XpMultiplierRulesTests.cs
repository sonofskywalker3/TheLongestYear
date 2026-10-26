using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class XpMultiplierRulesTests
{
    private static MetaState With(params string[] upgrades)
    {
        var meta = new MetaState();
        foreach (var u in upgrades) meta.OwnedUpgrades.Add(u);
        return meta;
    }

    [Fact]
    public void NoUpgrades_PercentIsOneHundred()
    {
        Assert.Equal(100, XpMultiplierRules.PercentFor(With(), which: 0, allSkillsMaxed: false));
        Assert.Equal(100, XpMultiplierRules.PercentFor(With(), which: 0, allSkillsMaxed: true));
    }

    /// <summary>Exp Boost (Jeff, 2026-10-26): I +25, II +50, III +75, IV +100, V +150, on all
    /// five skills at once. Highest owned tier wins; the tiers do not add up.</summary>
    [Theory]
    [InlineData(1, 125)]
    [InlineData(2, 150)]
    [InlineData(3, 175)]
    [InlineData(4, 200)]
    [InlineData(5, 250)]
    public void ExpBoost_HighestTierWins_OnEverySkill(int tier, int expected)
    {
        var owned = new string[tier];
        for (int t = 1; t <= tier; t++) owned[t - 1] = XpMultiplierRules.ExpBoostId(t);
        var meta = With(owned);
        for (int which = 0; which <= 4; which++)
            Assert.Equal(expected, XpMultiplierRules.PercentFor(meta, which, allSkillsMaxed: false));
    }

    [Fact]
    public void ExpBoost_LoneHighTier_StillCountsAsThatTier()
        => Assert.Equal(200, XpMultiplierRules.PercentFor(With("exp_boost_4"), which: 3, allSkillsMaxed: false));

    [Fact]
    public void ExpBoost_DoesNotTouchMastery_OrUnknownSkills()
    {
        var meta = With("exp_boost_1", "exp_boost_2", "exp_boost_3", "exp_boost_4", "exp_boost_5");
        Assert.Equal(100, XpMultiplierRules.PercentFor(meta, which: 0, allSkillsMaxed: true));
        Assert.Equal(100, XpMultiplierRules.PercentFor(meta, which: 5, allSkillsMaxed: false));
    }

    /// <summary>Mastery Boost: I +50, II +100, Mastery XP only, highest tier wins.</summary>
    [Theory]
    [InlineData(new string[0], 100)]
    [InlineData(new[] { "mastery_boost_1" }, 150)]
    [InlineData(new[] { "mastery_boost_1", "mastery_boost_2" }, 200)]
    public void MasteryBoost_AppliesToMasteryOnly(string[] owned, int expected)
    {
        var meta = With(owned);
        Assert.Equal(expected, XpMultiplierRules.PercentFor(meta, which: 2, allSkillsMaxed: true));
        Assert.Equal(100, XpMultiplierRules.PercentFor(meta, which: 2, allSkillsMaxed: false));
    }

    /// <summary>The retired per-skill chains keep working until the hand-over: tier N is +25N%
    /// on its own skill, and Junimo Insight adds 50 on every skill and on Mastery.</summary>
    [Theory]
    [InlineData("xp_mult_farming_1", 125)]
    [InlineData("xp_mult_farming_2", 150)]
    [InlineData("xp_mult_farming_3", 175)]
    [InlineData("xp_mult_farming_4", 200)]
    public void LegacyPerSkillTier_StillWorksDuringHandover(string owned, int expected)
    {
        Assert.Equal(expected, XpMultiplierRules.PercentFor(With(owned), which: 0, allSkillsMaxed: false));
        Assert.Equal(100, XpMultiplierRules.PercentFor(With(owned), which: 4, allSkillsMaxed: false));
    }

    [Fact]
    public void LegacyInsight_StillAddsFifty_OnSkillsAndMastery()
    {
        var meta = With("xp_mult_farming_4", XpMultiplierRules.LegacyInsightId);
        Assert.Equal(250, XpMultiplierRules.PercentFor(meta, which: 0, allSkillsMaxed: false));
        Assert.Equal(150, XpMultiplierRules.PercentFor(meta, which: 1, allSkillsMaxed: false));
        Assert.Equal(150, XpMultiplierRules.PercentFor(meta, which: 0, allSkillsMaxed: true));
    }

    /// <summary>Hand-over loop (controller ruling, 2026-10-26): a player holding both the old
    /// upgrades and Exp Boost gets the better of the two per skill, never the sum.</summary>
    [Theory]
    [InlineData(new[] { "xp_mult_farming_4", "exp_boost_1" }, 0, 200)]   // old +100 beats +25
    [InlineData(new[] { "xp_mult_farming_4", "exp_boost_1" }, 1, 125)]   // fishing: only Exp Boost
    [InlineData(new[] { "xp_mult_farming_2", "exp_boost_1", "exp_boost_2", "exp_boost_3" }, 0, 175)] // +75 beats +50
    [InlineData(new[] { "xp_mult_farming_4", "xp_mult_all", "exp_boost_5" }, 0, 250)] // tie at +150
    [InlineData(new[] { "xp_mult_farming_4", "xp_mult_all", "exp_boost_4" }, 0, 250)] // old +150 beats +100
    [InlineData(new[] { "xp_mult_farming_1", "exp_boost_5" }, 0, 250)]   // new +150 beats +25
    public void Handover_BetterOfOldAndExpBoost_NeverBoth(string[] owned, int which, int expected)
        => Assert.Equal(expected, XpMultiplierRules.PercentFor(With(owned), which, allSkillsMaxed: false));

    [Theory]
    [InlineData(new[] { "xp_mult_all", "mastery_boost_1" }, 150)]  // tie: +50 either way
    [InlineData(new[] { "xp_mult_all", "mastery_boost_2" }, 200)]  // +100 beats Insight's +50
    [InlineData(new[] { "xp_mult_all" }, 150)]
    public void Handover_BetterOfInsightAndMasteryBoost_NeverBoth(string[] owned, int expected)
        => Assert.Equal(expected, XpMultiplierRules.PercentFor(With(owned), which: 0, allSkillsMaxed: true));

    /// <summary>Rounding is to nearest, not truncation. Most XP arrives in small nibbles, and
    /// flooring every one of them would erase the whole first tier: a 3 XP gain at +25% is 3.75,
    /// which has to come back 4.</summary>
    [Theory]
    [InlineData(3, 125, 4)]
    [InlineData(4, 125, 5)]
    [InlineData(100, 125, 125)]
    [InlineData(100, 250, 250)]
    [InlineData(1, 125, 1)]     // 1.25 rounds down to 1
    [InlineData(2, 125, 3)]     // 2.5 rounds up
    [InlineData(50, 100, 50)]   // no upgrades: untouched
    [InlineData(0, 250, 0)]
    [InlineData(-5, 250, -5)]   // vanilla never does this, but never invent XP either
    public void Apply_RoundsToNearest(int amount, int percent, int expected)
        => Assert.Equal(expected, XpMultiplierRules.Apply(amount, percent));

    [Theory]
    [InlineData(0, "farming")]
    [InlineData(1, "fishing")]
    [InlineData(2, "foraging")]
    [InlineData(3, "mining")]
    [InlineData(4, "combat")]
    [InlineData(5, null)]
    public void SlugForVanillaSkill_MapsVanillaIndices(int which, string expected)
    {
        Assert.Equal(expected, XpMultiplierRules.SlugForVanillaSkill(which));
    }
}
