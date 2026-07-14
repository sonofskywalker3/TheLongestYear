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
    public void NoUpgrades_FactorIsOne()
    {
        Assert.Equal(1, XpMultiplierRules.FactorFor(With(), which: 0, allSkillsMaxed: false));
    }

    [Theory]
    [InlineData("xp_mult_farming_1", 2)]
    [InlineData("xp_mult_farming_4", 5)]
    public void PerSkillTier_FactorIsTierPlusOne(string owned, int expected)
    {
        // Highest owned tier wins; tier N = x(N+1).
        Assert.Equal(expected, XpMultiplierRules.FactorFor(With(owned), which: 0, allSkillsMaxed: false));
    }

    [Fact]
    public void TierAppliesOnlyToItsOwnSkill()
    {
        var meta = With("xp_mult_farming_4");
        Assert.Equal(1, XpMultiplierRules.FactorFor(meta, which: 4, allSkillsMaxed: false)); // combat untouched
    }

    [Fact]
    public void Capstone_DoublesOnTopOfSkillTier()
    {
        var meta = With("xp_mult_farming_4", XpMultiplierRules.CapstoneId);
        Assert.Equal(10, XpMultiplierRules.FactorFor(meta, which: 0, allSkillsMaxed: false));
    }

    [Fact]
    public void MasteryPhase_OnlyCapstoneApplies()
    {
        // All skills maxed = mastery is what's accruing; per-skill tiers are moot
        // (levels are capped) and must NOT leak x5 into mastery (user ruling: only the
        // capstone touches Mastery XP).
        var withTierOnly = With("xp_mult_farming_4");
        Assert.Equal(1, XpMultiplierRules.FactorFor(withTierOnly, which: 0, allSkillsMaxed: true));

        var withCapstone = With("xp_mult_farming_4", XpMultiplierRules.CapstoneId);
        Assert.Equal(2, XpMultiplierRules.FactorFor(withCapstone, which: 0, allSkillsMaxed: true));
    }

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
