using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;

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

        private readonly IMonitor _monitor;

        public GameDataPools(IMonitor monitor) => _monitor = monitor;

        /// <summary>The reachability verdicts from the most recent <see cref="Build"/> on this
        /// instance. Null before the first call, and null whenever the shop/recipe/warp reads
        /// threw (fail open: no item is excluded for reachability that generation). Held so the
        /// board repair and tly_dumpbundles report exactly what the pools were built from, rather
        /// than re-deriving it.</summary>
        public SourceReachability LastReachability { get; private set; }

        /// <param name="extraExcludedIds">Save-specific exclusions merged into the tuning's
        /// excluded ids (YearTwoCrops.ExcludedFor on the current MetaState); null = none.</param>
        public ItemPools Build(BundleGenerationTuning tuning, IReadOnlySet<string> extraExcludedIds = null)
        {
            var crops = new List<RawCropEntry>();
            var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal);
            var forage = new List<RawSpawnEntry>();
            var fish = new List<RawSpawnEntry>();
            var trapIds = new HashSet<string>(StringComparer.Ordinal);
            var fishRows = new List<RawFishEntry>();
            var drops = new List<RawMonsterDropEntry>();
            var fruitTrees = new List<RawFruitTreeEntry>();
            var geodeDrops = new List<RawGeodeDropEntry>();
            var festivalSeasons = new Dictionary<string, Core.Season>(StringComparer.OrdinalIgnoreCase);

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
                {
                    var fruitIds = (kv.Value?.Fruit ?? new List<FruitTreeFruitData>())
                        .Where(f => f != null && !string.IsNullOrEmpty(f.ItemId))
                        .Select(f => f.ItemId)
                        .ToList();
                    fruitTrees.Add(new RawFruitTreeEntry(kv.Key, fruitIds));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    crops.Add(new RawCropEntry(c.HarvestItemId, MapSeasons(c.Seasons), c.HarvestMaxQuality, kv.Key));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    if (ItemPoolBuilder.IsExcludedLocation(kv.Key, tuning.ExcludedLocationMarkers))
                        continue;
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (SpawnForageData f in (loc.Forage ?? new List<SpawnForageData>()).Where(r => !PastSeasonSpawn.IsCopy(r?.Id)))
                        foreach (string id in SpawnItemIds(f.ItemId, f.RandomItemId))
                            forage.Add(new RawSpawnEntry(id, MapSeason(f.Season), f.Condition, kv.Key));
                    foreach (SpawnFishData f in (loc.Fish ?? new List<SpawnFishData>()).Where(r => !PastSeasonSpawn.IsCopy(r?.Id)))
                        foreach (string id in SpawnItemIds(f.ItemId, f.RandomItemId))
                            fish.Add(new RawSpawnEntry(id, MapSeason(f.Season), f.Condition, kv.Key));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Fish"))
                {
                    RawFishEntry entry = RawFishEntry.Parse(kv.Key, kv.Value);
                    fishRows.Add(entry);
                    if (entry.IsTrap)
                        trapIds.Add(kv.Key);
                }

                // Passive festivals (Night Market, SquidFest, Trout Derby...): a spawn row on a
                // festival-only map or behind IS_PASSIVE_FESTIVAL_OPEN is only reachable in that
                // festival's season. See ItemPoolBuilder.SeasonsFromSpawn.
                foreach (var kv in Game1.content.Load<Dictionary<string, StardewValley.GameData.PassiveFestivalData>>("Data/PassiveFestivals"))
                {
                    if (kv.Value == null) continue;
                    festivalSeasons[kv.Key] = MapSeasonValue(kv.Value.Season);
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
                    $"GameDataPools: data read failed ({ex.GetType().Name}: {ex.Message}), " +
                    "pools may be partial; affected bundles keep their vanilla slots.",
                    LogLevel.Warn);
            }

            // Reachability (spec 2026-09-10-source-reachability): reads the shop, recipe and warp
            // tables and asks which items are provably out of reach this run. Kept in its OWN
            // try/catch, separate from the pool reads above: fail open on ANY exception here,
            // because a partial source graph is worse than none (it looks authoritative while
            // missing exactly the alternative route that would have kept an item allowed).
            SourceReachability reachability = null;
            try
            {
                var shopListings = new List<RawShopListing>();
                // shopId -> owner NPC names, for placing shops that are opened by talking to
                // someone rather than by an "OpenShop" map tile (the Fishmonger case that
                // prompted this whole feature: its shop has an Owners list and no tile action
                // anywhere).
                var shopOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                {
                    if (kv.Value == null) continue;
                    foreach (var entry in kv.Value.Items ?? new List<ShopItemData>())
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;
                        if (!ItemIsObject(entry.ItemId)) continue;
                        shopListings.Add(new RawShopListing(entry.ItemId, kv.Key, entry.IsRecipe));
                    }
                    foreach (var owner in kv.Value.Owners ?? new List<ShopOwnerData>())
                    {
                        if (string.IsNullOrEmpty(owner?.Name)) continue;
                        if (!shopOwners.TryGetValue(kv.Key, out List<string> names))
                            shopOwners[kv.Key] = names = new List<string>();
                        if (!names.Contains(owner.Name)) names.Add(owner.Name);
                    }
                }

                var recipes = new List<RawRecipeEntry>();
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes"))
                {
                    // ingredients / unused / yield / unlockConditions / displayName
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length < 4) continue;
                    string[] tokens = fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var ingredients = new List<string>();
                    for (int i = 0; i + 1 < tokens.Length; i += 2) ingredients.Add(tokens[i]);
                    string output = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? kv.Key;
                    recipes.Add(new RawRecipeEntry(output, ingredients, fields[3]));
                }

                // Crafting outputs, POSITIVE PROOF ONLY (2026-09-10 fix round 1): a craftable item
                // is reachable, full stop, so its output id joins reachableSpawnIds below. This is
                // deliberately NOT a recipe source rule (no _recipesByOutput entry, no ingredient or
                // learnability check): 0.18 draws no conclusion about whether a craftable item is
                // UNREACHABLE, only ever the opposite. Format verified against the decompiled
                // CraftingRecipe(string, bool) constructor: ingredients / unused / output / bigCraftable
                // / unlockConditions / displayName, the same ingredient/output field indices as cooking
                // (index 3 differs: bigCraftable bool here, not unlockConditions), so field[2] is the
                // output pair "id qty..." exactly like cooking's field[2]. Big-craftable outputs are
                // skipped here: they need the (BC) qualifier, not (O), and this class only ever tracks
                // (O) ids, so leaving field 3 unread would wrongly mark an OBJECT with the same
                // numeric id as reachable.
                const int CraftingBigCraftableFieldIndex = 3;
                var craftingOutputs = new List<string>();
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CraftingRecipes"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length <= CraftingBigCraftableFieldIndex) continue;
                    if (bool.TryParse(fields[CraftingBigCraftableFieldIndex], out bool isBigCraftable) && isBigCraftable)
                        continue;
                    string output = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(output)) craftingOutputs.Add(output);
                }

                var links = new List<RawLocationLink>();
                var allLocations = new List<string>();
                var shopPlacements = new List<RawShopPlacement>();
                foreach (GameLocation location in Game1.locations)
                {
                    if (location?.Name == null) continue;
                    allLocations.Add(location.Name);
                    foreach (StardewValley.Warp warp in location.warps)
                        if (!string.IsNullOrEmpty(warp?.TargetName))
                            links.Add(new RawLocationLink(location.Name, warp.TargetName));

                    // A shop is "in" the location whose tiles open it. This finds nothing for
                    // shops opened by talking to an NPC (see the owner-placement loop below,
                    // which is the path that matters).
                    foreach (string shopId in ShopIdsOpenedIn(location))
                        shopPlacements.Add(new RawShopPlacement(shopId, location.Name));
                }

                // Place each shop at both its owner's home and its owner's current location.
                // Both count, and a shop is only condemned when every placement is unreachable,
                // so listing more places is the conservative direction.
                foreach (KeyValuePair<string, List<string>> shop in shopOwners)
                {
                    foreach (string ownerName in shop.Value)
                    {
                        if (ownerName == "AnyOrNone" || ownerName == "Any" || ownerName == "None") continue;
                        NPC npc = Game1.getCharacterFromName(ownerName);
                        if (npc == null) continue;
                        if (!string.IsNullOrEmpty(npc.DefaultMap))
                            shopPlacements.Add(new RawShopPlacement(shop.Key, npc.DefaultMap));
                        if (!string.IsNullOrEmpty(npc.currentLocation?.Name))
                            shopPlacements.Add(new RawShopPlacement(shop.Key, npc.currentLocation.Name));
                    }
                }

                // Positive-reachability evidence: every id the game already told us spawns
                // somewhere, from tables this method has ALREADY read above. Without this, an
                // item that is forageable AND also listed in an unreachable island shop would be
                // condemned by the shop rule while its perfectly good spawn never got a vote.
                var reachableSpawnIds = new HashSet<string>(StringComparer.Ordinal);
                void MarkSpawn(string rawId)
                {
                    if (string.IsNullOrEmpty(rawId)) return;
                    reachableSpawnIds.Add(BundleParsing.NormalizeItemId(rawId));
                }
                foreach (RawSpawnEntry spawn in forage) MarkSpawn(spawn?.ItemId);
                foreach (RawSpawnEntry spawn in fish) MarkSpawn(spawn?.ItemId);
                // Crab-pot ids (Lobster, Crab, Cockle, Mussel, Oyster, Shrimp, Snail, Periwinkle,
                // Crayfish, any mod trap fish): trap-only catches have no Data/Locations row, so
                // nothing else speaks for them on the reachable side. Same failure mode as the
                // Driftwood bug above: a mod listing one in an unreachable shop would condemn it.
                foreach (string trapId in trapIds) MarkSpawn(trapId);
                foreach (RawMonsterDropEntry drop in drops) MarkSpawn(drop?.ItemId);
                foreach (RawGeodeDropEntry drop in geodeDrops) MarkSpawn(drop?.ItemId);
                foreach (RawFruitTreeEntry tree in fruitTrees)
                    foreach (string fruit in tree?.FruitItemIds ?? Array.Empty<string>())
                        MarkSpawn(fruit);
                // Fishing trash (167-172) comes off the line from day 1 in any reachable water and
                // has no Data/Locations row to speak for it (FishingTrashAvailability); without this
                // it was invisible to the positive-proof set, so a mod that also lists a trash id in
                // an unreachable shop (e.g. Driftwood, 169) got it wrongly condemned.
                foreach (string trashId in FishingTrashAvailability.QualifiedIds())
                    MarkSpawn(trashId);
                // Crafting outputs: positive proof only (see the read loop above for why this is not
                // a source rule).
                foreach (string output in craftingOutputs)
                    MarkSpawn(output);

                IReadOnlySet<string> unreachablePlaces = ReachabilityGraph.UnreachableLocations(
                    links, allLocations,
                    name => ItemPoolBuilder.IsExcludedLocation(name, tuning.ExcludedLocationMarkers));
                reachability = new SourceReachability(
                    unreachablePlaces, shopListings, shopPlacements, crops, recipes, reachableSpawnIds);
                _monitor?.Log(
                    $"Reachability: {unreachablePlaces.Count} of {allLocations.Count} locations out of reach.",
                    LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor?.Log(
                    $"Reachability derivation failed ({ex.GetType().Name}: {ex.Message}). " +
                    "No item will be excluded for reachability this generation.",
                    LogLevel.Warn);
                reachability = null;
            }
            this.LastReachability = reachability;

            ItemPools pools = ItemPoolBuilder.Build(
                crops, objects, forage, fish, trapIds, drops,
                fruitTrees, geodeDrops, tuning, extraExcludedIds,
                fishRows.ToDictionary(r => r.ItemId, StringComparer.Ordinal),
                festivalSeasons, reachability);
            _monitor?.Log(
                $"GameDataPools: crops {pools.Crops.Count}, fish {pools.Fish.Count}, " +
                $"crab-pot {pools.CrabPot.Count}, forage {pools.Forage.Count}, " +
                $"monster {pools.MonsterDrops.Count}, metals {pools.Metals.Count}, " +
                $"artisan {pools.ArtisanGoods.Count}, saplings {pools.Saplings.Count}, " +
                $"geode-minerals {pools.GeodeMinerals.Count}, artifacts {pools.Artifacts.Count}, " +
                $"books {pools.Books.Count}, cooking {pools.Cooking.Count}, " +
                $"tapper {pools.TapperGoods.Count}; derived season pins {pools.DerivedSeasonPins.Count}.",
                LogLevel.Trace);
            if (reachability != null && reachability.Reasons.Count > 0)
            {
                _monitor?.Log($"Reachability: {reachability.Reasons.Count} items kept off the board.", LogLevel.Info);
                foreach (var reason in reachability.Reasons)
                    _monitor?.Log($"  {reason.Key}: {reason.Value}", LogLevel.Trace);
            }
            return pools;
        }

        /// <summary>Shop ids opened by an "OpenShop" tile action anywhere in this location. This is
        /// how a shop gets a place when Data/Shops itself records no location. The owner-based
        /// placement above is the important path; this tile scan is a secondary, best-effort
        /// source that finds nothing for shops (like the Fishmonger's) opened by talking to an
        /// NPC rather than by a map tile.</summary>
        private static IEnumerable<string> ShopIdsOpenedIn(GameLocation location)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            xTile.Layers.Layer layer = location.Map?.GetLayer("Buildings");
            if (layer == null) yield break;
            for (int x = 0; x < layer.LayerWidth; x++)
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                xTile.Tiles.Tile tile = layer.Tiles[x, y];
                if (tile == null) continue;
                if (!tile.Properties.TryGetValue("Action", out xTile.ObjectModel.PropertyValue value)) continue;
                string action = value?.ToString() ?? "";
                if (!action.StartsWith("OpenShop", StringComparison.OrdinalIgnoreCase)) continue;
                string[] parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && found.Add(parts[1])) yield return parts[1];
            }
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
