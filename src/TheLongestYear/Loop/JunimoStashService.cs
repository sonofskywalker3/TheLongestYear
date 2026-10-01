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
    internal sealed partial class JunimoStashService
    {
        internal const string StashModDataKey = "tly.junimo.stash";

        /// <summary>True when Better Chests or Unlimited Storage is installed. Set once at game
        /// launch from <c>Helper.ModRegistry.IsLoaded</c>. Those two transpile the ItemGrabMenu
        /// constructor and read its <c>context</c>, so the stash menu is opened with a null context
        /// only while one of them is present. Chests Anywhere identifies the open chest from that
        /// same context, so nulling it unconditionally broke naming and chest switching there
        /// (two Nexus reports, 2026-09-14).</summary>
        public static bool StorageOverhaulLoaded;

        public static readonly string[] StorageOverhaulModIds =
            { "furyx639.BetterChests", "furyx639.UnlimitedStorage" };

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
            StashItemCodec.Monitor = monitor;
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
            var carried = new Dictionary<string, string>();
            foreach (var pair in farm.objects.Pairs)
            {
                if (pair.Value is Chest existing
                    && existing.modData.ContainsKey(StashModDataKey))
                {
                    staleTiles.Add(pair.Key);
                    CollectCarriedModData(existing, carried);
                    RescueLegacyRecords(existing);
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
            foreach (var kv in carried)
                chest.modData[kv.Key] = kv.Value;
            StampBetterChestsOptOut(chest);
            StampChestsAnywhereOptOutIfNeeded(chest);
            farm.objects[tile] = chest;
            _placedTile = tile;
            GuardNesting(chest);

            _monitor.Log(
                $"JunimoStashService: placed stash chest at ({tile.X}, {tile.Y}), " +
                $"cap={_meta.StashSlotCount} slots.",
                LogLevel.Info);
        }

        // Better Chests (furyx639.BetterChests) reads per-chest feature overrides from these
        // modData keys — the same ones its own "configure chest" UI writes. The stash is a
        // fixed-purpose few-slot chest: without the opt-out, BC resizes its MENU to a 70-slot
        // grid via an ItemGrabMenu transpiler (GetMenuCapacity reads BC's ResizeChest OPTION,
        // not Chest.GetActualCapacity, so the 0.11.3 capacity-postfix fix could never reach it
        // — VeggieGirl43's report), can bulk-stash arbitrary items into it, auto-organize it,
        // or let the player carry it off its anchor tile. Fix-our-mod rule: opt THIS chest out
        // through BC's supported per-chest surface; every other chest is left to BC.
        // Harmless without BC installed — just inert modData alongside our own tag.
        private const string BetterChestsOptionPrefix = "furyx639.BetterChests/";
        private const string BetterChestsDisabledValue = "Disabled";
        private static readonly string[] BetterChestsDisabledOptions =
            { "ResizeChest", "StashToChest", "AutoOrganize", "CarryChest" };

        private static void StampBetterChestsOptOut(Chest chest)
        {
            foreach (string option in BetterChestsDisabledOptions)
                chest.modData[BetterChestsOptionPrefix + option] = BetterChestsDisabledValue;
        }

        // Chests Anywhere (Pathoschild.ChestsAnywhere) keeps a chest's name, category, sort order
        // and hidden flag in these modData keys, written by its own Edit form. The stash chest is
        // rebuilt on every save load and every loop reset, so without carrying them over a name
        // the player gave it would vanish by the next morning.
        internal const string ChestsAnywhereModDataPrefix = "Pathoschild.ChestsAnywhere/";
        private const string ChestsAnywhereIgnoredKey = ChestsAnywhereModDataPrefix + "IsIgnored";

        private static void CollectCarriedModData(Chest from, Dictionary<string, string> into)
        {
            foreach (var pair in from.modData.Pairs)
            {
                if (pair.Key.StartsWith(ChestsAnywhereModDataPrefix, System.StringComparison.Ordinal))
                    into[pair.Key] = pair.Value;
            }
        }

        // With a storage-overhaul mod present the stash menu opens with a null context (see
        // JunimoStashMenuContextPatch), which Chests Anywhere cannot attach to: a player who
        // reached the stash through its list would be stuck there. Hide the stash from its lists
        // in that case; without an overhaul mod the stash is a first-class Chests Anywhere chest.
        private static void StampChestsAnywhereOptOutIfNeeded(Chest chest)
        {
            if (StorageOverhaulLoaded)
                chest.modData[ChestsAnywhereIgnoredKey] = "true";
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
            var overflow = new List<Item>();
            int ejectedCount = 0;
            var unknown = new List<StashItemRecord>();
            int total = _meta.StashItems.Count;
            for (int index = 0; index < _meta.StashItems.Count; index++)
            {
                // Saves from 0.18.118 or earlier can hold a container with non-cosmetic contents
                // (the deposit check is new). Never delete: keep the cosmetic contents nested and
                // take everything else out as its own stash entry, or onto the ground beside the stash if full.
                var ejected = new List<StashItemRecord>();
                StashItemRecord record = StashNesting.Trim(_meta.StashItems[index], ejected);

                // Ejected items first, so they survive even when the container's own id is unknown.
                foreach (StashItemRecord e in ejected)
                {
                    if (StashItemCodec.CreateFromRecord(e, _monitor, overflow) is Item loose)
                    {
                        overflow.Add(loose);
                        ejectedCount++;
                    }
                    else
                        _monitor.Log($"JunimoStashService: could not recreate ejected item '{e.ItemId}' from '{record.ItemId}' (unknown id).", LogLevel.Warn);
                }

                Item item = StashItemCodec.CreateFromRecord(record, _monitor, overflow);
                if (item == null)
                {
                    _monitor.Log(
                        $"JunimoStashService: could not recreate item '{record.ItemId}' (unknown id), skipping.",
                        LogLevel.Warn);
                    // Its nested items were handed out above (CreateFromRecord puts them in overflow).
                    unknown.Add(record with { Contents = null, HeldObject = null });
                    continue;
                }
                chest.Items.Add(item);
                restored++;
            }
            if (overflow.Count > 0)
            {
                foreach (Item extra in overflow)
                    DepositOrDropBesideStash(extra, "the stash was full when an old stashed container was emptied");
                // The extras now live on their own (in the stash or on the ground). Write the
                // trimmed containers and the deposited extras back, so a second populate (tly_setstash,
                // save load) neither ejects them again nor loses the ones already in the stash.
                // Unknown-id records stay banked, as they were before.
                BankToMeta();
                _meta.StashItems.AddRange(unknown);
            }
            if (ejectedCount > 0)
                _monitor.Log($"JunimoStashService: took {ejectedCount} non-cosmetic item(s) out of stashed containers.", LogLevel.Info);

            _monitor.Log(
                $"JunimoStashService: restored {restored}/{total} items into stash chest.",
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
                _meta.StashItems.Add(StashItemCodec.ToRecord(item));
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
                GuardNesting(chest);
                return chest;
            }

            // Belt-and-braces: scan the farm for the tagged chest in case the cache is stale
            // (e.g. someone called FindStashChest without PlaceChest first this session).
            foreach (var pair in farm.objects.Pairs)
            {
                if (pair.Value is Chest c && c.modData.ContainsKey(StashModDataKey))
                {
                    _placedTile = pair.Key;
                    GuardNesting(c);
                    return c;
                }
            }

            return null;
        }

        /// <summary>Render the stash with our recolored purple Junimo-chest sprite. The chest is a
        /// plain BC 130 for behaviour; here we override only the world draw for the tagged chest,
        /// mirroring vanilla's big-craftable chest draw (16x32 sprite, 4x scale, top tile one row
        /// up). junimo_stash.png is a horizontal strip of the chest's lid-animation frames; we pick
        /// the column matching the chest's live <c>currentLidFrame</c> so it opens when the farmer
        /// is near / accesses it and closes when they leave, exactly like the real Junimo Chest.
        /// Falls through to vanilla if the sprite isn't loaded yet (then the 130 + purple
        /// playerChoiceColor fallback renders).</summary>
        [HarmonyLib.HarmonyPatch(typeof(Chest), nameof(Chest.draw),
            new System.Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) })]
        internal static class StashDrawPatch
        {
            // currentLidFrame is private on Chest; read it by ref to drive the open/close animation.
            private static readonly HarmonyLib.AccessTools.FieldRef<Chest, int> _lidFrame =
                HarmonyLib.AccessTools.FieldRefAccess<Chest, int>("currentLidFrame");

            private static bool Prefix(Chest __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
            {
                if (!__instance.modData.ContainsKey(StashModDataKey)) return true;
                if (_stashTexture == null) _stashTexture = _loadStashTexture?.Invoke();
                if (_stashTexture == null) return true;   // fall back to vanilla (purple-tinted 130)

                int frames = System.Math.Max(1, _stashTexture.Width / 16);
                int col = System.Math.Clamp(_lidFrame(__instance) - __instance.startingLidFrame.Value, 0, frames - 1);
                float layerDepth = System.Math.Max(0f, ((y + 1) * 64f - 24f) / 10000f) + x * 1E-05f;
                spriteBatch.Draw(
                    _stashTexture,
                    Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64f, (y - 1) * 64f)),
                    new Rectangle(col * 16, 0, 16, 32),
                    Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, layerDepth);
                return false;
            }
        }
    }
}
