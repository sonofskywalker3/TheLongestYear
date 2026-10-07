using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Picks and gates the random shrine donation goals.</summary>
public static class ShrineGoalSampler
{
    public const int Salt = 0x6A1F;
    private const int EasyTarget = 3, NormalTarget = 4, HardTarget = 5, ExtremeTarget = 6;

    /// <summary>How many goals one theme list asks, by the Required Slots step.</summary>
    public static int TargetFor(DifficultyStep requiredSlots) => requiredSlots switch
    {
        DifficultyStep.Easy => EasyTarget,
        DifficultyStep.Normal => NormalTarget,
        DifficultyStep.Hard => HardTarget,
        DifficultyStep.Extreme => ExtremeTarget,
        _ => NormalTarget,
    };

    /// <summary>Uniform pick without replacement, at most <paramref name="count"/> ids, honouring
    /// group caps. Input is de-duplicated and sorted first so the result depends only on the seed.
    /// Returns fewer when the pool runs short. <paramref name="alreadyChosen"/> is the list's CC
    /// goals: they count toward the caps (one fruit-tree fruit, trap fish, jelly per list) but are
    /// never returned.</summary>
    public static IReadOnlyList<string> Pick(int seed, int weekOfYear, Theme theme,
        IReadOnlyList<string> candidateIds, int count, IReadOnlyList<GoalGroupCap> caps,
        IReadOnlyCollection<string>? alreadyChosen = null)
    {
        var picked = new List<string>();
        if (count <= 0) return picked;
        var pool = candidateIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Random rng = RollSeed.Rng(seed, weekOfYear, Salt, (int)theme);
        while (picked.Count < count && pool.Count > 0)
        {
            int i = rng.Next(pool.Count);
            string id = pool[i];
            pool.RemoveAt(i);
            if (IsCapped(id, picked, alreadyChosen, caps)) continue;
            picked.Add(id);
        }
        return picked;
    }

    private static bool IsCapped(string id, List<string> picked, IReadOnlyCollection<string>? alreadyChosen,
        IReadOnlyList<GoalGroupCap>? caps)
    {
        if (caps == null) return false;
        foreach (GoalGroupCap cap in caps)
        {
            if (!cap.Ids.Contains(id)) continue;
            int taken = picked.Count(p => cap.Ids.Contains(p)) + (alreadyChosen?.Count(p => cap.Ids.Contains(p)) ?? 0);
            if (taken >= cap.Max) return true;
        }
        return false;
    }

    /// <summary>Whether an item may be asked this week. Easy/Normal Item Rarity: placed in the
    /// bundles, in season now and not before its own goal week. Hard/Extreme: placed and not before
    /// its hard week (or pacing week).</summary>
    public static bool IsAllowed(DifficultyStep itemRarity, bool placed, int goalWeek,
        int hardWeekOrPacing, bool inSeasonNow, int weekOfYear) => itemRarity switch
    {
        DifficultyStep.Easy or DifficultyStep.Normal => placed && inSeasonNow && goalWeek <= weekOfYear,
        _ => placed && hardWeekOrPacing <= weekOfYear,
    };
}
