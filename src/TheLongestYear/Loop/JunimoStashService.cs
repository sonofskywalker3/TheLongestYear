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

        public JunimoStashService(IMonitor monitor, MetaState meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta    = meta;
            _config  = config;
        }

        /// <summary>
        /// Place a fresh stash Chest on the Farm at the configured tile.
        /// No-ops if the tile is (0,0) (disabled) or the stash upgrade has not been purchased.
        /// Idempotent: removes any existing tagged chest at that tile before placing a new one.
        /// </summary>
        public void PlaceChest()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return;

            if (_meta.StashSlotCount == 0)
                return;

            Farm farm = Game1.getFarm();
            Vector2 tile = new Vector2(_config.StashTileX, _config.StashTileY);

            // Remove any stale tagged chest (from a prior reset) to avoid duplication.
            if (farm.objects.ContainsKey(tile)
                && farm.objects[tile] is Chest existing
                && existing.modData.ContainsKey(StashModDataKey))
            {
                farm.objects.Remove(tile);
                _monitor.Log(
                    $"JunimoStashService: removed stale stash chest at ({_config.StashTileX}, {_config.StashTileY}).",
                    LogLevel.Trace);
            }

            // Place a new player chest (playerChest = true enables the vanilla 36-slot layout;
            // we cap via JunimoStashCapPatch, not by changing the chest's internal size).
            var chest = new Chest(playerChest: true, tile);
            chest.modData[StashModDataKey] = "1";
            farm.objects[tile] = chest;

            _monitor.Log(
                $"JunimoStashService: placed stash chest at ({_config.StashTileX}, {_config.StashTileY}), " +
                $"cap={_meta.StashSlotCount} slots.",
                LogLevel.Info);
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
        /// Register the stash indicator bubble on the Farm.
        /// Called from WorldResetService.RegisterIndicators after the chest is placed.
        /// </summary>
        public void RegisterIndicator()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return;
            if (_meta.StashSlotCount == 0)
                return;

            Farm farm = Game1.getFarm();
            IndicatorRegistry.Register("tly.stash", farm,
                new Vector2(_config.StashTileX, _config.StashTileY),
                IndicatorKind.Question);
        }

        /// <summary>
        /// Find the stash Chest in the current Farm's object layer. Returns null if not found.
        /// </summary>
        public Chest FindStashChest()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return null;

            Vector2 tile = new Vector2(_config.StashTileX, _config.StashTileY);
            Farm farm = Game1.getFarm();
            if (farm == null)
                return null;

            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj)
                && obj is Chest chest
                && chest.modData.ContainsKey(StashModDataKey))
            {
                return chest;
            }

            return null;
        }
    }
}
