using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

public static partial class ItemPoolBuilder
{
    /// <summary>The object categories a grown crop belongs to: vegetable, fruit, flower. A
    /// Data/Crops harvest outside them is not produce a crop bundle should ask for: Fiber Seeds
    /// grow Fiber (a resource, player report: Fiber in Quality Crops), and the wild seeds name
    /// one forage item as a placeholder harvest while the real crop is a random forage spawn.</summary>
    public static readonly IReadOnlySet<int> CropHarvestCategories = new HashSet<int> { -75, -79, -80 };

    /// <summary>Real harvests outside <see cref="CropHarvestCategories"/> that a player grows on
    /// purpose, so they stay in the crop pool: Coffee Bean (a seed-category item, and the crop's
    /// own harvest).</summary>
    public static readonly IReadOnlySet<string> CropHarvestCategoryExceptions =
        new HashSet<string>(StringComparer.Ordinal) { "(O)433" };

    /// <summary>True when a Data/Crops harvest belongs in the crop pool by its object category
    /// or the exception list.</summary>
    private static bool IsProduceHarvest(string id, RawObjectEntry obj)
        => CropHarvestCategories.Contains(obj.Category) || CropHarvestCategoryExceptions.Contains(id);

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
            if (!Vets(bare, id, objects, excluded) || !IsProduceHarvest(id, objects[bare]))
                continue;
            if (!bySeasons.TryGetValue(id, out List<Season>? seasons))
                bySeasons[id] = seasons = new List<Season>();
            foreach (Season s in crop.Seasons)
                if (!seasons.Contains(s))
                    seasons.Add(s);
        }

        // Curated additions (spec: Tea Leaves aren't a Data/Crops entry — grown from a
        // bush, not a seed): join the season's pool, mirroring SeasonalForageAdditions.
        foreach (KeyValuePair<string, List<string>> addition in tuning.CropPoolAdditions)
        {
            if (!Enum.TryParse(addition.Key, ignoreCase: true, out Season season))
                continue;
            foreach (string rawId in addition.Value)
            {
                string bare = Unqualify(rawId);
                string id = Qualify(bare);
                if (!Vets(bare, id, objects, excluded))
                    continue;
                if (!bySeasons.TryGetValue(id, out List<Season>? seasons))
                    bySeasons[id] = seasons = new List<Season>();
                if (!seasons.Contains(season))
                    seasons.Add(season);
            }
        }

        return Finish(bySeasons.Select(kv => MakeItem(
            kv.Key, objects, tuning, SortedSeasons(kv.Value), Array.Empty<string>())));
    }

    private static IReadOnlyList<PoolItem> BuildForagePool(
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning,
        IReadOnlyDictionary<string, Season>? festivalSeasons,
        IReadOnlyDictionary<string, SeasonSource>? locationSeasons)
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
            if (string.IsNullOrEmpty(spawn.ItemId) || IsSpecialOrderGated(spawn.Condition))
                continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;
            // A map that drops eggs, milk or wool on the ground (Visit Mount Vapius) does not make
            // them forage: a Foraging bundle asking for Large Brown Eggs reads as a bug (Nexus post
            // Thrippa, 2026-09-25). The Animal and Chef's bundles still reach them by kind.
            if (IsAnimalProduct(objects[bare]))
                continue;
            SeasonReading reading = ReadSpawnSeasons(spawn.Season, spawn.Condition, spawn.Location, festivalSeasons, locationSeasons);
            if (reading.Verdict == SeasonVerdict.Impossible)
                continue;   // the row never fires
            AddSeasons(id, reading.Seasons);
        }

        foreach ((Season season, string rawId) in BuiltInSeasonalForageAdditions)
        {
            string bare = Unqualify(rawId);
            string id = Qualify(bare);
            if (Vets(bare, id, objects, excluded))
                AddSeasons(id, new[] { season });
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

    /// <summary>Monster-drop pool, restricted to items whose Data/Objects Category is the
    /// monster-loot category. Monster drop tables carry bars/gems/minerals alongside true
    /// loot, so Vets() alone isn't enough — a category check keeps the pool type-pure for
    /// correct bundle classification.</summary>
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
            if (!objects.TryGetValue(bare, out RawObjectEntry? dropObj)
                || dropObj.Category != MonsterLootCategory)
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

    /// <summary>Objects pool filtered by any of several Data/Objects Categories (e.g. the
    /// two Books categories, cooking recipe books vs. skill books).</summary>
    private static IReadOnlyList<PoolItem> BuildMultiCategoryPool(
        IReadOnlyDictionary<string, RawObjectEntry> objects, IReadOnlySet<int> categories,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var items = new List<PoolItem>();
        foreach (KeyValuePair<string, RawObjectEntry> entry in objects)
        {
            if (!categories.Contains(entry.Value.Category))
                continue;
            string id = Qualify(entry.Key);
            if (!Vets(entry.Key, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    /// <summary>A category pool (like <see cref="BuildCategoryPool"/>) plus a curated list
    /// of fixed additional ids (e.g. TapperGoods: syrup category + Hardwood/Sap/Moss/seeds).</summary>
    private static IReadOnlyList<PoolItem> BuildCategoryPoolWithAdditions(
        IReadOnlyDictionary<string, RawObjectEntry> objects, int category,
        IReadOnlyList<string> additionalIds,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PoolItem>();
        foreach (KeyValuePair<string, RawObjectEntry> entry in objects)
        {
            if (entry.Value.Category != category)
                continue;
            string id = Qualify(entry.Key);
            if (!seen.Add(id) || !Vets(entry.Key, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        foreach (string rawId in additionalIds)
        {
            string bare = Unqualify(rawId);
            string id = Qualify(bare);
            if (!seen.Add(id) || !Vets(bare, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    /// <summary>Objects pool filtered by Data/Objects Type (e.g. "Arch" for Artifacts).</summary>
    private static IReadOnlyList<PoolItem> BuildTypePool(
        IReadOnlyDictionary<string, RawObjectEntry> objects, string type,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var items = new List<PoolItem>();
        foreach (KeyValuePair<string, RawObjectEntry> entry in objects)
        {
            if (!string.Equals(entry.Value.Type, type, StringComparison.OrdinalIgnoreCase))
                continue;
            string id = Qualify(entry.Key);
            if (!Vets(entry.Key, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    /// <summary>Fruit-tree sapling pool: saplings are shop items (no season data), so every
    /// vetted sapling id gets the "any season" empty list.</summary>
    private static IReadOnlyList<PoolItem> BuildSaplingPool(
        IReadOnlyList<RawFruitTreeEntry> fruitTrees,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PoolItem>();
        foreach (RawFruitTreeEntry tree in fruitTrees)
        {
            string bare = Unqualify(tree.SaplingItemId);
            string id = Qualify(bare);
            if (!seen.Add(id) || !Vets(bare, id, objects, excluded))
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }

    /// <summary>Geode-mineral pool: distinct drop-derived ids MERGED with the curated
    /// default-mineral list (the vanilla default geode table is code, not data), then
    /// filtered to drop any item whose object Category is the gem category — gems belong
    /// to the Jewel bundle, not GeodeMinerals — applied AFTER the merge so it's correct
    /// regardless of which quartz-family items are gem-category in a given data set.</summary>
    private static IReadOnlyList<PoolItem> BuildGeodeMineralPool(
        IReadOnlyList<RawGeodeDropEntry> geodeDrops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning)
    {
        var bareIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawGeodeDropEntry drop in geodeDrops)
            bareIds.Add(Unqualify(drop.ItemId));
        foreach (string defaultId in DefaultGeodeMinerals)
            bareIds.Add(Unqualify(defaultId));

        var items = new List<PoolItem>();
        foreach (string bare in bareIds)
        {
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;
            if (objects.TryGetValue(bare, out RawObjectEntry? obj) && obj.Category == GemCategory)
                continue;
            items.Add(MakeItem(id, objects, tuning, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return Finish(items);
    }
}
