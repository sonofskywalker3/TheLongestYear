using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Reads the live game data tables into the Core Raw* boundary records and
    /// delegates pool derivation to the pure ItemPoolBuilder. Because everything is read
    /// from the game's OWN data at generation time, mod-added content (SVE crops/fish/
    /// forage/monsters) joins the pools automatically — the spec's "SVE-proof by
    /// construction". Locations whose key matches a tuning.ExcludedLocationMarkers entry
    /// are skipped (Ginger Island and other post-CC / late-game areas are not year-1
    /// content). All failures degrade to smaller pools, never throw: a bundle whose pool
    /// can't fill it keeps its vanilla slots (filler fallback).</summary>
    internal sealed class GameDataPools
    {
        private const int MonsterDropListFieldIndex = 6;
        private const int FishTrapFieldIndex = 1;
        private const string FishTrapMarker = "trap";

        private readonly IMonitor _monitor;

        public GameDataPools(IMonitor monitor) => _monitor = monitor;

        public ItemPools Build(BundleGenerationTuning tuning)
        {
            var crops = new List<RawCropEntry>();
            var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal);
            var forage = new List<RawSpawnEntry>();
            var fish = new List<RawSpawnEntry>();
            var trapIds = new HashSet<string>(StringComparer.Ordinal);
            var drops = new List<RawMonsterDropEntry>();
            var fruitTrees = new List<RawFruitTreeEntry>();
            var geodeDrops = new List<RawGeodeDropEntry>();

            try
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ObjectData>>("Data/Objects"))
                {
                    ObjectData o = kv.Value;
                    if (o == null) continue;
                    objects[kv.Key] = new RawObjectEntry(
                        o.Type ?? "", o.Category, o.Price, o.ExcludeFromRandomSale,
                        (IReadOnlyList<string>)(o.ContextTags ?? new List<string>()));

                    foreach (ObjectGeodeDropData geodeDrop in o.GeodeDrops ?? new List<ObjectGeodeDropData>())
                    {
                        if (geodeDrop == null) continue;
                        if (!string.IsNullOrEmpty(geodeDrop.ItemId))
                            geodeDrops.Add(new RawGeodeDropEntry(geodeDrop.ItemId));
                        foreach (string randomId in geodeDrop.RandomItemId ?? new List<string>())
                            if (!string.IsNullOrEmpty(randomId))
                                geodeDrops.Add(new RawGeodeDropEntry(randomId));
                    }
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, FruitTreeData>>("Data/FruitTrees"))
                    fruitTrees.Add(new RawFruitTreeEntry(kv.Key));

                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    crops.Add(new RawCropEntry(c.HarvestItemId, MapSeasons(c.Seasons), c.HarvestMaxQuality));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    if (ItemPoolBuilder.IsExcludedLocation(kv.Key, tuning.ExcludedLocationMarkers))
                        continue;
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (SpawnForageData f in loc.Forage ?? new List<SpawnForageData>())
                        foreach (string id in SpawnItemIds(f.ItemId, f.RandomItemId))
                            forage.Add(new RawSpawnEntry(id, MapSeason(f.Season), f.Condition, kv.Key));
                    foreach (SpawnFishData f in loc.Fish ?? new List<SpawnFishData>())
                        foreach (string id in SpawnItemIds(f.ItemId, f.RandomItemId))
                            fish.Add(new RawSpawnEntry(id, MapSeason(f.Season), f.Condition, kv.Key));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Fish"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length > FishTrapFieldIndex && fields[FishTrapFieldIndex] == FishTrapMarker)
                        trapIds.Add(kv.Key);
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Monsters"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length <= MonsterDropListFieldIndex) continue;
                    // Decompile-verified (Monster.parseMonsterInfo): space-separated pairs,
                    // item id FIRST, drop chance SECOND.
                    string[] pairs = fields[MonsterDropListFieldIndex]
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i + 1 < pairs.Length; i += 2)
                        drops.Add(new RawMonsterDropEntry(pairs[i]));
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log(
                    $"GameDataPools: data read failed ({ex.GetType().Name}: {ex.Message}) — " +
                    "pools may be partial; affected bundles keep their vanilla slots.",
                    LogLevel.Warn);
            }

            ItemPools pools = ItemPoolBuilder.Build(
                crops, objects, forage, fish, trapIds, drops,
                fruitTrees, geodeDrops, tuning);
            _monitor?.Log(
                $"GameDataPools: crops {pools.Crops.Count}, fish {pools.Fish.Count}, " +
                $"crab-pot {pools.CrabPot.Count}, forage {pools.Forage.Count}, " +
                $"monster {pools.MonsterDrops.Count}, metals {pools.Metals.Count}, " +
                $"artisan {pools.ArtisanGoods.Count}, saplings {pools.Saplings.Count}, " +
                $"geode-minerals {pools.GeodeMinerals.Count}, artifacts {pools.Artifacts.Count}, " +
                $"books {pools.Books.Count}, cooking {pools.Cooking.Count}, " +
                $"tapper {pools.TapperGoods.Count}; derived season pins {pools.DerivedSeasonPins.Count}.",
                LogLevel.Trace);
            return pools;
        }

        private static IEnumerable<string> SpawnItemIds(string itemId, List<string> randomItemId)
        {
            if (!string.IsNullOrEmpty(itemId) && ItemIsObject(itemId))
                yield return itemId;
            foreach (string id in randomItemId ?? new List<string>())
                if (!string.IsNullOrEmpty(id) && ItemIsObject(id))
                    yield return id;
        }

        /// <summary>Only plain objects belong in bundle pools (spawn ids can be qualified
        /// with any item type; bundles can only ask for objects).</summary>
        private static bool ItemIsObject(string id)
            => !id.StartsWith("(", StringComparison.Ordinal)
               || id.StartsWith("(O)", StringComparison.Ordinal);

        private static Core.Season? MapSeason(StardewValley.Season? season)
            => season == null ? null : MapSeasonValue(season.Value);

        private static IReadOnlyList<Core.Season> MapSeasons(List<StardewValley.Season> seasons)
            => (seasons ?? new List<StardewValley.Season>()).Select(MapSeasonValue).ToList();

        private static Core.Season MapSeasonValue(StardewValley.Season season)
            => Enum.Parse<Core.Season>(season.ToString(), ignoreCase: true); // map by NAME, never cast
    }
}
