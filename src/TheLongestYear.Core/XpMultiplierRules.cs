namespace TheLongestYear.Core;

/// <summary>
/// Factor math for the xp_mult upgrade family (spec 2026-07-14 economy Change 3, rebalanced
/// 2026-08-30). Per-skill chains xp_mult_&lt;slug&gt;_1..4 add +25% per tier to that skill's XP,
/// so the full chain is +100% (double). The xp_mult_all capstone adds a further +50% and is the
/// ONLY tier that touches Mastery XP. Mastery accrues inside Farmer.gainExperience only once
/// every skill is 10 (Farmer.Level &gt;= 25) - at that point per-skill scaling is moot (levels
/// capped), so the mastery phase uses the capstone's +50% alone.
///
/// WHY QUARTER STEPS, not the old x2..x5 (Nexus, gazumbrado, 29 Aug 2026): "the double XP for
/// skills might be too cheap. I bought all of them for 100 JP each and by day 4 of a new save I
/// had level 5 in Fishing, Forage and Mining". He was right. Level 5 is 2,150 XP in vanilla, so
/// the old 100 JP first rung halved every climb on its own, and 500 JP bought that across the
/// board. Doubling is now where the chain ENDS (1,000 JP for the last tier, 2,150 JP for one
/// skill doubled) instead of where it starts. Jeff, 2026-08-30.
///
/// The percentages ADD rather than compound: a full chain plus the capstone is +150%, i.e. x2.5,
/// not x3. Deliberate, so the ceiling stays somewhere a player can reason about.
///
/// Pure Core: no Stardew dependencies, deterministic, unit-tested.
/// </summary>
public static class XpMultiplierRules
{
    public const string CapstoneId = "xp_mult_all";

    /// <summary>Percent added per per-skill tier owned (tier N = +25N%).</summary>
    public const int PercentPerTier = 25;

    /// <summary>Percent the capstone adds on top of the per-skill total.</summary>
    public const int CapstonePercent = 50;

    /// <summary>Highest per-skill tier, so a full chain is +100% (double).</summary>
    public const int MaxTier = 4;

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

    /// <summary>Percent to scale an XP gain to: 100 means unchanged, 125 means +25%. Always
    /// &gt;= 100. Percent rather than a factor because the steps are now fractional and integer
    /// percent keeps the maths exact - see <see cref="Apply"/> for the rounding.</summary>
    public static int PercentFor(MetaState meta, int which, bool allSkillsMaxed)
    {
        if (meta == null) return 100;
        bool capstone = meta.HasUpgrade(CapstoneId);
        if (allSkillsMaxed)
            return capstone ? 100 + CapstonePercent : 100;

        string? slug = SlugForVanillaSkill(which);
        if (slug == null) return 100;

        int percent = 100;
        for (int tier = MaxTier; tier >= 1; tier--)
            if (meta.HasUpgrade($"xp_mult_{slug}_{tier}"))
            {
                percent += PercentPerTier * tier;
                break;
            }
        if (capstone) percent += CapstonePercent;
        return percent;
    }

    /// <summary>Scale one XP gain by a percent, rounding to nearest and never below the raw
    /// amount. Rounding matters now that the steps are fractional: a 3 XP nibble at +25% is 3.75,
    /// and truncating every one of those to 3 would quietly erase the whole first tier on the
    /// small, frequent gains that make up most of a day.</summary>
    public static int Apply(int amount, int percent)
    {
        if (amount <= 0 || percent <= 100) return amount;
        long scaled = ((long)amount * percent + 50) / 100;
        return (int)System.Math.Min(scaled, int.MaxValue);
    }
}
