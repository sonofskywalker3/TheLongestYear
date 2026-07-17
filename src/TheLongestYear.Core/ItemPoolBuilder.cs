using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Pure pool derivation: neutral Raw* records in, vetted/weighted/ordered
/// ItemPools out. Vetting (spec modded-content rules): the config-extensible
/// exclude-list, plus structural signals from the item's OWN data — Type "Quest",
/// ExcludeFromRandomSale, the fish_legendary context tag, and items with no
/// Data/Objects entry at all. Weights: numeric id = vanilla weight, non-numeric =
/// conservative modded weight, RareRollWeights override both. All output lists are
/// ordinal-ordered by ItemId — seeded sampling must be deterministic, and dictionary
/// enumeration order is not a contract.</summary>
public static class ItemPoolBuilder
{
    private const string QuestType = "Quest";
    private const string LegendaryFishTag = "fish_legendary";
    private const int MetalCategory = -15;
    private const int ArtisanCategory = -26;

    public static ItemPools Build(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns,
        IReadOnlySet<string> trapFishIds,
        IReadOnlyList<RawMonsterDropEntry> monsterDrops,
        BundleGenerationTuning tuning)
    {
        var excluded = new HashSet<string>(tuning.ExcludedItemIds, StringComparer.Ordinal);

        var cropPool = BuildCropPool(crops, objects, excluded, tuning);
        var (fishPool, crabPotPool) = BuildFishPools(fishSpawns, trapFishIds, objects, excluded, tuning);
        var foragePool = BuildForagePool(forageSpawns, objects, excluded, tuning);
        var monsterPool = BuildMonsterPool(monsterDrops, objects, excluded, tuning);
        var metalsPool = BuildCategoryPool(objects, MetalCategory, excluded, tuning);
        var artisanPool = BuildCategoryPool(objects, ArtisanCategory, excluded, tuning);

        return new ItemPools
        {
            Crops = cropPool,
            Fish = fishPool,
            CrabPot = crabPotPool,
            Forage = foragePool,
            MonsterDrops = monsterPool,
            Metals = metalsPool,
            ArtisanGoods = artisanPool,
            DerivedSeasonPins = DerivePins(cropPool, fishPool, foragePool),
        };
    }

    /// <summary>Season list for one spawn entry: an explicit Season wins; otherwise any
    /// season names found in the Condition string (best-effort GameStateQuery token scan);
    /// otherwise empty = any season.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(Season? season, string? condition)
    {
        if (season != null)
            return new[] { season.Value };
        if (string.IsNullOrEmpty(condition))
            return Array.Empty<Season>();

        var found = new List<Season>();
        foreach (string token in condition.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse(token, ignoreCase: true, out Season s) && !found.Contains(s))
                found.Add(s);
        }
        return found;
    }

    private static IReadOnlyList<PoolItem> BuildCropPool(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var bySeasons = new Dictionary<string, List<Season>>(StringComparer.Ordinal);
        foreach (RawCropEntry crop in crops)
        {
            string bare = Unqualify(crop.HarvestItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;
            if (!bySeasons.TryGetValue(id, out List<Season>? seasons))
                bySeasons[id] = seasons = new List<Season>();
            foreach (Season s in crop.Seasons)
                if (!seasons.Contains(s))
                    seasons.Add(s);
        }
        return Finish(bySeasons.Select(kv => MakeItem(
            kv.Key, objects, tuning, SortedSeasons(kv.Value), Array.Empty<string>())));
    }

    private static (IReadOnlyList<PoolItem> fish, IReadOnlyList<PoolItem> crabPot) BuildFishPools(
        IReadOnlyList<RawSpawnEntry> fishSpawns, IReadOnlySet<string> trapFishIds,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var seasonsById = new Dictionary<string, List<Season>>(StringComparer.Ordinal);
        var anySeasonById = new HashSet<string>(StringComparer.Ordinal);
        var locationsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId))
                continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;

            IReadOnlyList<Season> seasons = SeasonsFromSpawn(spawn.Season, spawn.Condition);
            if (seasons.Count == 0)
                anySeasonById.Add(id); // one any-season spawn makes the item any-season
            if (!seasonsById.TryGetValue(id, out List<Season>? list))
                seasonsById[id] = list = new List<Season>();
            foreach (Season s in seasons)
                if (!list.Contains(s))
                    list.Add(s);

            if (!locationsById.TryGetValue(id, out List<string>? locs))
                locationsById[id] = locs = new List<string>();
            if (!locs.Contains(spawn.Location))
                locs.Add(spawn.Location);
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
        return (Finish(fish), Finish(crabPot));
    }

    private static IReadOnlyList<PoolItem> BuildForagePool(
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var seasonsById = new Dictionary<string, List<Season>>(StringComparer.Ordinal);
        var anySeasonById = new HashSet<string>(StringComparer.Ordinal);

        void AddSeasons(string id, IReadOnlyList<Season> seasons)
        {
            if (seasons.Count == 0)
            {
                anySeasonById.Add(id);
                if (!seasonsById.ContainsKey(id))
                    seasonsById[id] = new List<Season>();
                return;
            }
            if (!seasonsById.TryGetValue(id, out List<Season>? list))
                seasonsById[id] = list = new List<Season>();
            foreach (Season s in seasons)
                if (!list.Contains(s))
                    list.Add(s);
        }

        foreach (RawSpawnEntry spawn in forageSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId))
                continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;
            AddSeasons(id, SeasonsFromSpawn(spawn.Season, spawn.Condition));
        }

        // Curated harder additions (spec seasonal-forage ruling): join the season's pool.
        foreach (KeyValuePair<string, List<string>> addition in tuning.SeasonalForageAdditions)
        {
            if (!Enum.TryParse(addition.Key, ignoreCase: true, out Season season))
                continue;
            foreach (string rawId in addition.Value)
            {
                string bare = Unqualify(rawId);
                string id = Qualify(bare);
                if (!Vets(bare, id, objects, excluded))
                    continue;
                AddSeasons(id, new[] { season });
            }
        }

        return Finish(seasonsById.Keys.Select(id => MakeItem(
            id, objects, tuning,
            anySeasonById.Contains(id) ? Array.Empty<Season>() : SortedSeasons(seasonsById[id]),
            Array.Empty<string>())));
    }

    private static IReadOnlyList<PoolItem> BuildMonsterPool(
        IReadOnlyList<RawMonsterDropEntry> drops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PoolItem>();
        foreach (RawMonsterDropEntry drop in drops)
        {
            string bare = Unqualify(drop.ItemId);
            string id = Qualify(bare);
            if (!seen.Add(id) || !Vets(bare, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    private static IReadOnlyList<PoolItem> BuildCategoryPool(
        IReadOnlyDictionary<string, RawObjectEntry> objects, int category,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var items = new List<PoolItem>();
        foreach (KeyValuePair<string, RawObjectEntry> entry in objects)
        {
            if (entry.Value.Category != category)
                continue;
            string id = Qualify(entry.Key);
            if (!Vets(entry.Key, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    /// <summary>Structural + configured vetting. False = never offer this item.</summary>
    private static bool Vets(
        string bareId, string qualifiedId,
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded)
    {
        if (excluded.Contains(qualifiedId))
            return false;
        if (!objects.TryGetValue(bareId, out RawObjectEntry? obj))
            return false; // unknown to Data/Objects — can't price/vet it, drop it
        if (string.Equals(obj.Type, QuestType, StringComparison.OrdinalIgnoreCase))
            return false;
        if (obj.ExcludeFromRandomSale)
            return false;
        if (obj.ContextTags != null && obj.ContextTags.Contains(LegendaryFishTag))
            return false;
        return true;
    }

    private static PoolItem MakeItem(
        string qualifiedId, IReadOnlyDictionary<string, RawObjectEntry> objects,
        BundleGenerationTuning tuning, IReadOnlyList<Season> seasons, IReadOnlyList<string> locations)
    {
        string bare = Unqualify(qualifiedId);
        int price = objects.TryGetValue(bare, out RawObjectEntry? obj) ? obj.Price : 0;
        int weight = tuning.RareRollWeights.TryGetValue(qualifiedId, out int over) ? over
            : bare.All(char.IsDigit) ? tuning.VanillaItemWeight
            : tuning.ModdedItemWeight;
        return new PoolItem(qualifiedId, price, Math.Max(1, weight), seasons, locations);
    }

    private static IReadOnlyDictionary<string, Season> DerivePins(
        params IReadOnlyList<PoolItem>[] pools)
    {
        var pins = new Dictionary<string, Season>(StringComparer.Ordinal);
        foreach (IReadOnlyList<PoolItem> pool in pools)
        {
            foreach (PoolItem item in pool)
            {
                if (item.Seasons.Count == 0)
                {
                    pins.Remove(item.ItemId); // obtainable any season somewhere — never pin
                    continue;
                }
                Season earliest = item.Seasons.Min();
                if (earliest == Season.Spring)
                {
                    pins.Remove(item.ItemId);
                    continue;
                }
                if (!pins.TryGetValue(item.ItemId, out Season existing) || earliest < existing)
                    pins[item.ItemId] = earliest;
            }
        }
        return pins;
    }

    private static IReadOnlyList<Season> SortedSeasons(List<Season> seasons)
    {
        seasons.Sort();
        return seasons.Count >= 4 ? Array.Empty<Season>() : seasons.ToArray();
    }

    private static IReadOnlyList<PoolItem> Finish(IEnumerable<PoolItem> items)
        => items.OrderBy(p => p.ItemId, StringComparer.Ordinal).ToList();

    private static string Qualify(string bareId) => BundleParsing.NormalizeItemId(bareId);

    private static string Unqualify(string id)
        => id.StartsWith("(O)", StringComparison.Ordinal) ? id.Substring(3) : id;
}
