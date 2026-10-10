using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

public static partial class ItemPoolBuilder
{
    /// <summary>Fish and crab-pot pools, restricted to items whose Data/Objects Type is
    /// "Fish". Location fish-spawn tables carry non-fish junk/trash entries (e.g. wood,
    /// stone) alongside real fish, so Vets() alone isn't enough — a type check keeps the
    /// pool type-pure for correct bundle classification.</summary>
    private static (IReadOnlyList<PoolItem> fish, IReadOnlyList<PoolItem> crabPot, IReadOnlyList<PoolItem> unpooled) BuildFishPools(
        IReadOnlyList<RawSpawnEntry> fishSpawns, IReadOnlySet<string> trapFishIds,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning,
        IReadOnlyDictionary<string, Season>? festivalSeasons,
        LocationWeeks? locationWeeks)
    {
        var seasonsById = new Dictionary<string, List<Season>>(StringComparer.Ordinal);
        var anySeasonById = new HashSet<string>(StringComparer.Ordinal);
        var locationsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Night Market fish (Jeff, 2026-08-28: Midnight Squid, Spook Fish and Blobfish "should
        // be valid options"). Data/Objects flags them ExcludeFromRandomSale, which the vet reads
        // as "never offer", so none of the three ever reached the pool and the "one market fish"
        // rule fell back to Octopus and Sea Cucumber. A fish with a spawn row on a passive
        // festival map is a market fish: it passes the vet, and only its festival rows count,
        // because its Beach rows are gated in code (the market's dates), not in data, and would
        // otherwise read as an all-year beach catch.
        var marketFish = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId) || !BuiltInFestivalLocations.ContainsKey(spawn.Location ?? ""))
                continue;
            string bare = Unqualify(spawn.ItemId);
            if (objects.TryGetValue(bare, out RawObjectEntry? marketObj) && marketObj.ExcludeFromRandomSale
                && string.Equals(marketObj.Type, FishType, StringComparison.OrdinalIgnoreCase))
                marketFish.Add(Qualify(bare));
        }

        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId) || IsSpecialOrderGated(spawn.Condition))
                continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            bool isMarketFish = marketFish.Contains(id);
            if (isMarketFish && !BuiltInFestivalLocations.ContainsKey(spawn.Location ?? ""))
                continue;
            if (!(isMarketFish ? VetsIgnoringRandomSale(bare, id, objects, excluded) : Vets(bare, id, objects, excluded)))
                continue;
            if (!objects.TryGetValue(bare, out RawObjectEntry? spawnObj)
                || !string.Equals(spawnObj.Type, FishType, StringComparison.OrdinalIgnoreCase))
                continue;

            IReadOnlyList<Season> seasons = SeasonsFromSpawn(spawn.Season, spawn.Condition, spawn.Location, festivalSeasons);
            if (seasons.Count == 0)
                anySeasonById.Add(id); // one any-season spawn makes the item any-season
            if (!seasonsById.TryGetValue(id, out List<Season>? list))
                seasonsById[id] = list = new List<Season>();
            foreach (Season s in seasons)
                if (!list.Contains(s))
                    list.Add(s);

            if (!locationsById.TryGetValue(id, out List<string>? locs))
                locationsById[id] = locs = new List<string>();
            // Location is the Data/Locations key the spawn row was read under, never null here.
            if (!locs.Contains(spawn.Location!))
                locs.Add(spawn.Location!);
        }

        var fish = new List<PoolItem>();
        var crabPot = new List<PoolItem>();
        foreach (string id in seasonsById.Keys)
        {
            IReadOnlyList<Season> seasons = anySeasonById.Contains(id)
                ? Array.Empty<Season>()
                : SortedSeasons(seasonsById[id]);
            var locs = locationsById[id];
            locs.Sort(StringComparer.Ordinal);
            PoolItem item = MakeItem(id, objects, tuning, seasons, locs);
            if (trapFishIds.Contains(Unqualify(id)))
                crabPot.Add(item);
            else
                fish.Add(item);
        }

        // Curated additions: the three mine fish and five legendaries the game data never rows
        // into a spawn table (MineShaft.getFish hard-codes area/floor; legendaries are
        // CatchLimit-1 rod events). Only join when Data/Objects actually knows the id (a mod could
        // remove it) and it is not already present from a spawn row. Vanilla DOES give the
        // legendaries a real Data/Locations row (Vets bypasses their ExcludeFromRandomSale via
        // PoolAdditions.VetExceptions, so the main spawn loop above already adds them), so a
        // "seen" addition still needs its Weight forced to the addition's weight: otherwise it
        // rolls at the ordinary VanillaItemWeight instead of the intended 1. Its seasons/locations
        // stay whatever the data row said, which can be richer than the curated fallback.
        var seenIds = new HashSet<string>(seasonsById.Keys, StringComparer.Ordinal);
        foreach (PoolAddition addition in PoolAdditions.Fish)
        {
            if (!seenIds.Add(addition.ItemId))
            {
                int existingIndex = fish.FindIndex(p => p.ItemId == addition.ItemId);
                if (existingIndex >= 0)
                    fish[existingIndex] = fish[existingIndex] with { Weight = addition.Weight };
                continue;
            }
            if (!objects.ContainsKey(Unqualify(addition.ItemId)))
                continue;
            PoolItem item = MakeItem(addition.ItemId, objects, tuning, addition.Seasons, addition.Locations)
                with { Weight = addition.Weight };
            fish.Add(item);
        }

        return (Finish(fish), Finish(crabPot),
            BuildUnpooledFish(fishSpawns, trapFishIds, objects, excluded, tuning, festivalSeasons, locationWeeks, seenIds, marketFish));
    }

    /// <summary>Rod fish the pool vet leaves out ONLY for their ExcludeFromRandomSale flag, which
    /// keeps an item out of random shop stock and out of the bundle pools, not off the line. Stardew
    /// Valley Expanded flags every one of its fish, so none of them had an availability week: a
    /// cross-mod board asking for a Bull Trout read it as unknown, week 13 (mod-support work,
    /// 2026-10-08). They are never sampled; the availability model places them from their own spawn
    /// rows. Only rows in places <paramref name="locationWeeks"/> can date count, and the seasons and
    /// locations are gathered from those rows alone, so a row in a map no door leads to (SVE's
    /// Highlands) can neither place the fish nor widen its seasons. Null weeks = none collected
    /// (tests that build pools by hand, and a failed warp read).</summary>
    private static IReadOnlyList<PoolItem> BuildUnpooledFish(
        IReadOnlyList<RawSpawnEntry> fishSpawns, IReadOnlySet<string> trapFishIds,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning,
        IReadOnlyDictionary<string, Season>? festivalSeasons,
        LocationWeeks? locationWeeks, IReadOnlySet<string> pooledIds, IReadOnlySet<string> marketFish)
    {
        if (locationWeeks == null) return Array.Empty<PoolItem>();
        var seasonsById = new Dictionary<string, List<Season>>(StringComparer.Ordinal);
        var anySeasonById = new HashSet<string>(StringComparer.Ordinal);
        var locationsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId) || IsSpecialOrderGated(spawn.Condition)) continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (pooledIds.Contains(id) || marketFish.Contains(id) || trapFishIds.Contains(bare)) continue;
            if (Vets(bare, id, objects, excluded) || !VetsIgnoringRandomSale(bare, id, objects, excluded)) continue;
            if (!string.Equals(objects[bare].Type, FishType, StringComparison.OrdinalIgnoreCase)) continue;
            if (!locationWeeks.TryGet(spawn.Location ?? "", out _)) continue;

            IReadOnlyList<Season> seasons = SeasonsFromSpawn(spawn.Season, spawn.Condition, spawn.Location, festivalSeasons);
            if (seasons.Count == 0) anySeasonById.Add(id);
            if (!seasonsById.TryGetValue(id, out List<Season>? list))
                seasonsById[id] = list = new List<Season>();
            foreach (Season s in seasons)
                if (!list.Contains(s)) list.Add(s);
            if (!locationsById.TryGetValue(id, out List<string>? locs))
                locationsById[id] = locs = new List<string>();
            if (!locs.Contains(spawn.Location ?? "")) locs.Add(spawn.Location ?? "");
        }
        return Finish(seasonsById.Keys.Select(id =>
        {
            List<string> locs = locationsById[id];
            locs.Sort(StringComparer.Ordinal);
            return MakeItem(id, objects, tuning,
                anySeasonById.Contains(id) ? Array.Empty<Season>() : SortedSeasons(seasonsById[id]), locs);
        }));
    }
}
