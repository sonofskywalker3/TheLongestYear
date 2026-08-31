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
    }

    /// <summary>The 2026-08-30 rebalance: tier N adds 25N%, so a full chain is +100% (double)
    /// where tier 1 alone used to be. Highest owned tier wins.</summary>
    [Theory]
    [InlineData("xp_mult_farming_1", 125)]
    [InlineData("xp_mult_farming_2", 150)]
    [InlineData("xp_mult_farming_3", 175)]
    [InlineData("xp_mult_farming_4", 200)]
    public void PerSkillTier_AddsTwentyFivePercentEach(string owned, int expected)
    {
        Assert.Equal(expected, XpMultiplierRules.PercentFor(With(owned), which: 0, allSkillsMaxed: false));
    }

    [Fact]
    public void TierAppliesOnlyToItsOwnSkill()
    {
        var meta = With("xp_mult_farming_4");
        Assert.Equal(100, XpMultiplierRules.PercentFor(meta, which: 4, allSkillsMaxed: false)); // combat untouched
    }

    /// <summary>The capstone ADDS its 50% rather than compounding, so the ceiling is x2.5 and not
    /// x3. Deliberate (Jeff, 2026-08-30): a ceiling a player can reason about.</summary>
    [Fact]
    public void Capstone_AddsFiftyPercentOnTop_NotCompounded()
    {
        var meta = With("xp_mult_farming_4", XpMultiplierRules.CapstoneId);
        Assert.Equal(250, XpMultiplierRules.PercentFor(meta, which: 0, allSkillsMaxed: false));
    }

    [Fact]
    public void MasteryPhase_OnlyCapstoneApplies()
    {
        // All skills maxed = mastery is what's accruing; per-skill tiers are moot
        // (levels are capped) and must NOT leak into mastery (user ruling: only the
        // capstone touches Mastery XP).
        var withTierOnly = With("xp_mult_farming_4");
        Assert.Equal(100, XpMultiplierRules.PercentFor(withTierOnly, which: 0, allSkillsMaxed: true));

        var withCapstone = With("xp_mult_farming_4", XpMultiplierRules.CapstoneId);
        Assert.Equal(150, XpMultiplierRules.PercentFor(withCapstone, which: 0, allSkillsMaxed: true));
    }

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
