using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Manages the Junimo Stash chest lifecycle across runs.
    ///
    /// The Farm is wiped on every reset (loadForNewGame), so the chest cannot rely
    /// on normal world persistence. Instead:
    ///   - <see cref="PlaceChest"/> creates a fresh Chest at the configured tile after each reset.
    ///   - <see cref="PopulateFromMeta"/> fills the newly placed chest from <see cref="MetaState.StashItems"/>.
    ///   - <see cref="BankToMeta"/> reads the chest's current contents and serialises them
    ///     back into <see cref="MetaState.StashItems"/> (called from MetaStore.Save on the game's
    ///     Saving event — never eagerly, to match the anti-save-scum invariant).
    ///
    /// The chest is identified by <c>modData["tly.junimo.stash"] == "1"</c>.
    /// </summary>
    internal sealed class JunimoStashService
    {
        internal const string StashModDataKey = "tly.junimo.stash";

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly GameplayConfig _config;

        /// <summary>The tile the chest was most recently placed at. Cached so the indicator
        /// + FindStashChest both track the actually-resolved tile (which may differ from
        /// the config when auto-pick or fallback kicked in).</summary>
        private Vector2? _placedTile;

        public JunimoStashService(IMonitor monitor, MetaState meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta    = meta;
            _config  = config;
        }

        /// <summary>
        /// Place a fresh stash Chest on the Farm at the resolved tile.
        ///
        /// Tile resolution:
        ///   - Config (0,0) sentinel = auto-pick relative to <c>Farm.GetMainFarmHouseEntry()</c>.
        ///   - Configured tile is validated; if it's blocked (building/resource/terrain feature)
        ///     we fall back to auto-pick and log loudly so the player isn't left hunting an
        ///     invisible chest. The 2026-05-27 playtest reported "I don't see a stash chest?"
        ///     when the prior hardcoded default (72, 12) landed under the farmhouse roof.
        ///
        /// Idempotent: removes any existing tagged chest before placing a new one.
        /// </summary>
        public void PlaceChest()
        {
            Farm farm = Game1.getFarm();
            if (farm == null)
            {
                _monitor.Log("JunimoStashService: getFarm() returned null — skipping placement.", LogLevel.Warn);
                return;
            }

            Vector2 tile = ResolveTile(farm);
            if (tile == Vector2.Zero)
            {
                _monitor.Log(
                    "JunimoStashService: could not resolve a valid tile — chest NOT placed. " +
                    "Use tly_setstash to anchor it manually.",
                    LogLevel.Warn);
                return;
            }

            // Remove any stale tagged chest at the resolved tile (idempotency).
            if (farm.objects.ContainsKey(tile)
                && farm.objects[tile] is Chest existing
                && existing.modData.ContainsKey(StashModDataKey))
            {
                farm.objects.Remove(tile);
                _monitor.Log(
                    $"JunimoStashService: removed stale stash chest at ({tile.X}, {tile.Y}).",
                    LogLevel.Trace);
            }

            // Place a new player chest (playerChest = true enables the vanilla 36-slot layout;
            // we cap via JunimoStashCapPatch, not by changing the chest's internal size).
            var chest = new Chest(playerChest: true, tile);
            chest.modData[StashModDataKey] = "1";
            farm.objects[tile] = chest;
            _placedTile = tile;

            _monitor.Log(
                $"JunimoStashService: placed stash chest at ({tile.X}, {tile.Y}), " +
                $"cap={_meta.StashSlotCount} slots.",
                LogLevel.Info);
        }

        /// <summary>
        /// Resolve the tile to place the stash chest at. Returns Vector2.Zero only if we
        /// cannot find any valid tile (extremely degenerate Farm state).
        /// </summary>
        private Vector2 ResolveTile(Farm farm)
        {
            bool isAuto = _config.StashTileX == 0 && _config.StashTileY == 0;
            Vector2 desired = isAuto ? AutoTile(farm) : new Vector2(_config.StashTileX, _config.StashTileY);

            // Empty Farm somehow — auto-pick gave Zero. Bail.
            if (isAuto && desired == Vector2.Zero)
                return Vector2.Zero;

            if (IsTilePlaceable(farm, desired))
                return desired;

            // Configured tile is blocked. Try auto fallback (skip if already auto).
            if (!isAuto)
            {
                Vector2 auto = AutoTile(farm);
                if (auto != Vector2.Zero && IsTilePlaceable(farm, auto))
                {
                    _monitor.Log(
                        $"JunimoStashService: configured tile ({desired.X}, {desired.Y}) is blocked " +
                        $"({DescribeBlocker(farm, desired)}); falling back to auto tile ({auto.X}, {auto.Y}). " +
                        "Run tly_setstash to anchor a different tile if this doesn't fit your farm.",
                        LogLevel.Warn);
                    return auto;
                }
            }

            // Last resort: place at the desired tile anyway and hope the overlay is visible.
            // (Better than no chest at all — the player can use tly_setstash to relocate.)
            _monitor.Log(
                $"JunimoStashService: no clear tile near the farmhouse — placing at ({desired.X}, {desired.Y}) " +
                $"despite blocker ({DescribeBlocker(farm, desired)}). Use tly_setstash to relocate.",
                LogLevel.Warn);
            return desired;
        }

        /// <summary>Pick a tile two east + one south of the farmhouse entry — the spot a player
        /// walking out the door cannot miss. Returns Vector2.Zero if the entry is unavailable.</summary>
        private static Vector2 AutoTile(Farm farm)
        {
            try
            {
                Point entry = farm.GetMainFarmHouseEntry();
                return new Vector2(entry.X + 2, entry.Y + 1);
            }
            catch
            {
                return Vector2.Zero;
            }
        }

        /// <summary>
        /// True when the tile is clear enough that a chest placed there will be visible and
        /// reachable. Checks: no building, no resource clump, no existing object, no terrain
        /// feature (tree/grass) and no large terrain feature (bush) — even though objects/terrain
        /// don't physically block the place, they obscure the chest visually and that's exactly
        /// what burned the user on (72, 12).
        /// </summary>
        private static bool IsTilePlaceable(Farm farm, Vector2 tile)
        {
            if (!farm.isTileOpenBesidesTerrainFeatures(tile))
                return false;
            if (farm.terrainFeatures.ContainsKey(tile))
                return false;
            var rect = new Microsoft.Xna.Framework.Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
            foreach (var ltf in farm.largeTerrainFeatures)
                if (ltf.getBoundingBox().Intersects(rect))
                    return false;
            return true;
        }

        /// <summary>Best-effort human-readable blocker description for the log.</summary>
        private static string DescribeBlocker(Farm farm, Vector2 tile)
        {
            var rect = new Microsoft.Xna.Framework.Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
            foreach (var b in farm.buildings)
                if (b.intersects(rect))
                    return $"building '{b.buildingType.Value}'";
            foreach (var c in farm.resourceClumps)
                if (c.getBoundingBox().Intersects(rect))
                    return "resource clump";
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj))
                return $"object '{obj?.QualifiedItemId ?? "?"}'";
            if (farm.terrainFeatures.ContainsKey(tile))
                return $"terrain feature '{farm.terrainFeatures[tile]?.GetType().Name ?? "?"}'";
            foreach (var ltf in farm.largeTerrainFeatures)
                if (ltf.getBoundingBox().Intersects(rect))
                    return $"large terrain feature '{ltf.GetType().Name}'";
            return "tile not passable";
        }

        /// <summary>
        /// Fill the placed stash chest from <see cref="MetaState.StashItems"/>.
        /// Call after <see cref="PlaceChest"/> on each reset. No-op if no chest is placed.
        /// </summary>
        public void PopulateFromMeta()
        {
            Chest chest = FindStashChest();
            if (chest == null)
                return;

            int restored = 0;
            foreach (StashItemRecord record in _meta.StashItems)
            {
                Item item = ItemRegistry.Create(record.ItemId, record.Quantity, record.Quality,
                    allowNull: true);
                if (item == null)
                {
                    _monitor.Log(
                        $"JunimoStashService: could not recreate item '{record.ItemId}' (unknown id) — skipping.",
                        LogLevel.Warn);
                    continue;
                }
                chest.Items.Add(item);
                restored++;
            }

            _monitor.Log(
                $"JunimoStashService: restored {restored}/{_meta.StashItems.Count} items into stash chest.",
                LogLevel.Trace);
        }

        /// <summary>
        /// Read the stash chest's current contents and write them into
        /// <see cref="MetaState.StashItems"/>. Called by MetaStore.Save on the Saving event.
        /// Overwrites whatever was previously in StashItems — the chest is the authoritative source.
        /// No-op if no stash upgrade is owned or the tile is not configured.
        /// </summary>
        public void BankToMeta()
        {
            Chest chest = FindStashChest();
            if (chest == null)
            {
                // No chest to read from (e.g. player hasn't purchased stash_1 yet, or tile not set).
                return;
            }

            _meta.StashItems.Clear();
            foreach (Item item in chest.Items)
            {
                if (item == null) continue;
                _meta.StashItems.Add(new StashItemRecord(
                    item.QualifiedItemId,
                    item.Stack,
                    (item as StardewValley.Object)?.quality.Value ?? 0));
            }

            _monitor.Log(
                $"JunimoStashService: banked {_meta.StashItems.Count} items into MetaState.StashItems.",
                LogLevel.Trace);
        }

        /// <summary>
        /// Register the stash indicator bubble on the Farm at the placed tile.
        /// Called from WorldResetService.RegisterIndicators after the chest is placed.
        /// </summary>
        public void RegisterIndicator()
        {
            if (_placedTile == null)
                return;

            Farm farm = Game1.getFarm();
            if (farm == null) return;
            IndicatorRegistry.Register("tly.stash", farm, _placedTile.Value, IndicatorKind.Question);
        }

        /// <summary>
        /// Find the stash Chest in the current Farm's object layer. Returns null if not found.
        /// </summary>
        public Chest FindStashChest()
        {
            Farm farm = Game1.getFarm();
            if (farm == null)
                return null;

            // Prefer the cached placement tile (handles auto-pick / fallback correctly).
            if (_placedTile != null
                && farm.objects.TryGetValue(_placedTile.Value, out StardewValley.Object obj)
                && obj is Chest chest
                && chest.modData.ContainsKey(StashModDataKey))
            {
                return chest;
            }

            // Belt-and-braces: scan the farm for the tagged chest in case the cache is stale
            // (e.g. someone called FindStashChest without PlaceChest first this session).
            foreach (var pair in farm.objects.Pairs)
            {
                if (pair.Value is Chest c && c.modData.ContainsKey(StashModDataKey))
                {
                    _placedTile = pair.Key;
                    return c;
                }
            }

            return null;
        }
    }
}
