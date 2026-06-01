using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        /// <summary>Loads + caches the recolored purple Junimo-chest sprite drawn for the stash.
        /// Set from ModEntry (where the mod helper is available); the draw patch pulls from it.</summary>
        private static System.Func<Texture2D> _loadStashTexture;
        private static Texture2D _stashTexture;

        /// <summary>Wire the stash sprite loader (call once at startup with the mod content helper).</summary>
        public static void SetTextureLoader(System.Func<Texture2D> loader) => _loadStashTexture = loader;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly GameplayConfig _config;

        /// <summary>The tile the chest was most recently placed at. Cached so the indicator
        /// + FindStashChest both track the actually-resolved tile (which may differ from
        /// the config when auto-pick or fallback kicked in).</summary>
        private Vector2? _placedTile;

        /// <summary>The tile the stash was last placed at this session (null if not yet placed).
        /// Used to position the planning shrine relative to the stash.</summary>
        public Vector2? LastPlacedTile => _placedTile;

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

            // 2026-05-29 round 9: sweep BEFORE ResolveTile, not after. The previous order made
            // ResolveTile see the old chest as a blocking object at the desired tile, falling
            // through to the next ladder candidate even when the desired tile was the only
            // problem — the keeper-save reload landed at (67, 18) instead of (67, 17) because
            // the old chest was still at (67, 17) when ResolveTile ran.
            var staleTiles = new List<Vector2>();
            foreach (var pair in farm.objects.Pairs)
            {
                if (pair.Value is Chest existing
                    && existing.modData.ContainsKey(StashModDataKey))
                {
                    staleTiles.Add(pair.Key);
                }
            }
            foreach (Vector2 staleTile in staleTiles)
            {
                farm.objects.Remove(staleTile);
                _monitor.Log(
                    $"JunimoStashService: removed stale stash chest at ({staleTile.X}, {staleTile.Y}).",
                    LogLevel.Trace);
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

            // Use a regular player chest (BC 130) for all behaviour (capacity, menu, persistence),
            // and render it with our recolored purple Junimo-chest sprite via StashDrawPatch. The
            // real Junimo Chest (BC 256) can't be tinted (it ignores playerChoiceColor — only the
            // colorable types (BC)130/232/BigChest/BigStoneChest honour it), so we duplicated its
            // sprite and recolored it purple offline (tools/extract_sprites.py -> junimo_stash.png).
            // The purple playerChoiceColor below is only a fallback tint if that PNG fails to load
            // (StashDrawPatch falls through to vanilla, which then draws a purple-tinted 130).
            //
            // GetActualCapacity is patched separately (JunimoStashCapacityPatch) to return the
            // current StashSlotCount so the ItemGrabMenu only shows the unlocked slot count.
            var chest = new Chest(playerChest: true, tile, itemId: "130");
            chest.playerChoiceColor.Value = new Microsoft.Xna.Framework.Color(150, 90, 200);
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

            // Configured tile (or first auto candidate) is blocked. Walk the auto candidate
            // ladder looking for a clear tile near the farmhouse before falling back.
            Point? entryPoint = TryGetFarmHouseEntry(farm);
            if (entryPoint.HasValue)
            {
                foreach (Vector2 candidate in AutoCandidates(entryPoint.Value))
                {
                    if (candidate == desired) continue;  // already tried
                    if (!IsTilePlaceable(farm, candidate)) continue;

                    string source = isAuto
                        ? $"first auto candidate ({desired.X}, {desired.Y}) blocked by " +
                          $"{DescribeBlocker(farm, desired)}"
                        : $"configured tile ({desired.X}, {desired.Y}) blocked by " +
                          $"{DescribeBlocker(farm, desired)}";
                    _monitor.Log(
                        $"JunimoStashService: {source}; using fallback tile ({candidate.X}, {candidate.Y}). " +
                        "Run tly_setstash to anchor a different tile.",
                        LogLevel.Info);
                    return candidate;
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

        /// <summary>Pick a tile three east + one south of the farmhouse entry. Path through
        /// the 2026-05-28 / 2026-05-29 playtests:
        ///   - (entry+2,+1)  : original — landed on the porch (blocked by Farmhouse building)
        ///   - (entry+2,+2)  : 2026-05-28 ladder fallback — "directly in front of the exit"
        ///   - (entry+4,+2)  : 2026-05-29 first retry — "in front of the mailbox" (which sits
        ///                     at (68, 16) on the Standard farm per Farm.cs:1483, so the
        ///                     chest at (68, 17) was the mail-reading tile)
        ///   - (entry+3,+2)  : 2026-05-29 second retry — "one space too low"
        ///   - (entry+3,+1)  : current — same column as before, one tile north. On Standard
        ///                     farm that's (67, 16): one tile west of the mailbox column,
        ///                     just clear of the porch's bottom edge. Returns Vector2.Zero
        ///                     if the entry is unavailable.</summary>
        private static Vector2 AutoTile(Farm farm)
        {
            Point? entry = TryGetFarmHouseEntry(farm);
            return entry.HasValue
                ? new Vector2(entry.Value.X + 3, entry.Value.Y + 1)
                : Vector2.Zero;
        }

        /// <summary>
        /// Ordered list of fallback candidate offsets relative to the farmhouse entry tile.
        /// The first candidate is the original "+2, +1" choice; subsequent entries are biased
        /// SOUTH and AWAY (-/+ X) of the entry so they clear the Farmhouse building footprint
        /// — the 2026-05-28 playtest showed (+2, +1) lands on the porch which intersects the
        /// Building.intersects rect. Tiles are tried in order; first one that <c>IsTilePlaceable</c>
        /// wins.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<Vector2> AutoCandidates(Point entry)
        {
            // (dx, dy) offsets — 2026-05-29 v3: lead with (+3, +1), fall through south then
            // east past the mailbox column then west. Avoids both the porch (dx <=2 at +1) and
            // the mailbox column (dx=4) on Standard farm.
            (int dx, int dy)[] offsets =
            {
                ( 3, 1),  // new default — west-of-mailbox, just clear of porch
                ( 3, 2),
                ( 3, 3),
                ( 5, 1),  // jump past the mailbox column
                ( 5, 2),
                ( 2, 3),  // SE, deeper south than the old default
                ( 0, 3),  // straight south, far enough to clear porch
                (-2, 3),
                (-3, 2),  // wider west fallback
                (-4, 2),  // widest west fallback
            };

            foreach (var (dx, dy) in offsets)
                yield return new Vector2(entry.X + dx, entry.Y + dy);
        }

        /// <summary>Safe wrapper around <c>Farm.GetMainFarmHouseEntry</c> — returns null if the
        /// farmhouse isn't resolvable yet (very early load, degenerate state).</summary>
        private static Point? TryGetFarmHouseEntry(Farm farm)
        {
            try { return farm.GetMainFarmHouseEntry(); }
            catch { return null; }
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

        /// <summary>Render the stash with our recolored purple Junimo-chest sprite. The chest is a
        /// plain BC 130 for behaviour; here we override only the world draw for the tagged chest,
        /// mirroring vanilla's big-craftable chest draw (16x32 sprite, 4x scale, top tile one row
        /// up). Falls through to vanilla if the sprite isn't loaded yet (then the 130 + purple
        /// playerChoiceColor fallback renders). Lid animation is intentionally skipped — the stash
        /// opens straight into its menu.</summary>
        [HarmonyLib.HarmonyPatch(typeof(Chest), nameof(Chest.draw),
            new System.Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) })]
        internal static class StashDrawPatch
        {
            private static bool Prefix(Chest __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
            {
                if (!__instance.modData.ContainsKey(StashModDataKey)) return true;
                if (_stashTexture == null) _stashTexture = _loadStashTexture?.Invoke();
                if (_stashTexture == null) return true;   // fall back to vanilla (purple-tinted 130)

                float layerDepth = System.Math.Max(0f, ((y + 1) * 64f - 24f) / 10000f) + x * 1E-05f;
                spriteBatch.Draw(
                    _stashTexture,
                    Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64f, (y - 1) * 64f)),
                    new Rectangle(0, 0, 16, 32),
                    Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, layerDepth);
                return false;
            }
        }
    }
}
