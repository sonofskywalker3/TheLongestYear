namespace TheLongestYear.Core;

/// <summary>
/// Factor math for the xp_mult upgrade family (spec 2026-07-14 economy Change 3).
/// Per-skill chains xp_mult_&lt;slug&gt;_1..4 give x2..x5 on that skill's XP; the
/// xp_mult_all capstone doubles everything ON TOP (x10 max) and is the ONLY tier
/// that touches Mastery XP. Mastery accrues inside Farmer.gainExperience only once
/// every skill is 10 (Farmer.Level &gt;= 25) — at that point per-skill scaling is
/// moot (levels capped), so the mastery phase uses the capstone factor alone.
/// Pure Core: no Stardew dependencies, deterministic, unit-tested.
/// </summary>
public static class XpMultiplierRules
{
    public const string CapstoneId = "xp_mult_all";

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

    /// <summary>Integer factor to scale an XP gain by (always >= 1).</summary>
    public static int FactorFor(MetaState meta, int which, bool allSkillsMaxed)
    {
        if (meta == null) return 1;
        bool capstone = meta.HasUpgrade(CapstoneId);
        if (allSkillsMaxed)
            return capstone ? 2 : 1;

        string? slug = SlugForVanillaSkill(which);
        if (slug == null) return 1;

        int factor = 1;
        for (int tier = 4; tier >= 1; tier--)
            if (meta.HasUpgrade($"xp_mult_{slug}_{tier}"))
            {
                factor = tier + 1;
                break;
            }
        if (capstone) factor *= 2;
        return factor;
    }
}
