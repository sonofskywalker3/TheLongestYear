using System;
using System.Collections.Generic;
using System.Linq;

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
public static class ItemPoolBuilder
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
        IReadOnlyDictionary<string, Season>? festivalSeasons = null)
    {
        var excluded = new HashSet<string>(tuning.ExcludedItemIds, StringComparer.Ordinal);
        // Save-specific exclusions (YearTwoCrops: Pierre's year-2 seeds until the upgrade is owned).
        if (extraExcludedIds != null)
            excluded.UnionWith(extraExcludedIds);

        var cropPool = BuildCropPool(crops, objects, excluded, tuning);
        var (fishPool, crabPotPool) = BuildFishPools(fishSpawns, trapFishIds, objects, excluded, tuning, festivalSeasons);
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
            FruitTreeFruitIds = new HashSet<string>(
                fruitTrees.SelectMany(t => t.FruitItemIds ?? Array.Empty<string>())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => Qualify(Unqualify(id))),
                StringComparer.Ordinal),
            FishRows = fishRows ?? new Dictionary<string, RawFishEntry>(),
            ByKind = byKind,
            ColourTags = colourTags,
            WinterOnly = winterOnly,
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
        foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
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

            if (obj.ContextTags != null)
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

    /// <summary>Maps that only exist while a passive festival runs, keyed to that festival's
    /// Data/PassiveFestivals id. The Night Market replaces the Beach with BeachNightMarket (that
    /// pair is in the festival data) and adds the Submarine, which the data does not mention:
    /// the game gates the market by date in code, so its spawn rows carry no season and read as
    /// all-year (player report 2026-08-28: a Sea Cucumber demanded before Summer 1).</summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltInFestivalLocations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Submarine"] = "NightMarket",
            ["BeachNightMarket"] = "NightMarket",
        };

    /// <summary>Vanilla passive-festival seasons, used only when the caller supplies no
    /// Data/PassiveFestivals table (hand-built pools) or the table lacks the id.</summary>
    private static readonly IReadOnlyDictionary<string, Season> BuiltInFestivalSeasons =
        new Dictionary<string, Season>(StringComparer.OrdinalIgnoreCase)
        {
            ["NightMarket"] = Season.Winter,
            ["SquidFest"] = Season.Winter,
            ["TroutDerby"] = Season.Summer,
            ["DesertFestival"] = Season.Spring,
        };

    private const string PassiveFestivalOpenQuery = "IS_PASSIVE_FESTIVAL_OPEN";
    private static readonly char[] ConditionSeparators = { ' ', ',' };

    /// <summary>Season list for one spawn entry: an explicit Season wins; otherwise any
    /// season names found in the Condition string (best-effort GameStateQuery token scan);
    /// otherwise empty = any season. Note: negated GSQ season clauses (containing '!')
    /// cannot be token-scanned safely, so any negation means "no season signal".</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(Season? season, string? condition)
        => SeasonsFromSpawn(season, condition, null, null);

    /// <summary>As above, plus the passive-festival rule: a row on a festival-only map
    /// (<see cref="BuiltInFestivalLocations"/>) or conditioned on
    /// <c>IS_PASSIVE_FESTIVAL_OPEN &lt;id&gt;</c> is only reachable in that festival's season,
    /// looked up in <paramref name="festivalSeasons"/> (Data/PassiveFestivals, so modded
    /// festivals count) with <see cref="BuiltInFestivalSeasons"/> as the fallback. Explicit
    /// seasons and season tokens still win; an unknown festival is no signal.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(
        Season? season, string? condition, string? location,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null)
    {
        if (season != null)
            return new[] { season.Value };

        string[] tokens = (condition ?? "").Split(ConditionSeparators, StringSplitOptions.RemoveEmptyEntries);
        bool negated = condition != null && condition.Contains('!');
        if (!negated)
        {
            var found = new List<Season>();
            foreach (string token in tokens)
            {
                // IsDefined: Enum.TryParse accepts any integer ("TIME 0600 1800" would read as
                // two nonsense seasons); only a season NAME is a signal.
                if (Enum.TryParse(token, ignoreCase: true, out Season s)
                    && Enum.IsDefined(typeof(Season), s) && !found.Contains(s))
                    found.Add(s);
            }
            if (found.Count > 0)
                return found;
        }

        string? festival = null;
        if (!string.IsNullOrEmpty(location) && BuiltInFestivalLocations.TryGetValue(location, out string? byLocation))
            festival = byLocation;
        for (int i = 0; festival == null && i + 1 < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], PassiveFestivalOpenQuery, StringComparison.OrdinalIgnoreCase))
                festival = tokens[i + 1];
        }
        if (festival != null)
        {
            if (festivalSeasons != null && festivalSeasons.TryGetValue(festival, out Season fromData))
                return new[] { fromData };
            if (BuiltInFestivalSeasons.TryGetValue(festival, out Season builtIn))
                return new[] { builtIn };
        }
        return Array.Empty<Season>();
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

    /// <summary>Fish and crab-pot pools, restricted to items whose Data/Objects Type is
    /// "Fish". Location fish-spawn tables carry non-fish junk/trash entries (e.g. wood,
    /// stone) alongside real fish, so Vets() alone isn't enough — a type check keeps the
    /// pool type-pure for correct bundle classification.</summary>
    private static (IReadOnlyList<PoolItem> fish, IReadOnlyList<PoolItem> crabPot) BuildFishPools(
        IReadOnlyList<RawSpawnEntry> fishSpawns, IReadOnlySet<string> trapFishIds,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning,
        IReadOnlyDictionary<string, Season>? festivalSeasons)
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
            if (string.IsNullOrEmpty(spawn.ItemId))
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

        return (Finish(fish), Finish(crabPot));
    }

    private static IReadOnlyList<PoolItem> BuildForagePool(
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        HashSet<string> excluded, BundleGenerationTuning tuning,
        IReadOnlyDictionary<string, Season>? festivalSeasons)
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
            AddSeasons(id, SeasonsFromSpawn(spawn.Season, spawn.Condition, spawn.Location, festivalSeasons));
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

    /// <summary>Built-in structural exclusions: Ginger Island / Qi-gated content, which is
    /// post-CC and never year-1 obtainable (Nexus 1122358, 2026-08-24 — engine bundles
    /// rolled these on fresh saves). Location markers can't catch them (crops come from
    /// Data/Crops, which has no location; category pools scan all of Data/Objects), and
    /// they must NOT live only in the tuning defaults: an existing config.json overrides
    /// serialized lists wholesale, so config-default-only excludes silently vanish on
    /// every install that has saved a config. Ids verified against the game's Data/Objects.</summary>
    public static readonly IReadOnlySet<string> BuiltInExcludedItemIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)69",  // Banana Sapling
        "(O)835", // Mango Sapling
        "(O)889", // Qi Fruit          — Qi challenge crop (Data/Crops lists all four seasons)
        "(O)832", // Pineapple         — island crop
        "(O)830", // Taro Root         — island crop
        "(O)831", // Taro Tuber        — island seed (Golden Coconut geode drop)
        "(O)833", // Pineapple Seeds   — island seed (Golden Coconut geode drop)
        "(O)91",  // Banana            — island fruit tree
        "(O)834", // Mango             — island fruit tree
        "(O)829", // Ginger            — island forage (also a Golden Coconut drop)
        "(O)851", // Magma Cap         — Volcano forage
        "(O)909", // Radioactive Ore   — island-only (metals pool)
        "(O)910", // Radioactive Bar   — island-only (metals pool)
        "(O)848", // Cinder Shard      — Volcano-only (metals pool)
        "(O)852", // Dragon Tooth      — Volcano-only (Golden Coconut drop)
        "(O)820", // Fossilized Skull  — Golden Coconut drop (island fossil)
        "(O)903", // Ginger Ale        — island dish (cooking pool)
        "(O)904", // Banana Pudding    — island dish
        "(O)905", // Mango Sticky Rice — island dish
        "(O)906", // Poi               — island dish
        "(O)907", // Tropical Curry    — island dish
        "(O)873", // Piña Colada       — island resort drink
        "(O)795", // Void Salmon       — Witch's Swamp only, behind the Dark Talisman quest (post-CC)
    };

    /// <summary>Built-in excluded location markers, merged with the config list by
    /// <see cref="IsExcludedLocation"/> (same config-override rationale as
    /// <see cref="BuiltInExcludedItemIds"/>). BugLand = Mutant Bug Lair: behind the Dark
    /// Talisman quest, which is itself post-CC — never year-1 content. WitchSwamp is behind
    /// the same quest, so Void Salmon is out too (0.12.18; the 2026-08-24 "hard but fair"
    /// ruling assumed the swamp was reachable in year 1, which it is not).</summary>
    public static readonly IReadOnlyList<string> BuiltInExcludedLocationMarkers = new[] { "BugLand", "WitchSwamp" };

    /// <summary>Data/Locations keys that are not places anyone fishes or forages, matched
    /// EXACTLY (case-insensitive) rather than by substring so a modded "Temple" or
    /// "DefaultFarm" map stays in. "Temp" is the Festival of Ice contest map: its rows mix
    /// river and ocean fish (Red Mullet next to Bream) and carry no season, so treating it
    /// as a habitat leaked ocean fish into Lake Fish, river fish into Ocean Fish, and marked
    /// river fish catchable year-round (player report, 2026-08-28). "fishingGame" is the
    /// Fair minigame; "Default" is the trash / Joja Cola table every water shares.</summary>
    public static readonly IReadOnlySet<string> BuiltInNonHabitatLocationKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Default", "Temp", "fishingGame" };

    /// <summary>The vet minus the ExcludeFromRandomSale rule, for Night Market fish (see
    /// BuildFishPools): the flag keeps them out of random shop stock, it does not mean they
    /// cannot be caught.</summary>
    private static bool VetsIgnoringRandomSale(
        string bareId, string qualifiedId,
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded)
    {
        if (BuiltInExcludedItemIds.Contains(qualifiedId) || excluded.Contains(qualifiedId))
            return false;
        if (!objects.TryGetValue(bareId, out RawObjectEntry? obj))
            return false;
        if (string.Equals(obj.Type, QuestType, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>Structural + configured vetting. False = never offer this item. A
    /// PoolAdditions.VetExceptions id skips the ExcludeFromRandomSale check: those are the
    /// curated mine fish and legendaries, wanted despite the flag (spec 2026-08-28-obtainable-board,
    /// section 3).</summary>
    private static bool Vets(
        string bareId, string qualifiedId,
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded)
    {
        if (BuiltInExcludedItemIds.Contains(qualifiedId) || excluded.Contains(qualifiedId))
            return false;
        if (!objects.TryGetValue(bareId, out RawObjectEntry? obj))
            return false; // unknown to Data/Objects — can't price/vet it, drop it
        if (string.Equals(obj.Type, QuestType, StringComparison.OrdinalIgnoreCase))
            return false;
        if (obj.ExcludeFromRandomSale && !PoolAdditions.VetExceptions.Contains(qualifiedId))
            return false;
        return true;
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

    private const string ForageItemTag = "forage_item";
    private const string TruffleId = "(O)430";
    private static readonly int[] ForageCategories = { -79, -80, -81, -75, -23 };

    /// <summary>Mirrors StardewValley.Object.isForage(): the only objects the game gives
    /// forage quality to when picked up.</summary>
    public static bool IsForageCategory(RawObjectEntry obj, string qualifiedId)
        => Array.IndexOf(ForageCategories, obj.Category) >= 0
           || (obj.ContextTags != null && obj.ContextTags.Contains(ForageItemTag))
           || qualifiedId == TruffleId;

    /// <summary>River/Sea/Cave Jelly are rod catches that never carry quality.</summary>
    public static bool IsJelly(string qualifiedId)
        => Unqualify(qualifiedId).EndsWith("Jelly", StringComparison.Ordinal);

    private static IReadOnlySet<string> BuildQualityEligibleIds(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns,
        IReadOnlySet<string> trapFishIds,
        HashSet<string> excluded)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawCropEntry crop in crops)
        {
            if (string.IsNullOrEmpty(crop.HarvestItemId)) continue;
            if (crop.HarvestMaxQuality == 0) continue; // CropData clamps to base quality (e.g. Fiber)
            string bare = Unqualify(crop.HarvestItemId);
            string id = Qualify(bare);
            if (Vets(bare, id, objects, excluded)) result.Add(id);
        }
        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId)) continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded)) continue;
            if (!objects.TryGetValue(bare, out RawObjectEntry? obj)
                || !string.Equals(obj.Type, FishType, StringComparison.OrdinalIgnoreCase)) continue;
            if (trapFishIds.Contains(bare) || IsJelly(id)) continue;
            result.Add(id);
        }
        foreach (RawSpawnEntry spawn in forageSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId)) continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded)) continue;
            if (objects.TryGetValue(bare, out RawObjectEntry? obj) && IsForageCategory(obj, id))
                result.Add(id);
        }
        return result;
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

    /// <summary>True when a Data/Locations key is a built-in non-habitat key
    /// (<see cref="BuiltInNonHabitatLocationKeys"/>, exact match) or matches any
    /// excluded-location marker — built-in (<see cref="BuiltInExcludedLocationMarkers"/>)
    /// or configured — (case-insensitive substring): such locations never feed the pools.</summary>
    public static bool IsExcludedLocation(string locationKey, IReadOnlyList<string> markers)
    {
        if (BuiltInNonHabitatLocationKeys.Contains(locationKey))
            return true;
        foreach (string marker in BuiltInExcludedLocationMarkers.Concat(markers))
        {
            if (!string.IsNullOrEmpty(marker)
                && locationKey.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string Qualify(string bareId) => BundleParsing.NormalizeItemId(bareId);

    private static string Unqualify(string id)
        => id.StartsWith("(O)", StringComparison.Ordinal) ? id.Substring(3) : id;
}
