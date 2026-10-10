using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>Pure pool derivation: neutral Raw* records in, vetted/weighted/ordered
/// ItemPools out. Vetting (spec modded-content rules): the config-extensible
/// exclude-list, plus structural signals from the item's OWN data — Type "Quest",
/// ExcludeFromRandomSale, and items with no Data/Objects entry at all (a curated
/// PoolAdditions.VetExceptions id skips the ExcludeFromRandomSale check). Weights: any id
/// without a mod prefix (SMAPI mod items are
/// Author.Mod_Item) = vanilla weight, prefixed = conservative modded weight,
/// RareRollWeights override both. All output lists are
/// ordinal-ordered by ItemId — seeded sampling must be deterministic, and dictionary
/// enumeration order is not a contract.</summary>
public static partial class ItemPoolBuilder
{
    private const string QuestType = "Quest";
    private const string FishType = "Fish";
    private const string ArchType = "Arch";
    private const int MetalCategory = -15;
    private const int ArtisanCategory = -26;
    private const int MonsterLootCategory = -28;
    private const int CookingCategory = -7;
    private const int TapperCategory = -27;
    private const int GemCategory = -2;
    private static readonly HashSet<int> BookCategories = new() { -102, -103 };
    private const string ColourTagPrefix = "color_";
    private const int TrophyWeight = 3;

    /// <summary>Fixed additions to the TapperGoods pool beyond the -27 (syrup) category:
    /// Hardwood, Sap, Moss (1.6), Maple Seed, Acorn, Pine Cone — tapper-adjacent items a
    /// player collects alongside tapper output.</summary>
    private static readonly string[] TapperGoodsAdditions =
    {
        "(O)709", "(O)92", "(O)Moss", "(O)310", "(O)309", "(O)311",
    };

    /// <summary>Built-in seasonal forage additions, merged with the tuning's
    /// SeasonalForageAdditions (same config-override rationale as
    /// <see cref="BuiltInExcludedItemIds"/>: a saved config.json replaces that dictionary
    /// wholesale). Winter Root and Snow Yam are dug from tilled snow and artifact spots, so
    /// they have no Data/Locations forage row and never reached the Winter pool, which left
    /// Winter with five candidates once any-season shellfish stopped counting; vanilla's own
    /// Winter Foraging bundle asks for both.</summary>
    private static readonly (Season Season, string ItemId)[] BuiltInSeasonalForageAdditions =
    {
        (Season.Winter, "(O)412"), // Winter Root
        (Season.Winter, "(O)416"), // Snow Yam
    };

    /// <summary>Curated vanilla default-geode mineral table (code, not data, in the base
    /// game): Copper/Iron Ore, Coal, Stone, Earth Crystal, Frozen Tear, Fire Quartz,
    /// Quartz. Merged with drop-derived ids; gem-category (-2) items are filtered out
    /// after the merge regardless of source.</summary>
    private static readonly string[] DefaultGeodeMinerals =
    {
        "(O)378", "(O)380", "(O)382", "(O)390", "(O)86", "(O)84", "(O)82", "(O)80",
    };

    public static ItemPools Build(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns,
        IReadOnlySet<string> trapFishIds,
        IReadOnlyList<RawMonsterDropEntry> monsterDrops,
        IReadOnlyList<RawFruitTreeEntry> fruitTrees,
        IReadOnlyList<RawGeodeDropEntry> geodeDrops,
        BundleGenerationTuning tuning,
        IReadOnlySet<string>? extraExcludedIds = null,
        IReadOnlyDictionary<string, RawFishEntry>? fishRows = null,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null,
        SourceReachability? reachability = null,
        IReadOnlySet<string>? vanillaOnlyIds = null,
        LocationWeeks? locationWeeks = null)
    {
        var excluded = new HashSet<string>(tuning.ExcludedItemIds, StringComparer.Ordinal);
        // Save-specific exclusions (YearTwoCrops: Pierre's year-2 seeds until the upgrade is owned).
        if (extraExcludedIds != null)
            excluded.UnionWith(extraExcludedIds);

        // Provably unreachable items (spec 2026-09-10-source-reachability). Merged HERE so every
        // pool inherits it: Vets() consults `excluded`, and all thirteen pools go through Vets.
        // Applies at every difficulty, unlike YearTwoCrops: this is impossibility, not pacing.
        if (reachability != null)
        {
            foreach (string id in AllCandidateIds(crops, objects, forageSpawns, fishSpawns))
                if (reachability.IsUnreachable(id))
                    excluded.Add(id);
        }

        // TLY Custom boards ask only for vanilla items (spec 2026-10-08-custom-board-vanilla-only):
        // every other id joins `excluded`, so every pool drops it through Vets and ExcludedIds
        // carries it to the consumers that read ids from outside the pools (a bundle's own items,
        // flavored-slot inputs). Null (Normal, Remixed, the shared load-time pools) adds nothing.
        if (vanillaOnlyIds != null)
        {
            foreach (string id in AllCandidateIds(crops, objects, forageSpawns, fishSpawns))
                if (!vanillaOnlyIds.Contains(id))
                    excluded.Add(id);
        }

        var cropPool = BuildCropPool(crops, objects, excluded, tuning);
        var (fishPool, crabPotPool, unpooledFish) = BuildFishPools(fishSpawns, trapFishIds, objects, excluded, tuning, festivalSeasons, locationWeeks);
        var foragePool = BuildForagePool(forageSpawns, objects, excluded, tuning, festivalSeasons);
        var qualityEligible = BuildQualityEligibleIds(crops, objects, forageSpawns, fishSpawns, trapFishIds, excluded);
        var monsterPool = BuildMonsterPool(monsterDrops, objects, excluded, tuning);
        var metalsPool = BuildCategoryPool(objects, MetalCategory, excluded, tuning);
        var artisanPool = BuildCategoryPool(objects, ArtisanCategory, excluded, tuning);
        var artifactsPool = BuildTypePool(objects, ArchType, excluded, tuning);
        var booksPool = BuildMultiCategoryPool(objects, BookCategories, excluded, tuning)
            .Where(item => AvailabilityWeeks.BookWeeks.ContainsKey(item.ItemId)).ToList();
        var saplingsPool = BuildSaplingPool(fruitTrees, objects, excluded, tuning);
        var geodeMineralsPool = BuildGeodeMineralPool(geodeDrops, objects, excluded, tuning);
        var cookingPool = BuildCategoryPool(objects, CookingCategory, excluded, tuning);
        var tapperGoodsPool = BuildCategoryPoolWithAdditions(
            objects, TapperCategory, TapperGoodsAdditions, excluded, tuning);

        var seasonsById = BuildKnownSeasonsById(cropPool, fishPool, crabPotPool, foragePool);
        var (byKind, colourTags, winterOnly) = BuildByKindAndSpecialSets(objects, excluded, tuning, seasonsById);

        return new ItemPools
        {
            Crops = cropPool,
            Fish = fishPool,
            UnpooledFish = unpooledFish,
            LocationWeeks = locationWeeks,
            CrabPot = crabPotPool,
            Forage = foragePool,
            MonsterDrops = monsterPool,
            Metals = metalsPool,
            ArtisanGoods = artisanPool,
            Artifacts = artifactsPool,
            Books = booksPool,
            Saplings = saplingsPool,
            GeodeMinerals = geodeMineralsPool,
            Cooking = cookingPool,
            TapperGoods = tapperGoodsPool,
            DerivedSeasonPins = DerivePins(cropPool, fishPool, crabPotPool, foragePool),
            QualityEligibleIds = qualityEligible,
            TrapFishIds = new HashSet<string>(trapFishIds.Select(id => Qualify(Unqualify(id))), StringComparer.Ordinal),
            JellyIds = new HashSet<string>(
                fishPool.Where(p => IsJelly(p.ItemId)
                                    || (objects.TryGetValue(Unqualify(p.ItemId), out RawObjectEntry? obj) && IsJellyCatch(obj)))
                    .Select(p => p.ItemId),
                StringComparer.Ordinal),
            FruitTreeFruitIds = new HashSet<string>(
                fruitTrees.SelectMany(t => t.FruitItemIds ?? Array.Empty<string>())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => Qualify(Unqualify(id))),
                StringComparer.Ordinal),
            FishRows = fishRows ?? new Dictionary<string, RawFishEntry>(),
            ByKind = byKind,
            ColourTags = colourTags,
            WinterOnly = winterOnly,
            // Every id kept out on purpose, so a consumer reading ids from outside the pools
            // (a bundle's own vanilla ids) cannot re-admit one. Vets checks the built-in list
            // separately from `excluded`, so both halves are joined here.
            ExcludedIds = new HashSet<string>(
                BuiltInExcludedItemIds.Concat(excluded), StringComparer.Ordinal),
        };
    }

    /// <summary>Item -> known catalog seasons, gathered from the pools that already carry
    /// season data (crops, fish, crab pot, forage). Feeds the ByKind/WinterOnly walk over ALL
    /// Data/Objects, most of which have no season data of their own (empty = unknown/any).</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<Season>> BuildKnownSeasonsById(
        params IReadOnlyList<PoolItem>[] pools)
    {
        var map = new Dictionary<string, IReadOnlyList<Season>>(StringComparer.Ordinal);
        foreach (IReadOnlyList<PoolItem> pool in pools)
            foreach (PoolItem item in pool)
                if (item.Seasons.Count > 0)
                    map[item.ItemId] = item.Seasons;
        return map;
    }

    /// <summary>Walks every Data/Objects entry that passes <see cref="Vets"/> once, building the
    /// per-kind pools, the colour-tag index, and the Winter-only set together. Trophy is
    /// afterwards REPLACED by a fixed weight-3 list built directly from
    /// <see cref="AuthoredBundleCatalog.GilTrophies"/> (hats and weapons are not Data/Objects
    /// rows, so the walk can never find most of them).</summary>
    private static (
        IReadOnlyDictionary<ItemKind, IReadOnlyList<PoolItem>> byKind,
        IReadOnlyDictionary<string, IReadOnlyList<PoolItem>> colourTags,
        IReadOnlyList<PoolItem> winterOnly) BuildByKindAndSpecialSets(
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded,
        BundleGenerationTuning tuning, IReadOnlyDictionary<string, IReadOnlyList<Season>> seasonsById)
    {
        var byKind = new Dictionary<ItemKind, List<PoolItem>>();
        foreach (ItemKind kind in Enum.GetValues<ItemKind>())
            byKind[kind] = new List<PoolItem>();
        var colourTags = new Dictionary<string, List<PoolItem>>(StringComparer.Ordinal);
        var winterOnly = new List<PoolItem>();

        foreach (KeyValuePair<string, RawObjectEntry> entry in objects)
        {
            string bare = entry.Key;
            RawObjectEntry obj = entry.Value;
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded))
                continue;

            IReadOnlyList<Season> seasons = seasonsById.TryGetValue(id, out IReadOnlyList<Season>? known)
                ? known : Array.Empty<Season>();
            PoolItem item = MakeItem(id, objects, tuning, seasons, Array.Empty<string>());
            byKind[ItemKindClassifier.From(bare, obj)].Add(item);

            // The colour index feeds the Dye recipe, and Dye picks only from the six vanilla Dye
            // items plus coloured crops, fruit, flowers, forage and beach finds (Jeff,
            // 2026-09-29). Every other object with a colour tag, dishes, artifacts, bombs, books,
            // rings, Joja Cola, used to be fair game: 60 boards asked for all of them.
            if (IsDyeCandidate(id, obj) && obj.ContextTags != null)
            {
                foreach (string tag in obj.ContextTags)
                {
                    if (!tag.StartsWith(ColourTagPrefix, StringComparison.Ordinal))
                        continue;
                    if (!colourTags.TryGetValue(tag, out List<PoolItem>? list))
                        colourTags[tag] = list = new List<PoolItem>();
                    list.Add(item);
                }
            }

            if (seasons.Count == 1 && seasons[0] == Season.Winter)
                winterOnly.Add(item);
        }

        byKind[ItemKind.Trophy] = AuthoredBundleCatalog.GilTrophies
            .Select(id => new PoolItem(id, 0, TrophyWeight, Array.Empty<Season>(), Array.Empty<string>()))
            .ToList();

        return (
            byKind.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<PoolItem>)Finish(kv.Value)),
            colourTags.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<PoolItem>)Finish(kv.Value), StringComparer.Ordinal),
            Finish(winterOnly));
    }

    private static PoolItem MakeItem(
        string qualifiedId, IReadOnlyDictionary<string, RawObjectEntry> objects,
        BundleGenerationTuning tuning, IReadOnlyList<Season> seasons, IReadOnlyList<string> locations)
    {
        string bare = Unqualify(qualifiedId);
        bool known = objects.TryGetValue(bare, out RawObjectEntry? obj);
        int price = known ? obj!.Price : 0;
        int category = known ? obj!.Category : 0;
        int weight = WeightFor(qualifiedId, tuning);
        return new PoolItem(qualifiedId, price, Math.Max(1, weight), seasons, locations, category);
    }

    private const char ModIdSeparator = '.';

    /// <summary>Draw weight: a named override, else vanilla for any id without a mod prefix
    /// (SMAPI mod items are Author.Mod_Item), else modded. 1.6's own string ids (Goby, the jellies,
    /// Broccoli, Moss, Mystery Box, the books) are vanilla (Jeff, 2026-08-28).</summary>
    public static int WeightFor(string qualifiedId, BundleGenerationTuning tuning)
    {
        if (tuning.RareRollWeights.TryGetValue(qualifiedId, out int over)) return over;
        return Unqualify(qualifiedId).Contains(ModIdSeparator) ? tuning.ModdedItemWeight : tuning.VanillaItemWeight;
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

    /// <summary>Every id that could enter a pool, so the reachability rule is asked about each
    /// exactly once. Data/Objects is the superset for the category pools; crops and spawns are
    /// added because a harvest or catch need not have its own Data/Objects row in a mod.</summary>
    private static IEnumerable<string> AllCandidateIds(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string bare in objects.Keys)
            if (seen.Add(Qualify(bare))) yield return Qualify(bare);
        foreach (RawCropEntry crop in crops)
            if (crop?.HarvestItemId != null && seen.Add(Qualify(Unqualify(crop.HarvestItemId))))
                yield return Qualify(Unqualify(crop.HarvestItemId));
        foreach (RawSpawnEntry spawn in forageSpawns)
            if (spawn?.ItemId != null && seen.Add(Qualify(Unqualify(spawn.ItemId))))
                yield return Qualify(Unqualify(spawn.ItemId));
        foreach (RawSpawnEntry spawn in fishSpawns)
            if (spawn?.ItemId != null && seen.Add(Qualify(Unqualify(spawn.ItemId))))
                yield return Qualify(Unqualify(spawn.ItemId));
    }

    private static string Qualify(string bareId) => BundleParsing.NormalizeItemId(bareId);

    private static string Unqualify(string id)
        => id.StartsWith("(O)", StringComparison.Ordinal) ? id.Substring(3) : id;
}
