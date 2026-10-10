using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class BundleEngine
    {
        /// <summary>Draws one bundle per room-position (Vault unmodified) and returns the
        /// generated set. Deterministic for a given seed (see <see cref="BundleEngineSeed"/>).</summary>
        /// <param name="randomRewards">Randomizer "Random bundle rewards" for THIS board. Every
        /// caller passes <c>MetaState.RandomBundleRewardsBoard</c>, never live config, so a reload
        /// re-derives the same rewards the reset wrote.</param>
        public GeneratedBundleSet Generate(int seed, bool randomRewards)
        {
            _lastSeed = seed;
            _lastDomains.Clear();
            _lastRecipes.Clear();
            _lastVanillaOnlyRecipes.Clear();
            ItemPools itemPools = new GameDataPools(_monitor).Build(_tuning, _extraExcludedIds, VanillaFilter);
            // Item-rarity modifier (spec 2026-08-26): bias the pool weights the sampler already
            // reads, rather than teaching the sampler about difficulty. A bias of 1.0 returns the
            // same instance, so the default path is untouched.
            itemPools = Core.RarityBias.Apply(itemPools, _difficulty.RarityBias, _thresholds);
            LastDerivedSeasonPins = itemPools.DerivedSeasonPins;

            // Board-level legendary allowance (LegendaryFishRules.BoardAllowance): how many
            // legendaries this whole board may hold at this step. Rolled off its own salt so it
            // cannot move any other stream; decided before the authored bundles compose so a
            // board that gets none never composes a Weatherman's with a Mutant Carp in it.
            int legendaryAllowance = Core.LegendaryFishRules.BoardAllowance(
                Availability?.Step ?? Core.DifficultyStep.Normal, new Random(seed ^ LegendarySalt));
            _monitor?.Log($"BundleEngine: legendary allowance for this board: {(legendaryAllowance == int.MaxValue ? "open" : legendaryAllowance.ToString())}.", LogLevel.Trace);
            // Prismatic Shard / Mystery Box allowance (CappedAsks.BoardAllowance): fixed by the
            // stamped Stack size step, no roll, so it moves no stream.
            int cappedAllowance = Core.CappedAsks.BoardAllowance(_difficulty.Steps?.StackSize ?? Core.DifficultyStep.Normal);
            _monitor?.Log($"BundleEngine: Prismatic Shard / Mystery Box allowance for this board: {cappedAllowance} each.", LogLevel.Trace);
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomPools = VanillaRoomPools();
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools =
                WidenWithAuthoredBundles(roomPools, itemPools, seed, legendaryAllowance, cappedAllowance);

            var allPicks = new List<BundleSpec>();
            // "bundleIndex:slotIndex" -> the input a flavored slot names. Persisted beside the
            // board, never written into the bundle string (GeneratedBundleSet.Flavors).
            var flavors = new Dictionary<string, string>();
            var usedNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            // Absolute index -> the (room, name) that already claimed it, for the defensive
            // duplicate-index check below (see class doc: every candidate already carries
            // vanilla's own globally-unique absolute index, so a collision here should be
            // structurally impossible with vanilla data).
            var claimedIndices = new Dictionary<int, (string Room, string Name)>();

            // The Vault and the Abandoned Joja Mart pass through UNMODIFIED (single-candidate
            // positions, real indices kept). Candidate 0 is vanilla's own Data/Bundles entry --
            // BuildRoomPools adds the standard set first and only ever appends widening candidates
            // after it -- so this writes back exactly what the game authored. The Vault's amounts
            // are the one thing scaled, by its own difficulty multiplier.
            // The bundle-count dial reshapes the Vault too (amendment of 2026-10-09): Easy drops its
            // priciest bundle here; Hard and Extreme add extras after pass 1, once the themed rooms
            // have taken their reserved indices, so those indices never move.
            int vaultDelta = _difficulty.BundleCount?.VaultDelta ?? 0;
            var vaultKept = new List<BundleSpec>();
            int vaultInsertAt = -1;
            foreach (string room in PassThroughRooms)
            {
                if (!pools.TryGetValue(room, out IReadOnlyList<IReadOnlyList<BundleSpec>> positions))
                    continue;

                List<BundleSpec> specs = positions
                    .Where(candidates => candidates.Count > 0) // an empty one is already WARN-logged by BuildRoomPools
                    .Select(candidates => room == VaultRoomName
                        ? VaultAmountScaler.Scale(candidates[0], _tuning.VaultAmountMultiplier)
                        : candidates[0])
                    .ToList();
                if (room == VaultRoomName && vaultDelta < 0)
                {
                    IReadOnlyList<BundleSpec> kept = Core.VaultBundleCount.Kept(specs, vaultDelta);
                    _monitor?.Log(
                        $"BundleEngine: bundle count for {VaultRoomName}: {specs.Count} -> {kept.Count} (drops {string.Join(", ", specs.Except(kept).Select(s => s.Name))}).",
                        LogLevel.Info);
                    specs = kept.ToList();
                }
                foreach (BundleSpec spec in specs)
                {
                    if (!TryClaimIndex(spec, claimedIndices))
                        continue;
                    BundleSpec added = Uniquify(spec, usedNameCounts);
                    allPicks.Add(added);
                    if (room == VaultRoomName)
                        vaultKept.Add(added);
                }
                if (room == VaultRoomName)
                    vaultInsertAt = allPicks.Count;
            }

            // Pass 1: pick and classify. Deterministic room order (ordinal by name) rather than
            // the dictionary's own enumeration order -- Dictionary<TKey,TValue> enumeration order
            // is an implementation detail, not a contract, so relying on it would make the
            // fixed-key-space guarantee below fragile across process launches/.NET versions even
            // though the seed is the same. Picks whose items are already final (authored, or a
            // domain the engine does not re-roll) seed the board-wide "asked" set here.
            var picked = new List<PickRecord>();
            var asked = new HashSet<string>(StringComparer.Ordinal);
            // Bundle-count dial (spec 2026-10-09-bundle-count-dial): extra bundles take indices
            // from a reserved range, skipping every index any room pool already uses. Rooms are
            // walked in ordinal order below, so the same seed and pools give the same indices.
            var reservedIndices = new Core.ReservedBundleIndices(
                pools.Values.SelectMany(positions => positions).SelectMany(candidates => candidates).Select(c => c.Index));
            foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomEntry
                     in pools.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (PassThroughRooms.Contains(roomEntry.Key))
                    continue; // already emitted above, unmodified

                IReadOnlyList<IReadOnlyList<BundleSpec>> positions = cappedAllowance == 0
                    ? WithoutHelpers(roomEntry.Value)
                    : roomEntry.Value;
                IReadOnlyList<BundleSpec> picks = RemixSelector.PickForRoom(positions, seed, roomEntry.Key);
                picks = ApplyBundleCount(roomEntry.Key, picks, positions, seed, reservedIndices);
                foreach (BundleSpec pick in picks)
                {
                    if (!TryClaimIndex(pick, claimedIndices))
                        continue;

                    if (AuthoredBundleNames.Contains(pick.Name))
                    {
                        // Authored slots (Plan-3) are composed ONCE per def by
                        // AuthoredBundleComposer (see WidenWithAuthoredBundles) and are FINAL --
                        // the composer already made deliberate stack-1/quality-0 choices for
                        // every slot (e.g. Weatherman's = all-fish, Preserver's = all-artisan).
                        // Those authored picks clear PoolDomainClassifier's 2/3 majority just as
                        // easily as a coincidentally-themed vanilla pick, so running them through
                        // the classify/fill/trim chain would silently RE-ROLL an authored
                        // bundle's already-final slots and make them position-dependent (final-
                        // review finding). Skip the chain entirely for authored picks.
                        picked.Add(new PickRecord(pick, new DomainMatch(PoolDomain.None, null), pick));
                        AddAskedItems(asked, pick);
                        continue;
                    }

                    DomainMatch match = PoolDomainClassifier.Classify(pick, itemPools);
                    if (match.Domain == PoolDomain.None)
                    {
                        // Since spec 2026-08-28-obtainable-board-3-pools only a money bundle (or an
                        // empty one) can land here: everything else falls through the classifier to
                        // its recipe. A non-money bundle keeping its vanilla slots is a bug in the
                        // classifier, so say so rather than letting it pass quietly.
                        if (!IsMoneyBundle(pick))
                            _monitor?.Log(
                                $"BundleEngine: '{pick.Room}/{pick.Name}' classified None but is not a money bundle, " +
                                "keeping vanilla slots (unexpected: every non-money bundle should roll from a recipe).",
                                LogLevel.Warn);
                        // Kept vanilla slots: the same per-pick rng stream the filler would have
                        // received, of which a None-domain fill consumes nothing.
                        BundleSpec trimmed = SlotTrimmer.Trim(pick, new Random(seed ^ (pick.Index * SlotSaltPrime)));
                        picked.Add(new PickRecord(pick, match, trimmed));
                        AddAskedItems(asked, trimmed);
                        continue;
                    }
                    // The recipe is built ONCE per bundle here and carried on the record: the fill
                    // order below, the diagnostics line and the fill itself all read this one
                    // instance, instead of each rebuilding it (final review, 2026-08-29).
                    Core.PoolRecipe recipe = match.Domain == PoolDomain.Recipe
                        ? BundleSlotFiller.RecipeFor(pick, itemPools, Availability)
                        : null;
                    picked.Add(new PickRecord(pick, match, null) { Recipe = recipe });
                }
            }

            // Vault extras (Hard +1, Extreme +2): priced off the board's own priciest Vault bundle,
            // indexed from the reserved range after every themed room's extras. Inserted right
            // after the Vault's own bundles so the board reads Vault, Joja, themed rooms as before.
            if (vaultDelta > 0 && vaultInsertAt >= 0)
            {
                IReadOnlyList<BundleSpec> extras = Core.VaultBundleCount.Extras(vaultKept, vaultDelta, reservedIndices.Next);
                var placed = new List<BundleSpec>();
                foreach (BundleSpec extra in extras)
                    if (TryClaimIndex(extra, claimedIndices))
                        placed.Add(Uniquify(extra, usedNameCounts));
                allPicks.InsertRange(vaultInsertAt, placed);
                _monitor?.Log(
                    $"BundleEngine: bundle count for {VaultRoomName}: {vaultKept.Count} -> {vaultKept.Count + placed.Count} (adds {string.Join(", ", placed.Select(s => $"{s.Index}:{s.Name}"))}).",
                    LogLevel.Info);
            }

            // Pass 2: re-roll, tightest pool first (2026-08-28, no item asked twice across the
            // board). Each fill leaves out everything already asked and adds its own picks, so a
            // bundle with few candidates (Night Fishing) is not the one left holding the repeat
            // fallback because a roomy bundle drew its fish first. Per-pick rng streams are
            // salted on the absolute index, so the fill order does not change them.
            foreach (PickRecord record in picked
                         .Where(r => r.Composed == null)
                         .OrderBy(r => BundleSlotFiller.CandidateCount(r.Pick, r.Match, itemPools, Availability, r.Recipe))
                         .ThenBy(r => r.Pick.Index))
            {
                BundleSpec pick = record.Pick;
                if (record.Recipe != null)
                {
                    Core.PoolRecipe recipe = record.Recipe;
                    _lastRecipes[pick.Index] =
                        $"{recipe.Name} ({string.Join(" + ", recipe.Parts.Select(p => p.Label))})";
                    if (recipe.IsVanillaOnly)
                        _lastVanillaOnlyRecipes.Add(pick.Name);
                }
                var slotRng = new Random(seed ^ (pick.Index * SlotSaltPrime));
                int legendariesSoFar = picked.Where(r => r.Composed != null).Sum(r => r.Composed.Slots.Count(sl => Core.LegendaryFishRules.IsLegendary(sl.ItemId)));
                int legendaryBudget = legendaryAllowance == int.MaxValue ? int.MaxValue : legendaryAllowance - legendariesSoFar;
                IReadOnlySet<string> banned = legendaryBudget <= 0 ? Core.LegendaryFishRules.Ids : null;
                IReadOnlyDictionary<string, int> cappedBudget = CappedBudget(picked, record, cappedAllowance);
                List<string> cappedOut = cappedBudget.Where(kv => kv.Value <= 0).Select(kv => kv.Key)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList();
                if (cappedOut.Count > 0)
                {
                    var withCapped = new HashSet<string>(cappedOut, StringComparer.Ordinal);
                    if (banned != null) withCapped.UnionWith(banned);
                    banned = withCapped;
                }
                BundleSpec composed = BundleSlotFiller.Fill(pick, record.Match, itemPools, _tuning, slotRng,
                    msg => _monitor?.Log("BundleEngine: " + msg, FillerLogLevel(msg)), asked, Availability, record.Recipe, banned, legendaryBudget,
                    cappedBudget);
                if (ReferenceEquals(composed, pick))
                {
                    _monitor?.Log(
                        $"BundleEngine: '{pick.Room}/{pick.Name}' matched domain {record.Match.Domain} but its " +
                        "filtered pool couldn't fill every slot — keeping vanilla slots.",
                        LogLevel.Trace);
                    composed = SlotTrimmer.Trim(pick, slotRng);
                }
                AddAskedItems(asked, composed);
                record.Composed = composed;
            }

            // Pass 3: emit in the original room/position order (name uniquification and the
            // fixed write-key space depend on it).
            foreach (PickRecord record in picked)
            {
                _lastDomains[record.Pick.Index] = record.Match; // for diagnostics (see below)
                // Required-slots modifier: adjust the pick-X count only. Applied after
                // composition so it sees the FINAL shown-slot count (SlotTrimmer and the
                // filler can both shrink it), and never to the Vault, which RequiredSlots
                // skips on its own.
                // Stack-size modifier: applied to the FINISHED slots so it reaches bundles the
                // engine kept verbatim from vanilla, not just the ones it re-rolled. Before
                // this it only scaled re-rolled bundles and missed most of the board.
                // Fish and forage asks: basis x band by step (QuantityAskPass), read against the
                // deadline the classifier will give each slot. Before StackScaling, which skips
                // banded slots.
                // Required slots FIRST (Codex review, 2026-09-04): Extreme turns a pick-3-of-4 into
                // 4-of-4, which changes the deadline the classifier will give each slot, and the
                // quantity pass must read that final shape, not the pre-dial one.
                BundleSpec finished = Core.RequiredSlots.Apply(record.Composed!, _difficulty);
                var fishRng = new Random(seed ^ (finished.Index * SlotSaltPrime) ^ FishAskSalt);
                BundleSpec composed = Core.QuantityAskPass.Apply(finished, _difficulty,
                    id => BundleSlotFiller.DeadlineFor(finished, record.Match, finished.Slots, id, Availability), fishRng,
                    out IReadOnlySet<int> bandedSlots, Availability);
                composed = Core.StackScaling.Apply(composed, _difficulty, bandedSlots);
                // Name the fruit, mushroom or fish on any flavored slot, and re-roll that slot's
                // stack against what the named input actually yields. AFTER StackScaling: the
                // quantity pass banded these ids off the machine's 35-a-week throughput, which is
                // the wrong basis once one fruit is named (plan 2026-09-21-flavored-bundle-slots).
                composed = Core.FlavoredSlotPass.Apply(
                    composed, seed, _difficulty, itemPools,
                    id => Availability?.IsPlaced(id) == true ? Availability.For(id).Week : (int?)null,
                    id => BundleSlotFiller.DeadlineFor(composed, record.Match, composed.Slots, id, Availability),
                    out IReadOnlyDictionary<int, string> slotFlavors, Availability);
                foreach (KeyValuePair<int, string> flavor in slotFlavors)
                    flavors[Core.FlavoredSlotPass.KeyFor(composed.Index, flavor.Key)] = flavor.Value;
                // A capped item asks for one, whatever the passes above made of its stack
                // (vanilla's "5 Mystery Box" on a kept Helper's, the stack dial, the quantity pass).
                composed = Core.CappedAsks.ClampBundle(composed);
                allPicks.Add(Uniquify(composed, usedNameCounts));
            }

            // Guard, not the rule: the fill budgets above are what hold the allowance.
            List<BundleSpec> themed = allPicks.Where(b => !CappedCountSkipsRoom(b.Room)).ToList();
            foreach (string id in Core.CappedAsks.Ids.OrderBy(id => id, StringComparer.Ordinal))
            {
                int onBoard = Core.CappedAsks.CountOnBoard(id, themed);
                if (onBoard > cappedAllowance)
                    _monitor?.Log(
                        $"BundleEngine: board asks for {id} x{onBoard}, over its allowance of {cappedAllowance} (seed {seed}).",
                        LogLevel.Error);
            }

            if (!randomRewards)
                return new GeneratedBundleSet(allPicks, flavors);

            // Last, so it moves no other stream: rewards never feed back into what a bundle asks.
            IReadOnlyList<string> rewardPool = RewardPool(roomPools, VanillaFilter);
            IReadOnlyList<BundleSpec> rewarded = Core.BundleRewardShuffle.Apply(allPicks, seed, rewardPool, IsRewardShuffleSkippedRoom);
            _monitor?.Log(
                $"Randomizer: bundle rewards shuffled ({rewarded.Count(b => !IsRewardShuffleSkippedRoom(b.Room))} bundles, pool {rewardPool.Count}).",
                LogLevel.Info);
            return new GeneratedBundleSet(rewarded, flavors);
        }

        /// <summary>The bundle-count dial for one room: drops or adds bundles to reach the room's
        /// target count (<see cref="Core.BundleCountRule"/>, <see cref="Core.RoomBundleCountPlanner"/>).
        /// A profile stamped before the dial existed has no rule and keeps every pick. Rolls from
        /// its own per-room stream, so the picks and every other stream stay where they were.</summary>
        private IReadOnlyList<BundleSpec> ApplyBundleCount(
            string room, IReadOnlyList<BundleSpec> picks, IReadOnlyList<IReadOnlyList<BundleSpec>> positions,
            int seed, Core.ReservedBundleIndices reservedIndices)
        {
            if (_difficulty.BundleCount is not Core.BundleCountRule rule)
                return picks;

            var countRng = new Random(seed ^ BundleCountSalt ^ unchecked(StableAuthoredSalt(room) * BundleCountSaltPrime));
            int target = rule.Target(picks.Count, countRng);
            if (target == picks.Count)
                return picks;

            IReadOnlyList<BundleSpec> planned = Core.RoomBundleCountPlanner.Plan(
                picks, positions, target, countRng, reservedIndices.Next, out int shortfall);
            _monitor?.Log(
                $"BundleEngine: bundle count for {room}: {picks.Count} -> {planned.Count} (target {target}).",
                LogLevel.Info);
            if (shortfall > 0)
                _monitor?.Log(
                    $"BundleEngine: {room} only has {planned.Count} different bundles to offer, {shortfall} short of the {target} the bundle-count dial asks for.",
                    LogLevel.Info);
            return planned;
        }
    }
}
