using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>One Data/FruitTrees entry: the sapling (the entry's key), the fruit it grows and the
/// seasons it fruits in (empty = every season, the game's default).</summary>
public sealed record RawFruitTree(string SaplingItemId, IReadOnlyList<string> FruitItemIds, IReadOnlyList<Season> Seasons);

/// <summary>Fruit from any fruit tree, read from Data/FruitTrees (mod-support work, 2026-10-08).
/// Vanilla's six orchard fruits keep their ruled rows (<see cref="AvailabilityWeeks.FruitTreeFruitWeeks"/>);
/// every other tree (Stardew Valley Expanded's Nectarine, Cornucopia's Grapefruit, Fig, Pistachio...)
/// had no rule and read as unknown.
///
/// The same arithmetic as the ruled rows: the sapling is bought the first week a walkable shop
/// sells it (<see cref="ShopWeeks"/>), the tree matures 28 days later, and it fruits from the first
/// week after that in one of its seasons. A sapling no walkable shop sells (Cornucopia's Durian,
/// only at the island trader) gives no week, and a tree whose first in-season fruit falls past the
/// year (a Spring tree: next Spring) is not placed either: the Traveling Cart route that puts
/// Apricot and Cherry at week 13 sells only vanilla fruit.</summary>
public static class FruitTreeAvailability
{
    private const int FruitEffort = 5;
    private const int MaturityDays = 28;
    private static readonly int MaturityWeeks = MaturityDays / Calendar.DaysPerWeek;

    public static ItemEffort? Derive(
        string qualifiedId, IReadOnlyList<RawFruitTree> trees, ShopWeeks? shopWeeks, WeekMode mode = WeekMode.Pacing)
    {
        if (qualifiedId == null || trees == null || shopWeeks == null) return null;
        if (AvailabilityWeeks.FruitTreeFruitWeeks.ContainsKey(qualifiedId)) return null;

        ItemEffort? best = null;
        foreach (RawFruitTree tree in trees)
        {
            if (tree?.FruitItemIds == null || !tree.FruitItemIds.Any(f => BundleParsing.NormalizeItemId(f) == qualifiedId)) continue;
            string sapling = BundleParsing.NormalizeItemId(tree.SaplingItemId);
            if (!shopWeeks.TryGet(sapling, out PlaceWeek bought)) continue;
            int? week = FirstFruitWeek(bought.Week, tree.Seasons);
            int? hard = FirstFruitWeek(bought.HardFor(mode), tree.Seasons);
            if (week == null || hard == null) continue;
            if (best != null && week >= best.EarliestWeek) continue;
            best = new ItemEffort(FruitEffort,
                $"tree fruit, sapling {sapling} sold from week {bought.Week}, {MaturityDays} days to mature, "
                + $"fruits {SeasonList(tree.Seasons)}, week {week}, effort {FruitEffort}"
                + (hard < week ? $", hard week {hard}" : ""),
                week, AvailabilityWeeks.SeasonOf(week.Value), HardWeek: hard);
        }
        return best;
    }

    /// <summary>First week, from the sapling's week plus the maturity time, that falls in one of the
    /// tree's seasons; null when none does before the year ends.</summary>
    public static int? FirstFruitWeek(int saplingWeek, IReadOnlyList<Season> seasons)
    {
        for (int week = Math.Max(1, saplingWeek) + MaturityWeeks; week <= Calendar.WeeksPerYear; week++)
            if (seasons == null || seasons.Count == 0 || seasons.Contains(AvailabilityWeeks.SeasonOf(week)))
                return week;
        return null;
    }

    private static string SeasonList(IReadOnlyList<Season> seasons)
        => seasons == null || seasons.Count == 0 ? "every season" : string.Join("/", seasons);
}
