using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Crops, greenhouse crops, Mixed Seeds and fruit trees, day by day (see the week-in-isolation
/// rule in the plan's Task 6).</summary>
public static class GrowSources
{
    private const string MixedSeedsId = "(O)770";
    private const string GreenhouseUnlock = "mail:ccPantry";   // Farm.cs 1132, GreenhouseBuilding.cs 47
    private const int FruitTreeMaturityDays = 28;              // FruitTree.cs 66
    private const int MinGrowthDays = 1;

    /// <summary>What Mixed Seeds become, by the season of the planting day (Crop.cs 294-320, 414-433).
    /// 473 resolves to 472. Winter picks a random other season's pool; only the greenhouse grows it,
    /// because the greenhouse waives the season check but still reports the outside season (GameLocation.cs 649-651).</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> MixedSeedPools = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    public static WeekMask Harvest(WeekMask seedWeeks, IReadOnlyList<Season> seasons, int growthDays, int regrowDays)
    {
        WeekMask inSeason = seasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(seasons);
        return Grow(seedWeeks, growthDays, regrowDays, day => inSeason.Contains(WeekMask.WeekOfDay(day)));
    }

    public static WeekMask Greenhouse(WeekMask seedWeeks, int growthDays, int regrowDays)
        => Grow(seedWeeks, growthDays, regrowDays, _ => true);

    public static IEnumerable<(string ItemId, ObtainSource Source)> Crops(IEnumerable<CropRow> rows, ObtainabilityModel snapshot)
    {
        List<CropRow> all = rows.ToList();
        var bySeed = all.GroupBy(r => r.SeedId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (CropRow crop in all.Where(r => r.SeedId != MixedSeedsId))
        {
            WeekMask dep = snapshot.Weeks(crop.SeedId, ObtainFilter.DependableOnly);
            WeekMask any = snapshot.Weeks(crop.SeedId, ObtainFilter.Any);
            var outdoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop,
                Harvest(dep, crop.Seasons, crop.GrowthDays, crop.RegrowDays),
                Harvest(any, crop.Seasons, crop.GrowthDays, crop.RegrowDays), outdoor, $"grown from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
            var indoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId, GreenhouseUnlock } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop,
                Greenhouse(dep, crop.GrowthDays, crop.RegrowDays), Greenhouse(any, crop.GrowthDays, crop.RegrowDays),
                indoor, $"greenhouse, from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
        }

        WeekMask mixed = snapshot.Weeks(MixedSeedsId, ObtainFilter.Any);
        if (mixed.IsEmpty) yield break;
        WeekMask winter = mixed & WeekMask.ForSeason(Season.Winter);
        foreach ((Season season, string[] seeds) in MixedSeedPools)
            foreach (string seed in seeds)
            {
                if (!bySeed.TryGetValue(seed, out CropRow? crop)) continue;
                WeekMask planted = mixed & WeekMask.ForSeason(season);
                WeekMask outdoorWeeks = Harvest(planted, crop.Seasons, crop.GrowthDays, crop.RegrowDays);
                if (!outdoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.Crop, outdoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId } }, $"Mixed Seeds in {season}"));
                WeekMask indoorWeeks = Greenhouse(planted | winter, crop.GrowthDays, crop.RegrowDays);
                if (!indoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.GreenhouseCrop, indoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId, GreenhouseUnlock } },
                        $"Mixed Seeds in the greenhouse ({season} pool)"));
            }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(
        IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (FruitTreeRow tree in rows)
        {
            WeekMask depMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.DependableOnly));
            WeekMask anyMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.Any));
            foreach (FruitRow fruit in tree.Fruit)
            {
                ConditionReading reading = ConditionSeasons.Read(fruit.Condition, festivals);
                WeekMask season = (fruit.Season is Season s ? WeekMask.ForSeason(s)
                    : tree.TreeSeasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(tree.TreeSeasons)) & reading.Weeks;
                bool luck = fruit.Chance < 1.0 || reading.Chance || fruit.IsRandom;
                ObtainConditions outdoor = ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.FruitTree, luck ? WeekMask.None : depMature & season,
                    anyMature & season, outdoor, $"fruit tree from {tree.SaplingId}"))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
                ObtainConditions indoor = ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId, GreenhouseUnlock } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.GreenhouseCrop, luck ? WeekMask.None : depMature & reading.Weeks,
                    anyMature & reading.Weeks, indoor, $"fruit tree in the greenhouse from {tree.SaplingId}"))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
            }
        }
    }

    /// <summary>Every week a tree planted from a sapling obtainable that day is mature.</summary>
    private static WeekMask Mature(WeekMask saplingWeeks)
    {
        WeekMask result = WeekMask.None;
        for (int plantDay = 1; plantDay <= Calendar.DaysPerYear; plantDay++)
        {
            if (!saplingWeeks.Contains(WeekMask.WeekOfDay(plantDay))) continue;
            int matureDay = plantDay + FruitTreeMaturityDays;
            if (matureDay > Calendar.DaysPerYear) break;
            return WeekMask.FromWeekOnwardOf(WeekMask.WeekOfDay(matureDay));
        }
        return result;
    }

    /// <summary>The day-by-day core: plant on any day whose week has seed and which <paramref name="canGrow"/>,
    /// harvest after growth if every day in between can grow, then every regrow interval while it still can.</summary>
    private static WeekMask Grow(WeekMask seedWeeks, int growthDays, int regrowDays, Func<int, bool> canGrow)
    {
        int growth = Math.Max(MinGrowthDays, growthDays);
        WeekMask result = WeekMask.None;
        for (int plantDay = 1; plantDay <= Calendar.DaysPerYear; plantDay++)
        {
            if (!seedWeeks.Contains(WeekMask.WeekOfDay(plantDay)) || !canGrow(plantDay)) continue;
            int harvestDay = plantDay + growth;
            if (harvestDay > Calendar.DaysPerYear || !GrowsThrough(plantDay, harvestDay, canGrow)) continue;
            result |= WeekMask.Of(WeekMask.WeekOfDay(harvestDay));
            if (regrowDays <= 0) continue;
            for (int next = harvestDay + regrowDays; next <= Calendar.DaysPerYear && GrowsThrough(harvestDay, next, canGrow); next += regrowDays)
                result |= WeekMask.Of(WeekMask.WeekOfDay(next));
        }
        return result;
    }

    private static bool GrowsThrough(int fromDay, int toDay, Func<int, bool> canGrow)
    {
        for (int day = fromDay; day <= toDay; day++)
            if (!canGrow(day)) return false;
        return true;
    }
}
