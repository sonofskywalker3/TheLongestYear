using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Wild trees that exist only once a player plants their seed, and the two data quirks that
/// decide which tree a seed plants and which seed drops a tree can give (Cornucopia More Crops 1.7.0,
/// 2026-11-25). Decompile: PC 1.6, StardewValley.TerrainFeatures/Tree.cs and StardewValley/Object.cs.</summary>
public static class PlantedTrees
{
    /// <summary>Tree.treeStage: a seed (stage 0) is a grown tree after five growth nights.</summary>
    public const int GrowthStagesToTree = 5;

    /// <summary>Tree types the game makes without a seed, so a tree of that type with none standing at
    /// load still comes: the Mushroom Tree ("7", a farm tree turning in Fall, Farm.cs) and the three
    /// green rain trees ("10" to "12", made by green rain in Summer). Their drops keep the routes they
    /// had; how soon such a tree comes is not modelled here.</summary>
    private static readonly IReadOnlySet<string> MadeWithoutASeed = new HashSet<string>(StringComparer.Ordinal) { "7", "10", "11", "12" };

    /// <summary>A tree a player has only by planting its seed: none stands at load, the entry has a seed
    /// the game plants (Tree._LoadWildTreeData skips a tree that is not SeedPlantable or names no seed),
    /// and the game makes none of that type in any other way.</summary>
    public static bool IsPlantedOnly(WildTreeRow tree)
        => tree != null && !tree.Standing && tree.SeedPlantable && !string.IsNullOrWhiteSpace(tree.SeedItemId)
           && !MadeWithoutASeed.Contains(tree.TreeId);

    /// <summary>Whether a standing tree is worth recording for <paramref name="treeId"/>: only for a tree
    /// <see cref="IsPlantedOnly"/> could judge. The trees made without a seed come and go during the year
    /// (green rain leaves its trees standing from Summer, a farm tree turns into a Mushroom Tree in Fall), and
    /// recording them would change the answers' row hashes on every such save for no change in an answer.</summary>
    public static bool StandingCounts(string treeId) => !MadeWithoutASeed.Contains(treeId);

    /// <summary>Growth nights from seed to grown tree: each night the tree gains a stage at its
    /// GrowthChance (Tree.cs dayUpdate 905-911), so the expected count is five over the chance, rounded
    /// up (25 at the default 0.2, 10 at 0.5). Expected, not certain: a ruling, as a crop's own days are.
    /// Null when the tree never grows (a chance of 0 or less).</summary>
    public static int? GrowthNights(double growthChance)
        => growthChance <= 0 ? null : (int)Math.Ceiling(GrowthStagesToTree / Math.Min(1.0, growthChance) - 1e-9);

    /// <summary>For each planting day, the day the tree is grown: a stage on each following morning
    /// that is not in Winter (Tree.IsInSeason, 782-789: no growth outdoors in Winter unless the tree
    /// GrowsInWinter), <paramref name="nights"/> of them. Never when the year ends first.</summary>
    public static DayTable GrowTable(int nights, bool growsInWinter)
    {
        WeekMask winter = WeekMask.ForSeason(Season.Winter);
        return DayTable.Exact(start =>
        {
            int grown = 0;
            for (int morning = start + 1; morning <= Calendar.WeeksPerYear * Calendar.DaysPerWeek; morning++)
            {
                if (growsInWinter || !winter.Contains(WeekMask.WeekOfDay(morning))) grown++;
                if (grown >= nights) return morning;
            }
            return null;
        });
    }

    /// <summary>The local ids of every seed that plants a wild tree, as the game's own lookup holds them
    /// (Tree._LoadWildTreeData, 252-277: every SeedPlantable entry's seed, qualified and local; only
    /// objects plant, Tree.ResolveTreeTypeFromSeed 286).</summary>
    public static IReadOnlySet<string> WildSeedLocalIds(IEnumerable<WildTreeRow> trees)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (WildTreeRow tree in trees ?? Enumerable.Empty<WildTreeRow>())
        {
            if (tree == null || !tree.SeedPlantable || string.IsNullOrWhiteSpace(tree.SeedItemId)) continue;
            string? local = ObjectLocalId(tree.SeedItemId);
            if (local != null) ids.Add(local);
        }
        return ids;
    }

    /// <summary>Whether a Data/FruitTrees sapling really plants a wild tree: Object.placementAction takes
    /// its IsWildTreeSapling branch (6023) before the fruit tree one (6673), so a seed keyed in both
    /// tables never grows the fruit tree, and that fruit tree's row gives nothing.</summary>
    public static bool PlantsAWildTree(string fruitTreeKey, IReadOnlySet<string> wildSeedLocalIds)
        => ObjectLocalId(fruitTreeKey) is string local && wildSeedLocalIds.Contains(local);

    private const string ObjectPrefix = "(O)";

    private static string? ObjectLocalId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        string trimmed = id!.Trim();
        if (trimmed.StartsWith(ObjectPrefix, StringComparison.Ordinal)) return trimmed.Substring(ObjectPrefix.Length);
        return trimmed.StartsWith("(", StringComparison.Ordinal) ? null : trimmed;   // another item type plants nothing
    }

    /// <summary>One SeedDropItems entry as the game tries it: its own chance, season and condition,
    /// whether its item is one plain qualified id (no query, no random list, no per-item condition), and
    /// ContinueOnDrop.</summary>
    public sealed record SeedDrop(double Chance, Season? Season, string? Condition, bool PlainItem, bool ContinueOnDrop);

    /// <summary>For each SeedDropItems entry in order, whether the game can ever drop it. Tree.shake
    /// (664-686) tries the entries in order and stops at the first that drops unless it has
    /// ContinueOnDrop; TryGetDrop (612-633) rolls the chance, then the season, then the condition. So an
    /// entry is never reached when an earlier one always drops wherever this one could: chance 1, a plain
    /// item, no ContinueOnDrop, a season that is none or the same, and a condition that is none or the
    /// same text with no random roll in it (a RANDOM query can fail once and pass the next time).
    /// Overlapping conditions that are not the same text are not reasoned about.</summary>
    public static IReadOnlyList<bool> ReachableSeedDrops(IReadOnlyList<SeedDrop> drops)
    {
        var reachable = new bool[drops.Count];
        for (int i = 0; i < drops.Count; i++)
        {
            reachable[i] = true;
            for (int j = 0; j < i && reachable[i]; j++)
                if (AlwaysDropsWhere(drops[j], drops[i])) reachable[i] = false;
        }
        return reachable;
    }

    private static bool AlwaysDropsWhere(SeedDrop earlier, SeedDrop later)
    {
        if (earlier.Chance < 1.0 || !earlier.PlainItem || earlier.ContinueOnDrop) return false;
        if (earlier.Season != null && earlier.Season != later.Season) return false;
        string earlierCondition = (earlier.Condition ?? "").Trim();
        if (earlierCondition.Length == 0) return true;
        if (earlierCondition.IndexOf("RANDOM", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return string.Equals(earlierCondition, (later.Condition ?? "").Trim(), StringComparison.Ordinal);
    }
}
