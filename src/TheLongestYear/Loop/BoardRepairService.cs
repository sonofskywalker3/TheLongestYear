using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Loop
{
    /// <summary>Swaps provably unreachable asks out of a board that was generated before the
    /// reachability rule existed (spec 2026-09-10-source-reachability, task 9).
    ///
    /// Runs at save load, not at reset: a player mid-year is holding an old board, and if the
    /// impossible ask is what blocks their season gate, waiting most of an in-game year for the
    /// next rewind is not a fix. A donated slot is NEVER touched, so nobody loses credit for
    /// something already handed in.
    ///
    /// Three things this class is careful about, each of which was a real defect in the plan it
    /// came from:
    ///
    /// 1. It never calls <see cref="BundleSlotFiller.Fill"/>. Fill rolls a WHOLE bundle, which
    ///    would re-roll donated slots, and a fabricated one-slot bundle would lose the recipe-part
    ///    identity of a composite bundle (Dye, Field Research). It calls
    ///    <see cref="BundleSlotFiller.ReplacementFor"/>, which picks one item for one slot from
    ///    the same pool and the same recipe part.
    ///
    /// 2. <c>NetWorldState.SetBundleData</c> does NOT refresh the Community Center's ingredient
    ///    cache, so the final write is followed by <c>cc.refreshBundlesIngredientsInfo()</c>.
    ///    That call is POLISH, not correctness, and the difference is worth stating because the
    ///    obvious guess is the wrong one. Decompile, StardewValley.Locations/CommunityCenter.cs:
    ///    <c>bundlesIngredientsInfo</c> is built by <c>refreshBundlesIngredientsInfo</c> (line 129)
    ///    and read in exactly two places, lines 709 and 719, both inside
    ///    <c>couldThisIngredienteBeUsedInABundle</c>. That method has exactly one caller in the
    ///    whole game: StardewValley.Menus/InventoryMenu.cs line 510, which sets
    ///    <c>GameMenu.bundleItemHovered</c>. It is the "a bundle wants this" hover glow, nothing
    ///    more. The DONATION path never consults the cache: <c>JunimoNoteMenu</c> builds its
    ///    <c>Bundle</c> objects straight from <c>Game1.netWorldState.Value.BundleData</c> every
    ///    time it opens (JunimoNoteMenu.cs lines 358, 1027, 1143). So skipping the refresh would
    ///    leave the hover glow pointing at the old, impossible item and ignoring the new one; it
    ///    would NOT refuse the donation.
    ///
    /// 3. <c>SaveLoaded</c> fires on multiplayer farmhands too, and mutating
    ///    <c>NetWorldState</c> from a peer races the host, so the whole repair is guarded by
    ///    <see cref="Context.IsMainPlayer"/>.
    ///
    /// Idempotent by construction: a replacement is drawn from the reachability-filtered pools and
    /// is re-checked against the same <see cref="SourceReachability"/> before it is written, so the
    /// next load finds nothing left to condemn and writes nothing at all.</summary>
    internal sealed class BoardRepairService
    {
        private const string MoneySlotId = "-1";

        /// <summary>The BundleData value's slash-delimited ingredient field
        /// (name / reward / INGREDIENTS / color / numberOfSlots / sprite / displayName). Only this
        /// one field is rewritten, so every other field of the live entry survives byte for byte:
        /// re-serializing through <see cref="BundleDataWriter"/> would rebuild fields this pass has
        /// no business deciding.</summary>
        private const int IngredientFieldIndex = 2;

        /// <summary>Per-bundle salt for the repair's rng, the same XOR + prime idiom
        /// <see cref="BundleEngine"/> uses for slot composition, so two saves on the same seed
        /// repair the same way and a repair never disturbs the generation stream.</summary>
        private const int RepairSaltPrime = 0x3D0F;

        /// <summary>How many times one slot may re-draw when the draw comes back unreachable. The
        /// pools are already reachability-filtered, but a Recipe part is widened with the bundle's
        /// OWN vanilla items (BundlePoolRecipes), which bypasses that filter, so a draw can still
        /// land on something condemned. A handful of retries clears that; anything beyond it is a
        /// pool with nothing reachable left, which is a "leave the slot alone".</summary>
        private const int MaxDrawAttempts = 8;

        private readonly IMonitor _monitor;
        private readonly SourceReachability _reachability;
        private readonly ItemPools _pools;
        private readonly BundleGenerationTuning _tuning;
        private readonly ItemAvailabilityModel _availability;
        private readonly int _seed;

        public BoardRepairService(
            IMonitor monitor, SourceReachability reachability, ItemPools pools,
            BundleGenerationTuning tuning, ItemAvailabilityModel availability, int seed)
        {
            _monitor = monitor;
            _reachability = reachability;
            _pools = pools;
            _tuning = tuning;
            _availability = availability;
            _seed = seed;
        }

        /// <summary>Replaces every unreachable, not-yet-donated ask on the live board. Returns the
        /// number of slots actually swapped: a slot nothing could replace is left exactly as it is
        /// and is NOT counted. Writes nothing, and refreshes nothing, when the board is clean.</summary>
        public int RepairIfNeeded()
        {
            // A farmhand must never write the shared world state (correction 3).
            if (!Context.IsMainPlayer) return 0;
            if (_reachability == null || _pools == null || _tuning == null) return 0;

            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return 0;

            // A snapshot: SetBundleData replaces the backing dictionary, so the live one must not
            // be enumerated while it is being written to.
            var board = new Dictionary<string, string>(worldState.BundleData, StringComparer.Ordinal);

            // Every concrete id the board asks for anywhere, so a repair cannot duplicate an ask
            // that already exists on another bundle (the board-wide no-repeat rule).
            var avoid = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in board)
            {
                ParsedBundle bundle = TryParse(entry.Key, entry.Value);
                if (bundle == null) continue;
                foreach (BundleIngredient ingredient in bundle.Ingredients)
                {
                    if (!IsConcrete(ingredient.ItemRef)) continue;
                    avoid.Add(BundleParsing.NormalizeItemId(ingredient.ItemRef));
                }
            }

            int repaired = 0;
            int unfixable = 0;
            int scanned = 0;
            int slotsRead = 0;
            foreach (KeyValuePair<string, string> entry in board)
            {
                ParsedBundle bundle = TryParse(entry.Key, entry.Value);
                if (bundle == null) continue;
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out _)) continue;

                scanned++;
                bool[] donated = DonatedFlags(worldState, bundle.Index);
                List<BundleIngredient> slots = bundle.Ingredients.ToList();
                bool changed = false;

                BundleSpec spec = null;
                DomainMatch match = null;
                PoolRecipe recipe = null;

                for (int i = 0; i < slots.Count; i++)
                {
                    BundleIngredient slot = slots[i];
                    if (!IsConcrete(slot.ItemRef)) continue;
                    if (donated != null && i < donated.Length && donated[i]) continue;

                    slotsRead++;
                    string oldId = BundleParsing.NormalizeItemId(slot.ItemRef);
                    if (!_reachability.IsUnreachable(oldId)) continue;

                    if (spec == null)
                    {
                        spec = SpecFor(bundle, slots);
                        match = PoolDomainClassifier.Classify(spec, _pools);
                        recipe = match.Domain == PoolDomain.Recipe
                            ? BundleSlotFiller.RecipeFor(spec, _pools, _availability)
                            : null;
                    }

                    PoolItem pick = Draw(spec, i, match, recipe, bundle.Index, avoid);
                    if (pick == null)
                    {
                        unfixable++;
                        _monitor?.Log(
                            $"Board repair: '{bundle.Name}' slot {i} asks for {oldId}, which this run cannot reach " +
                            $"({Reason(oldId)}), and its pool has nothing to put there. Leaving the ask alone.",
                            LogLevel.Warn);
                        continue;
                    }

                    int stack = LegendaryFishRules.ClampStack(pick.ItemId, slot.Stack);
                    int quality = LegendaryFishRules.ClampQuality(pick.ItemId, KeepQuality(pick.ItemId, slot.Quality));
                    slots[i] = new BundleIngredient(pick.ItemId, stack, quality);

                    // The old id can never be asked for again on this board; the new one must not
                    // be asked for twice.
                    avoid.Add(pick.ItemId);
                    changed = true;
                    repaired++;
                    _monitor?.Log(
                        $"Board repair: '{bundle.Name}' slot {i}: {oldId} ({Reason(oldId)}) -> " +
                        $"{pick.ItemId} x{stack} quality {quality}.",
                        LogLevel.Info);

                    // Rebuild the spec so a later slot of the same bundle sees what this one became
                    // (no duplicate inside one bundle).
                    spec = SpecFor(bundle, slots);
                }

                if (!changed) continue;
                Write(worldState, entry.Key, entry.Value, slots);
            }

            _monitor?.Log(
                $"Board repair: read {slotsRead} open slot(s) across {scanned} themed bundle(s); " +
                $"{repaired} swapped, {unfixable} unreplaceable.",
                LogLevel.Trace);

            if (repaired == 0)
            {
                if (unfixable > 0)
                    _monitor?.Log(
                        $"Board repair: {unfixable} unreachable ask(s) could not be replaced; the board was not changed.",
                        LogLevel.Warn);
                return 0;
            }

            // Keep the CC's ingredient cache in step with the board just written. Polish, not
            // correctness: that cache only drives the inventory hover glow, and the donation path
            // reads BundleData directly. See point 2 of the class doc for the decompile trail.
            RefreshIngredientCache();
            return repaired;
        }

        /// <summary>Lowers every ask above one for an item that never stacks (hats, weapons, Gil's
        /// trophy rings) on the live board, for boards the Stack size dial scaled before
        /// <see cref="UnstackableAsks"/> existed. Such a slot could never be deposited, so nothing
        /// donated is affected. Host only, like <see cref="RepairIfNeeded"/>. Returns the number of
        /// bundles rewritten (Nexus bug report 2026-09-14, Gil's Trophies Skeleton Mask x2).
        /// The stored copy of the written board (<see cref="MetaState.WrittenBoard"/>) gets the same
        /// rewrite, or the load-time manifest check would see the repaired live board as foreign.</summary>
        public static int ClampUnstackableAsks(IMonitor monitor, MetaState state)
        {
            if (!Context.IsMainPlayer) return 0;
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return 0;

            if (state?.WrittenBoard != null)
                foreach (string key in state.WrittenBoard.Keys.ToList())
                {
                    string storedRepaired = UnstackableAsks.RepairBundleValue(state.WrittenBoard[key]);
                    if (storedRepaired != null)
                        state.WrittenBoard[key] = storedRepaired;
                }

            var updates = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in worldState.BundleData)
            {
                string repaired = UnstackableAsks.RepairBundleValue(entry.Value);
                if (repaired == null) continue;
                updates[entry.Key] = repaired;
                monitor?.Log(
                    $"Board repair: '{entry.Key}' asked for more than one of an item that never stacks; lowered to one.",
                    LogLevel.Info);
            }
            if (updates.Count == 0) return 0;

            worldState.SetBundleData(updates);
            (Game1.getLocationFromName("CommunityCenter") as CommunityCenter)?.refreshBundlesIngredientsInfo();
            return updates.Count;
        }

        /// <summary>One replacement for one slot, re-drawing when the draw is itself unreachable
        /// (a Recipe part is widened with the bundle's own vanilla items, which never went through
        /// the pool filter). Null means leave the slot alone.</summary>
        private PoolItem Draw(
            BundleSpec spec, int slotIndex, DomainMatch match, PoolRecipe recipe,
            int bundleIndex, HashSet<string> avoid)
        {
            var rejected = new HashSet<string>(avoid, StringComparer.Ordinal);
            for (int attempt = 0; attempt < MaxDrawAttempts; attempt++)
            {
                var rng = new Random(unchecked(_seed ^ (bundleIndex * RepairSaltPrime) ^ (slotIndex + attempt)));
                PoolItem pick = BundleSlotFiller.ReplacementFor(
                    spec, slotIndex, match, _pools, _tuning, rng, rejected, _availability, recipe,
                    // Only the first attempt reports: the retries re-draw from the same recipe and
                    // would repeat the same line up to MaxDrawAttempts times.
                    attempt == 0 ? note => _monitor?.Log($"Board repair: {note}", LogLevel.Trace) : null);
                if (pick == null) return null;
                if (!_reachability.IsUnreachable(pick.ItemId)) return pick;
                rejected.Add(pick.ItemId);
            }
            return null;
        }

        /// <summary>The old slot's quality, kept only when the replacement can actually carry a
        /// star. An impossible quality ask is exactly the kind of slot this pass exists to remove
        /// (Nexus 1122358), so anything the game never stars falls back to 0.</summary>
        private int KeepQuality(string itemId, int quality)
        {
            if (quality <= 0) return 0;
            if (BundleSlotFiller.BuiltInQualityIneligibleItemIds.Contains(itemId)) return 0;
            if (_tuning.QualityIneligibleItemIds != null && _tuning.QualityIneligibleItemIds.Contains(itemId)) return 0;
            if (_pools.QualityEligibleIds != null && !_pools.QualityEligibleIds.Contains(itemId)) return 0;
            return quality;
        }

        /// <summary>Rewrites ONE key, ingredient field only, through the same merge-and-upsert call
        /// the engine uses (<c>SetBundleData</c> -> <c>NetDictionary.CopyFrom</c>, which upserts
        /// and never removes). The slot COUNT is unchanged, so the completion NetArray for this
        /// bundle is left exactly as it was (decompile: SetBundleData only ever grows it).</summary>
        private void Write(
            StardewValley.Network.NetWorldState worldState, string key, string value,
            IReadOnlyList<BundleIngredient> slots)
        {
            string[] fields = value.Split('/');
            if (fields.Length <= IngredientFieldIndex)
            {
                _monitor?.Log(
                    $"Board repair: '{key}' has only {fields.Length} field(s) and no ingredient field; skipping it.",
                    LogLevel.Warn);
                return;
            }
            fields[IngredientFieldIndex] = string.Join(" ",
                slots.Select(s => $"{s.ItemRef} {s.Stack} {s.Quality}"));
            worldState.SetBundleData(new Dictionary<string, string> { [key] = string.Join("/", fields) });
        }

        /// <summary>The CC's inventory-hover cache, rebuilt from the board that was just written.
        /// Verified name: <c>CommunityCenter.refreshBundlesIngredientsInfo</c> (public, decompile
        /// StardewValley.Locations/CommunityCenter.cs line 129). Not on the donation path: see
        /// point 2 of the class doc.</summary>
        private void RefreshIngredientCache()
        {
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc == null)
            {
                _monitor?.Log(
                    "Board repair: the Community Center is not loaded, so its ingredient cache was not refreshed. " +
                    "It rebuilds when the location is constructed.",
                    LogLevel.Trace);
                return;
            }
            cc.refreshBundlesIngredientsInfo();
        }

        private static bool[] DonatedFlags(StardewValley.Network.NetWorldState worldState, int index)
            => worldState.Bundles?.FieldDict != null && worldState.Bundles.FieldDict.ContainsKey(index)
                ? worldState.Bundles[index]
                : null;

        /// <summary>Money slots and category refs ("any egg") are not items and are never repaired.</summary>
        private static bool IsConcrete(string itemRef)
            => !string.IsNullOrEmpty(itemRef) && itemRef != MoneySlotId && !BundleParsing.IsCategoryRef(itemRef);

        /// <summary>A spec the Core classifier and filler can read. Only Name and Slots matter to
        /// them; the rest carries the parsed entry's own values so nothing downstream is invented.</summary>
        private static BundleSpec SpecFor(ParsedBundle bundle, IReadOnlyList<BundleIngredient> slots)
            => new BundleSpec(
                bundle.Room, bundle.Index, bundle.Name, bundle.Name, "", 0, bundle.NumberOfSlots,
                slots.Select(s => new BundleSlotSpec(s.ItemRef, s.Stack, s.Quality)).ToList());

        private string Reason(string itemId)
            => _reachability.Reasons.TryGetValue(itemId, out string why) && !string.IsNullOrEmpty(why)
                ? why
                : "no reachable source";

        private ParsedBundle TryParse(string key, string value)
        {
            try
            {
                return BundleParsing.Parse(key, value);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or IndexOutOfRangeException)
            {
                _monitor?.Log($"Board repair: could not read bundle '{key}' ({ex.GetType().Name}); skipping it.", LogLevel.Warn);
                return null;
            }
        }
    }
}
