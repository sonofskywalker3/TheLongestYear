using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Crops, greenhouse crops, Mixed Seeds, fruit trees and the tea bush as start-day chains:
/// seed table, then plant, then land (spec 2026-09-14-obtainability-phase2 section 1).</summary>
public static class GrowSources
{
    private const string MixedSeedsId = "(O)770";
    private const string TeaSaplingId = "(O)251";
    private const string TeaLeavesId = "(O)815";
    private const string GreenhouseUnlock = "mail:ccPantry";   // Farm.cs 1132, GreenhouseBuilding.cs 47
    /// <summary>Bush.IsSheltered (Bush.cs 196-205) accepts a greenhouse OR an indoor pot, and a pot
    /// needs no unlock, so the tea bush's sheltered source must not require the pantry bundle.</summary>
    private const string ShelteredNote = "sheltered (greenhouse or indoor pot)";
    private const int FruitTreeMaturityDays = 28;              // FruitTree.cs 66
    private const int TeaBushAgeDays = 20;                     // Bush.cs 220 (getAge() >= 20)
    private const int TeaBloomFirstDayOfMonth = 22;            // Bush.cs 220 (dayOfMonth >= 22)
    private const int MinGrowthDays = 1;
    private static readonly SetupStep SaplingStep = new("sapling", FruitTreeMaturityDays);
    private static readonly SetupStep TeaBushStep = new("tea bush", TeaBushAgeDays);

    /// <summary>What Mixed Seeds become, by the season of the planting day (Crop.cs 294-320, 414-433).
    /// 473 resolves to 472. Winter picks a random other season's pool; only the greenhouse grows it,
    /// because the greenhouse waives the season check but still reports the outside season (GameLocation.cs 649-651).</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> MixedSeedPools = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    /// <summary>Plant on day p outdoors: lands p + growth when every day through harvest is in season.</summary>
    public static DayTable PlantTable(IReadOnlyList<Season> seasons, int growthDays)
    {
        WeekMask inSeason = seasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(seasons);
        int growth = Math.Max(MinGrowthDays, growthDays);
        return DayTable.Exact(plant =>
        {
            int harvest = plant + growth;
            if (harvest > Calendar.DaysPerYear) return null;
            for (int day = plant; day <= harvest; day++)
                if (!inSeason.Contains(WeekMask.WeekOfDay(day))) return null;
            return harvest;
        });
    }

    public static DayTable GreenhouseTable(int growthDays)
    {
        int growth = Math.Max(MinGrowthDays, growthDays);
        return DayTable.Exact(plant => plant + growth <= Calendar.DaysPerYear ? plant + growth : null);
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Crops(IEnumerable<CropRow> rows, ObtainabilityModel snapshot)
    {
        List<CropRow> all = rows.ToList();
        var bySeed = all.GroupBy(r => r.SeedId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (CropRow crop in all.Where(r => r.SeedId != MixedSeedsId))
        {
            Derived.Input seed = Derived.Of(snapshot, crop.SeedId);
            DayTable dep = seed.Dependable;
            DayTable any = seed.Any;
            DayTable outdoors = PlantTable(crop.Seasons, crop.GrowthDays);
            DayTable indoors = GreenhouseTable(crop.GrowthDays);
            string regrow = crop.RegrowDays > 0 ? $", regrows every {crop.RegrowDays} days" : "";
            var outdoor = seed.Flag(ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId } });
            foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop, dep.Then(outdoors), any.Then(outdoors), outdoor, $"grown from {crop.SeedId}{regrow}"))
                yield return (crop.HarvestId, s);
            var indoor = seed.Flag(ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId, GreenhouseUnlock } });
            foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop, dep.Then(indoors), any.Then(indoors), indoor, $"greenhouse, from {crop.SeedId}{regrow}"))
                yield return (crop.HarvestId, s);
        }

        Derived.Input mixedSeeds = Derived.Of(snapshot, MixedSeedsId);
        DayTable mixed = mixedSeeds.Any;
        if (mixed.IsEmpty) yield break;
        foreach ((Season season, string[] seeds) in MixedSeedPools)
            foreach (string seed in seeds)
            {
                if (!bySeed.TryGetValue(seed, out CropRow? crop)) continue;
                // Outdoors the pool is the planting day's season; in the greenhouse that season or Winter.
                DayTable plantOutdoors = DayTable.Available(d => WeekMask.ForSeason(season).Contains(WeekMask.WeekOfDay(d)));
                DayTable plantIndoors = DayTable.Available(d => (WeekMask.ForSeason(season) | WeekMask.ForSeason(Season.Winter)).Contains(WeekMask.WeekOfDay(d)));
                DayTable outdoorLands = mixed.Then(plantOutdoors).Then(PlantTable(crop.Seasons, crop.GrowthDays));
                if (!outdoorLands.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.Crop, outdoorLands, Reliability.Chance,
                        mixedSeeds.Flag(ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId } }), $"Mixed Seeds in {season}"));
                DayTable indoorLands = mixed.Then(plantIndoors).Then(GreenhouseTable(crop.GrowthDays));
                if (!indoorLands.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.GreenhouseCrop, indoorLands, Reliability.Chance,
                        mixedSeeds.Flag(ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId, GreenhouseUnlock } }),
                        $"Mixed Seeds in the greenhouse ({season} pool)"));
            }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(
        IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var setup = new[] { SaplingStep };
        foreach (FruitTreeRow tree in rows)
        {
            Derived.Input sapling = Derived.Of(snapshot, tree.SaplingId);
            DayTable depMature = sapling.Dependable.Delay(FruitTreeMaturityDays);
            DayTable anyMature = sapling.Any.Delay(FruitTreeMaturityDays);
            foreach (FruitRow fruit in tree.Fruit)
            {
                ConditionReading reading = ConditionSeasons.Read(fruit.Condition, festivals);
                WeekMask season = fruit.Season is Season s ? WeekMask.ForSeason(s)
                    : tree.TreeSeasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(tree.TreeSeasons);
                DayTable fruitsOutdoors = ConditionSeasons.Availability(reading, season);
                DayTable fruitsIndoors = ConditionSeasons.Availability(reading, WeekMask.All);
                bool luck = fruit.Chance < 1.0 || reading.Chance || fruit.IsRandom;
                ObtainConditions outdoor = sapling.Flag(ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId } }, reading));
                foreach (ObtainSource src in SourcePair.Of(SourceKind.FruitTree, luck ? DayTable.None : depMature.Then(fruitsOutdoors),
                    anyMature.Then(fruitsOutdoors), outdoor, $"fruit tree from {tree.SaplingId}", setup))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
                ObtainConditions indoor = sapling.Flag(ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId, GreenhouseUnlock } }, reading));
                foreach (ObtainSource src in SourcePair.Of(SourceKind.GreenhouseCrop, luck ? DayTable.None : depMature.Then(fruitsIndoors),
                    anyMature.Then(fruitsIndoors), indoor, $"fruit tree in the greenhouse from {tree.SaplingId}", setup))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
            }
        }
    }

    /// <summary>Tea Leaves from a Tea Sapling (Bush.cs 209-225): the bush is age 20 or more, on days 22 to
    /// 28 of a month, not Winter unless sheltered (greenhouse or indoor pot).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> TeaBush(ObtainabilityModel snapshot)
    {
        Derived.Input sapling = Derived.Of(snapshot, TeaSaplingId);
        DayTable dep = sapling.Dependable;
        DayTable any = sapling.Any;
        if (any.IsEmpty) yield break;
        static bool Bloom(int day) => Calendar.DayOfMonthOf(day) >= TeaBloomFirstDayOfMonth;
        DayTable outdoors = DayTable.Available(d => Bloom(d) && !WeekMask.ForSeason(Season.Winter).Contains(WeekMask.WeekOfDay(d)));
        DayTable sheltered = DayTable.Available(Bloom);
        var setup = new[] { TeaBushStep };
        var outdoor = sapling.Flag(ObtainConditions.None with { Requires = new[] { "item:" + TeaSaplingId } });
        foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop, dep.Delay(TeaBushAgeDays).Then(outdoors), any.Delay(TeaBushAgeDays).Then(outdoors), outdoor, "tea bush, days 22 to 28", setup))
            yield return (TeaLeavesId, s);
        var indoor = sapling.Flag(ObtainConditions.None with { Requires = new[] { "item:" + TeaSaplingId, ShelteredNote } });
        foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop, dep.Delay(TeaBushAgeDays).Then(sheltered), any.Delay(TeaBushAgeDays).Then(sheltered), indoor, "sheltered tea bush, days 22 to 28", setup))
            yield return (TeaLeavesId, s);
    }
}
