using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>What a wild tree standing in the world yields (Data/WildTrees plus the live trees at
/// load): its seed, Wood, and its ChopItems. The week is the earliest map, dated by the walked
/// <see cref="LocationWeeks"/>, where a full-grown tree of that type stands.
///
/// Decompile (PC 1.6, StardewValley.TerrainFeatures/Tree.cs): a grown tree carries a seed on a
/// day with SeedOnShakeChance (dayUpdate, line 940) and drops it when shaken with Foraging 1 or
/// more (performUseAction, line 664); a felled tree drops 1 to 2 seeds at SeedOnChopChance with
/// Foraging 1 (line 573), Wood when DropWoodOnChop and its ChopItems. Foraging 1 comes from the
/// first trees and forage of day 1, so it adds no week. A tree type whose seed never drops (the
/// Mushroom Tree and the Mystic Tree: both chances 0) yields no seed.</summary>
public static class WildTreeAvailability
{
    /// <summary>Tree.cs: DropWoodOnChop drops Wood, (O)388.</summary>
    public const string WoodItemId = "(O)388";

    private const int WoodEffort = 1;
    private const int SeedEffort = 2;
    private const int ChopItemEffort = 1;
    private const int UncertainDropStep = 1;
    private const double CertainChance = 1.0;

    public static ItemEffort? Derive(string qualifiedId, EffortData data, LocationWeeks? weeks, WeekMode mode = WeekMode.Pacing)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrEmpty(qualifiedId)) return null;
        ItemEffort? best = null;
        foreach (RawWildTree tree in data.WildTrees)
        {
            if (tree == null) continue;
            foreach ((int effort, Season? season, string what) in Yields(tree, qualifiedId))
            {
                PlaceWeek? place = EarliestPlace(tree.TreeId, data.WildTreeSpots, weeks, out string location, out int maps);
                if (place == null) continue;
                int seasonWeek = season == null ? 1 : AvailabilityWeeks.FirstWeekOf(season.Value);
                int week = Math.Max(place.Week, seasonWeek);
                int hard = Math.Max(place.HardFor(mode), seasonWeek);
                bool better = best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort);
                if (better)
                    best = new ItemEffort(effort,
                        $"wild tree {tree.TreeId}, {what}, {location} ({maps} map(s)), week {week}, effort {effort}",
                        week, AvailabilityWeeks.SeasonOf(week), HardWeek: hard);
            }
        }
        return best;
    }

    private static IEnumerable<(int Effort, Season? Season, string What)> Yields(RawWildTree tree, string qualifiedId)
    {
        if (tree.SeedItemId != null && BundleParsing.NormalizeItemId(tree.SeedItemId) == qualifiedId
            && (tree.SeedOnShakeChance > 0 || tree.SeedOnChopChance > 0))
            yield return (SeedEffort, null, "seed by shaking or chopping (Foraging 1)");
        if (tree.DropsWood && qualifiedId == WoodItemId)
            yield return (WoodEffort, null, "wood by chopping");
        foreach (RawWildTreeDrop drop in tree.ChopItems ?? Array.Empty<RawWildTreeDrop>())
            if (drop != null && drop.Chance > 0 && BundleParsing.NormalizeItemId(drop.ItemId) == qualifiedId)
                yield return (ChopItemEffort + (drop.Chance < CertainChance ? UncertainDropStep : 0), drop.Season,
                    $"chop drop at {drop.Chance:0.###}");
    }

    private static PlaceWeek? EarliestPlace(string treeId, IReadOnlyList<RawWildTreeSpot> spots, LocationWeeks? weeks,
        out string location, out int maps)
    {
        location = "";
        PlaceWeek? best = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawWildTreeSpot spot in spots.Where(s => s != null && s.TreeId == treeId))
        {
            string map = spot.Location ?? "";
            if (!seen.Add(map)) continue;
            PlaceWeek? place = weeks != null
                ? (weeks.TryGet(map, out PlaceWeek walked) ? walked : null)
                : PlaceWeek.OwnGate(map);
            if (place == null) continue;
            if (best == null || place.Week < best.Week) { best = place; location = map; }
        }
        maps = seen.Count(m => weeks == null || weeks.TryGet(m, out _));
        return best;
    }
}
