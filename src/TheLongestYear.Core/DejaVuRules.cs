using System;

namespace TheLongestYear.Core;

/// <summary>Eligibility, tier and roll for the deja-vu villager lines (spec 2026-08-27). Pure; the
/// Harmony postfix supplies the live day count and RNG. Everything here is deliberately stingy:
/// the lines are meant to be uncanny, not a feature the player farms.</summary>
public static class DejaVuRules
{
    public const int WeeklyCapDays = 7;
    public const int TierMultiplier = 3;

    /// <summary>0 below the threshold, 1 from the threshold, 2 from TierMultiplier x threshold.</summary>
    public static int Tier(int familiarity, int threshold)
    {
        if (threshold <= 0) threshold = 1;
        if (familiarity >= threshold * TierMultiplier) return 2;
        return familiarity >= threshold ? 1 : 0;
    }

    public static bool IsEligible(MetaState meta, RunState run, string npc, int daysPlayed, int threshold)
    {
        if (meta.CompletedResets < 1) return false;                         // loop 1: nothing to remember
        meta.VillagerFamiliarity.TryGetValue(npc, out int fam);
        if (Tier(fam, threshold) == 0) return false;
        if (run.DejaVuShownTo.Contains(npc)) return false;                  // one per villager per loop
        if (run.DejaVuLastDay >= 0 && daysPlayed - run.DejaVuLastDay < WeeklyCapDays) return false;
        return true;
    }

    /// <summary>The tier to play now (0 = nothing). <paramref name="rollPercent"/> returns a value in
    /// [0,100) given 100; a hit is roll &lt; chance. <paramref name="force"/> (debug) skips the
    /// chance and the caps, never the config switch, and plays at least tier 1.</summary>
    public static int TryPick(MetaState meta, RunState run, string npc, int daysPlayed,
        GameplayConfig config, Func<int, int> rollPercent, bool force)
    {
        if (!config.EnableDejaVuDialogue) return 0;
        meta.VillagerFamiliarity.TryGetValue(npc, out int fam);
        int tier;
        if (force)
            tier = Math.Max(1, Tier(fam, config.DejaVuThreshold));
        else
        {
            if (!IsEligible(meta, run, npc, daysPlayed, config.DejaVuThreshold)) return 0;
            if (rollPercent(100) >= config.DejaVuChancePercent) return 0;
            tier = Tier(fam, config.DejaVuThreshold);
        }
        if (!run.DejaVuShownTo.Contains(npc)) run.DejaVuShownTo.Add(npc);
        run.DejaVuLastDay = daysPlayed;
        return tier;
    }
}
