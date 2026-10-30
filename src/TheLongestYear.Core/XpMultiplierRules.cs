namespace TheLongestYear.Core;

/// <summary>
/// Factor math for the XP upgrades.
///
/// EXP BOOST (Jeff, 2026-10-26, after tanky24u's feedback): one chain, exp_boost_1..5, and every
/// tier applies to all five skills at once: +25 / +50 / +75 / +100 / +150%. Highest owned tier
/// wins; the tiers do not add up. It never touches Mastery.
///
/// MASTERY BOOST (same day): mastery_boost_1..2, +50 / +100% on Mastery XP only, highest tier
/// wins, independent of Exp Boost. Mastery accrues inside Farmer.gainExperience only once every
/// skill is 10 (Farmer.Level &gt;= 25), so <paramref name="allSkillsMaxed"/> picks which chain
/// applies to a gain.
///
/// RETIRED (still honoured until the hand-over, <see cref="XpUpgradeHandover"/>): the per-skill
/// chains xp_mult_&lt;slug&gt;_1..4 (+25% per tier on that skill) and the Junimo Insight capstone
/// xp_mult_all (+50% on every skill and on Mastery). A save holding them keeps them working until
/// the end of the loop. If such a player also owns Exp Boost or Mastery Boost, each gain takes
/// the better of the old and the new bonus, never both added (controller ruling, 2026-10-26).
///
/// Pure Core: no Stardew dependencies, deterministic, unit-tested.
/// </summary>
public static class XpMultiplierRules
{
    public const string ExpBoostPrefix = "exp_boost_";
    public const string MasteryBoostPrefix = "mastery_boost_";

    /// <summary>Exp Boost percent by tier, indexed [1..5].</summary>
    private static readonly int[] ExpBoostPercents = { 0, 25, 50, 75, 100, 150 };

    /// <summary>Mastery Boost percent by tier, indexed [1..2].</summary>
    private static readonly int[] MasteryBoostPercents = { 0, 50, 100 };

    public static int ExpBoostTiers => ExpBoostPercents.Length - 1;
    public static int MasteryBoostTiers => MasteryBoostPercents.Length - 1;

    public static string ExpBoostId(int tier) => ExpBoostPrefix + tier;
    public static string MasteryBoostId(int tier) => MasteryBoostPrefix + tier;

    public static int ExpBoostPercent(int tier) => ExpBoostPercents[tier];
    public static int MasteryBoostPercent(int tier) => MasteryBoostPercents[tier];

    /// <summary>The retired Junimo Insight capstone.</summary>
    public const string LegacyInsightId = "xp_mult_all";

    /// <summary>Retired per-skill chain: percent added per tier owned (tier N = +25N%).</summary>
    public const int LegacyPercentPerTier = 25;

    /// <summary>Retired Junimo Insight: percent added on every skill and on Mastery.</summary>
    public const int LegacyInsightPercent = 50;

    /// <summary>Highest retired per-skill tier.</summary>
    public const int LegacyMaxTier = 4;

    public static string LegacySkillId(string slug, int tier) => $"xp_mult_{slug}_{tier}";

    /// <summary>Vanilla Farmer.gainExperience skill indices (0=farming, 1=fishing,
    /// 2=foraging, 3=mining, 4=combat; 5=luck is dead in 1.6). Null for anything else.</summary>
    public static string? SlugForVanillaSkill(int which) => which switch
    {
        0 => "farming",
        1 => "fishing",
        2 => "foraging",
        3 => "mining",
        4 => "combat",
        _ => null
    };

    /// <summary>Level at which a single skill is maxed.</summary>
    public const int MaxSkillLevel = 10;

    /// <summary>Vanilla Farmer.Level at which gainExperience routes XP into mastery.</summary>
    public const int MasteryRoutingLevel = 25;

    /// <summary>What vanilla Farmer.gainExperience adds to MasteryExp for a gain: farming at half
    /// rate, never less than one.</summary>
    public static int VanillaMasteryGain(int which, int howMuch) =>
        System.Math.Max(1, which == 0 ? howMuch / 2 : howMuch);

    /// <summary>Vanilla MasteryTrackerMenu.getMasteryExpNeededForLevel for levels 1-5.</summary>
    public static readonly int[] MasteryLevelThresholds = { 10000, 25000, 45000, 70000, 100000 };

    /// <summary>Mastery level (0-5) for a MasteryExp total, as vanilla getCurrentMasteryLevel.</summary>
    public static int MasteryLevelFor(long masteryExp)
    {
        int level = 0;
        foreach (int need in MasteryLevelThresholds)
            if (masteryExp >= need) level++;
        return level;
    }

    /// <summary>How many mastery levels a gain crossed: the level before the call against the
    /// level after the final adjustment. The announcement is decided once, from this.</summary>
    public static int MasteryLevelsGained(long expBefore, long expAfter) =>
        System.Math.Max(0, MasteryLevelFor(expAfter) - MasteryLevelFor(expBefore));

    /// <summary>The two destinations of one gain. At vanilla Level 25+ the game adds the same
    /// howMuch to MasteryExp AND to the skill's own XP, so each is scaled on its own: the skill
    /// by Exp Boost only while that skill is below 10 (unmodified level, not buffed), the mastery
    /// by Mastery Boost (or a better Junimo Insight). Mastery is 0 when vanilla is not routing.</summary>
    public static (int Skill, int Mastery) PlanGain(
        MetaState meta, int which, int howMuch, int baseSkillLevel, int vanillaLevel)
    {
        int skill = baseSkillLevel >= MaxSkillLevel
            ? howMuch
            : Apply(howMuch, PercentFor(meta, which, false));
        int mastery = vanillaLevel >= MasteryRoutingLevel
            ? Apply(VanillaMasteryGain(which, howMuch), PercentFor(meta, which, true))
            : 0;
        return (skill, mastery);
    }

    /// <summary>Percent to scale an XP gain to: 100 means unchanged, 125 means +25%. Always
    /// &gt;= 100. Percent rather than a factor because the steps are fractional and integer
    /// percent keeps the maths exact - see <see cref="Apply"/> for the rounding.</summary>
    public static int PercentFor(MetaState meta, int which, bool allSkillsMaxed)
    {
        if (meta == null) return 100;
        if (allSkillsMaxed)
            return 100 + System.Math.Max(MasteryBoostBonus(meta), LegacyMasteryBonus(meta));

        string? slug = SlugForVanillaSkill(which);
        if (slug == null) return 100;
        return 100 + System.Math.Max(ExpBoostBonus(meta), LegacySkillBonus(meta, slug));
    }

    /// <summary>Exp Boost's bonus: the highest owned tier's percent, 0 without any.</summary>
    public static int ExpBoostBonus(MetaState meta)
    {
        for (int tier = ExpBoostTiers; tier >= 1; tier--)
            if (meta.HasUpgrade(ExpBoostId(tier)))
                return ExpBoostPercents[tier];
        return 0;
    }

    /// <summary>Mastery Boost's bonus: the highest owned tier's percent, 0 without any.</summary>
    public static int MasteryBoostBonus(MetaState meta)
    {
        for (int tier = MasteryBoostTiers; tier >= 1; tier--)
            if (meta.HasUpgrade(MasteryBoostId(tier)))
                return MasteryBoostPercents[tier];
        return 0;
    }

    /// <summary>The retired upgrades' bonus on one skill: highest per-skill tier times 25, plus
    /// Junimo Insight's 50 (the old rule added them).</summary>
    public static int LegacySkillBonus(MetaState meta, string slug)
    {
        int bonus = 0;
        for (int tier = LegacyMaxTier; tier >= 1; tier--)
            if (meta.HasUpgrade(LegacySkillId(slug, tier)))
            {
                bonus = LegacyPercentPerTier * tier;
                break;
            }
        if (meta.HasUpgrade(LegacyInsightId)) bonus += LegacyInsightPercent;
        return bonus;
    }

    /// <summary>The retired upgrades' bonus on Mastery: Junimo Insight's 50 only.</summary>
    public static int LegacyMasteryBonus(MetaState meta)
        => meta.HasUpgrade(LegacyInsightId) ? LegacyInsightPercent : 0;

    /// <summary>Scale one XP gain by a percent, rounding to nearest and never below the raw
    /// amount. Rounding matters because the steps are fractional: a 3 XP nibble at +25% is 3.75,
    /// and truncating every one of those to 3 would quietly erase the whole first tier on the
    /// small, frequent gains that make up most of a day.</summary>
    public static int Apply(int amount, int percent)
    {
        if (amount <= 0 || percent <= 100) return amount;
        long scaled = ((long)amount * percent + 50) / 100;
        return (int)System.Math.Min(scaled, int.MaxValue);
    }
}
