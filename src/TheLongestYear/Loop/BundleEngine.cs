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
    /// <summary>
    /// Orchestrates a full owned-bundle generation + write. <see cref="Generate"/> draws one
    /// candidate per room-position from <see cref="VanillaBundlePool.BuildRoomPools"/> via
    /// <see cref="RemixSelector"/>, re-rolls each pick's slots (no item asked twice across the
    /// board: fills run tightest pool first and each leaves out what earlier ones asked);
    /// <see cref="WriteToWorld"/> commits the result into
    /// <c>Game1.netWorldState</c> and re-syncs the Community Center location.
    ///
    /// RunActivation gating is NOT done here — this class only builds/writes bundle data given a
    /// caller-supplied seed; the caller (run creation / <see cref="WorldResetService"/>'s reset
    /// sequence) is responsible for only invoking it inside an active TLY run (see MEMORY
    /// tly-dormant-per-save-gate-runactivation).
    ///
    /// <see cref="WriteToWorld"/> must be called on the SAME instance right after
    /// <see cref="Generate"/> (it logs the seed <see cref="Generate"/> was called with) — this
    /// mirrors how every other glue service here is used (one construct-per-call-site, see
    /// <see cref="CommunityCenterUnlock"/>/<see cref="WeeklyThemeQuestService"/>), and keeps
    /// WriteToWorld's signature exactly as specced (no seed parameter) while still producing an
    /// accurate log line.
    ///
    /// Global-index note (decompile-verified, <c>NetWorldState.SetBundleData</c>,
    /// StardewValley.Network/NetWorldState.cs): the underlying <c>Bundles</c>/<c>BundleRewards</c>
    /// NetIntDictionary-ies are keyed PURELY on the numeric index parsed out of the "Room/index"
    /// key -- NOT on the (room, index) pair. Two different rooms writing the same index would
    /// silently share one completion NetArray. Earlier revisions of this method re-numbered every
    /// non-Vault room's picks onto a synthetic global 0..N sequence AFTER picking to avoid that
    /// collision -- but that re-numbering was unnecessary AND actively harmful: vanilla's OWN
    /// absolute indices (the <c>Data/Bundles</c> key index, or the RandomBundles <c>Keys</c>-driven
    /// absolute index) are ALREADY globally unique across rooms by construction, and
    /// <see cref="RemixSelector.PickForRoom"/> now preserves each pick's absolute index as-is (see
    /// its class doc) instead of re-indexing to a room-local 0..n-1 sequence. So this method no
    /// longer re-numbers anything; it only guards against a collision that should be structurally
    /// impossible with vanilla data (see <see cref="Generate"/>'s duplicate-index check).
    ///
    /// This matters beyond just avoiding the collision: the write-key space this method emits is
    /// now VANILLA'S OWN absolute index space -- the SAME key space a legacy (vanilla-bundled) save
    /// already has entries in. Because <c>NetWorldState.SetBundleData</c> merges/upserts and NEVER
    /// removes a key, the OLD synthetic global-index scheme produced a key space DISJOINT from a
    /// legacy save's board -- the migration write couldn't overwrite the old board, so every legacy
    /// bundle survived as a ghost entry alongside the new engine-authored ones (live smoke-test
    /// finding, task 8: "50 classified / 114 items" after one reset on a legacy save). Writing in
    /// vanilla's own index space means the FIRST engine write on a legacy save overwrites every
    /// legacy "Room/index" key outright -- no ghosts, no migration step needed. It also incidentally
    /// fixed a second, downstream bug: <c>CommunityCenter.initAreaBundleConversions</c>
    /// (decompile: StardewValley.Locations/CommunityCenter.cs) does a plain
    /// <c>bundleToAreaDictionary.Add(num, ...)</c> for every key in the persisted, ever-merged
    /// <c>NetWorldState.BundleData</c> -- a duplicate NUMERIC index shared by two different rooms
    /// (exactly what the disjoint global-index scheme produced) throws <c>ArgumentException</c>
    /// there, which <c>Game1.AddLocations</c> catches and logs as "Couldn't create the
    /// 'CommunityCenter' location." Writing in vanilla's own per-room-unique absolute index space
    /// makes that numeric collision structurally impossible again.
    ///
    /// The write-key space (the full set of "Room/index" keys <see cref="WriteToWorld"/> emits)
    /// MUST be identical across every generation for a given pool shape, because
    /// <c>NetWorldState.SetBundleData</c> merges/upserts and NEVER removes a key -- a generation
    /// that emitted fewer keys than a previous one would leave stale bundles behind.
    /// </summary>
    internal sealed class BundleEngine
    {
        private const string VaultRoomName = "Vault";
        private const string MoneySlotId = "-1";

        // Per-bundle RNG salt for slot composition (trim + Plan-2 slot filling). spec.Index is
        // vanilla's own absolute bundle index — unique per generation — so each bundle gets an
        // independent deterministic stream from the loop seed.
        private const int SlotSaltPrime = 6151;

        /// <summary>Salt for the fish-ask roll, so it draws from its own stream and cannot shift
        /// the slot roll of a board generated before it existed.</summary>
        private const int FishAskSalt = 0x5F15;

        /// <summary>Salt for the board-level legendary allowance roll (LegendaryFishRules.BoardAllowance).</summary>
        private const int LegendarySalt = 0x1E6D;

        // The filler's "no stretch item for X" / "no hard item" lines are diagnostics about the
        // shape of a POOL, not events: on a board of 30-odd bundles they fire dozens of times per
        // generation and drown the swaps a reader actually wants to see. Keep them (they explain a
        // bundle the audit flags later) but at Trace; the swaps themselves stay at Info.
        private const string NoStretchLog = "no stretch item";
        private const string NoHardLog = "no hard item";

        private static LogLevel FillerLogLevel(string message)
            => message.Contains(NoStretchLog) || message.Contains(NoHardLog)
                ? LogLevel.Trace
                : LogLevel.Info;

        // Per-def RNG salt for authored bundle composition (Plan-3 "authored bundles"). Mirrors
        // RemixSelector's own RoomSaltPrime/StableRoomSalt idiom, but salted on the AUTHORED
        // DEF NAME rather than room -- see the doc comment on the composition block in Generate
        // for why (streams must be independent of room/position enumeration).
        private const int AuthoredSaltPrime = 5381;

        private static readonly (int Value, string Symbol)[] RomanNumerals =
        {
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };

        // Authored bundle names (Plan-3), for the classify/fill/trim exemption below -- see the
        // doc comment on the pick loop in Generate. Built once from AuthoredBundleCatalog.All
        // rather than re-querying .Any(...) per pick.
        private static readonly HashSet<string> AuthoredBundleNames =
            new(AuthoredBundleCatalog.All.Select(def => def.Name), StringComparer.Ordinal);

        private readonly VanillaBundlePool _pool;
        private readonly IMonitor _monitor;
        private readonly BundleGenerationTuning _tuning;
        private readonly bool _nonObjectDonationsEnabled;
        private readonly Dictionary<int, DomainMatch> _lastDomains = new();
        private readonly Dictionary<int, string> _lastRecipes = new();
        private readonly HashSet<string> _lastVanillaOnlyRecipes = new(StringComparer.Ordinal);
        private readonly RarityThresholds _thresholds;
        /// <summary>The difficulty profile this generation runs under. Supplies the item-rarity
        /// pool bias and the required-slots adjustment; the stack and quality modifiers arrive
        /// already baked into <see cref="_tuning"/> via DifficultyTuning.Scale, so they need no
        /// handling here. MUST be the caller's STAMPED profile (MetaState.Difficulty), never live
        /// config: the SaveLoaded re-derivation has to reproduce the board in the save, and a
        /// GMCM change mid-loop would otherwise re-derive a different one.</summary>
        private readonly Core.DifficultyProfile _difficulty;
        /// <summary>Save-specific pool exclusions (YearTwoCrops.ExcludedFor). Part of the
        /// generation inputs: reset and reload must pass the same set.</summary>
        private readonly IReadOnlySet<string> _extraExcludedIds;

        /// <summary>Derived item model, forwarded to <see cref="BundleSlotFiller.Fill"/> for the
        /// stretch and hard-item swaps (spec 2026-08-28-obtainable-board-2-stretch). Must be set the
        /// same way at every construction site: the board is re-generated and compared at save load.</summary>
        public Core.ItemAvailabilityModel Availability { get; set; }
        private int _lastSeed;

        public IReadOnlyDictionary<string, Core.Season> LastDerivedSeasonPins { get; private set; }
            = new Dictionary<string, Core.Season>();

        /// <summary>Every non-Vault pick's domain classification from the last <see
        /// cref="Generate"/> call, keyed by absolute index (diagnostics; see tly_genbundles).</summary>
        public IReadOnlyDictionary<int, DomainMatch> LastDomains => _lastDomains;

        /// <summary>For every pick that rolled from a <see cref="PoolDomain.Recipe"/> recipe, the
        /// recipe's name and its part labels, keyed by absolute index (diagnostics only; see
        /// tly_genbundles' "re-rolled from recipe" line).</summary>
        public IReadOnlyDictionary<int, string> LastRecipes => _lastRecipes;

        /// <summary>The names of the picks whose recipe had no pool to roll and offered the
        /// bundle's own items only (<see cref="Core.PoolRecipe.IsVanillaOnly"/>). The gate audit
        /// tags them, so a board that quietly stopped rolling is visible in the log.</summary>
        public IReadOnlySet<string> LastVanillaOnlyRecipes => _lastVanillaOnlyRecipes;

        public BundleEngine(IMonitor monitor, BundleGenerationTuning tuning, bool nonObjectDonationsEnabled, RarityThresholds thresholds = null, IReadOnlySet<string> extraExcludedIds = null, Core.DifficultyProfile difficulty = null)
        {
            _extraExcludedIds = extraExcludedIds;
            _monitor = monitor;
            _pool = new VanillaBundlePool(monitor);
            _tuning = tuning ?? new BundleGenerationTuning();
            _nonObjectDonationsEnabled = nonObjectDonationsEnabled;
            _thresholds = thresholds ?? new RarityThresholds();
            _difficulty = difficulty ?? new Core.DifficultyProfile();
        }

        /// <summary>The reshuffle-path pity trim stamped on the CURRENT board, or null. Every
        /// Generate call for a live board must pass this so a reload reproduces the same set.</summary>
        public static PityTrim TrimFor(MetaState meta)
            => meta.BoardTrimSeason >= 0 && meta.BoardTrimSeason < Calendar.MonthsPerYear && meta.BoardTrimSteps > 0
                ? new PityTrim((Core.Season)meta.BoardTrimSeason, meta.BoardTrimSteps)
                : null;

        /// <summary>Draws one bundle per room-position (Vault unmodified) and returns the
        /// generated set. Deterministic for a given seed (see <see cref="BundleEngineSeed"/>).</summary>
        public GeneratedBundleSet Generate(int seed, PityTrim trim = null)
        {
            _lastSeed = seed;
            _lastDomains.Clear();
            _lastRecipes.Clear();
            _lastVanillaOnlyRecipes.Clear();
            ItemPools itemPools = new GameDataPools(_monitor).Build(_tuning, _extraExcludedIds);
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
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools =
                WidenWithAuthoredBundles(_pool.BuildRoomPools(), itemPools, seed, legendaryAllowance);

            var allPicks = new List<BundleSpec>();
            var usedNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            // Absolute index -> the (room, name) that already claimed it, for the defensive
            // duplicate-index check below (see class doc: every candidate already carries
            // vanilla's own globally-unique absolute index, so a collision here should be
            // structurally impossible with vanilla data).
            var claimedIndices = new Dictionary<int, (string Room, string Name)>();

            // Vault passes through UNMODIFIED (single-candidate positions, real indices kept).
            if (pools.TryGetValue(VaultRoomName, out IReadOnlyList<IReadOnlyList<BundleSpec>> vaultPositions))
            {
                foreach (IReadOnlyList<BundleSpec> candidates in vaultPositions)
                {
                    if (candidates.Count == 0)
                        continue; // already WARN-logged by BuildRoomPools
                    BundleSpec spec = VaultAmountScaler.Scale(candidates[0], _tuning.VaultAmountMultiplier);
                    if (!TryClaimIndex(spec, claimedIndices))
                        continue;
                    allPicks.Add(Uniquify(spec, usedNameCounts));
                }
            }

            // Pass 1: pick and classify. Deterministic room order (ordinal by name) rather than
            // the dictionary's own enumeration order -- Dictionary<TKey,TValue> enumeration order
            // is an implementation detail, not a contract, so relying on it would make the
            // fixed-key-space guarantee below fragile across process launches/.NET versions even
            // though the seed is the same. Picks whose items are already final (authored, or a
            // domain the engine does not re-roll) seed the board-wide "asked" set here.
            var picked = new List<PickRecord>();
            var asked = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomEntry
                     in pools.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (roomEntry.Key == VaultRoomName)
                    continue;

                IReadOnlyList<BundleSpec> picks = RemixSelector.PickForRoom(roomEntry.Value, seed, roomEntry.Key);
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
                    Core.PoolRecipe? recipe = match.Domain == PoolDomain.Recipe
                        ? BundleSlotFiller.RecipeFor(pick, itemPools, Availability)
                        : null;
                    picked.Add(new PickRecord(pick, match, null) { Recipe = recipe });
                }
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
                int legendariesSoFar = picked.Count(r => r.Composed != null && r.Composed.Slots.Any(sl => Core.LegendaryFishRules.IsLegendary(sl.ItemId)));
                IReadOnlySet<string> banned = legendariesSoFar >= legendaryAllowance ? Core.LegendaryFishRules.Ids : null;
                BundleSpec composed = BundleSlotFiller.Fill(pick, record.Match, itemPools, _tuning, slotRng, trim, _thresholds,
                    msg => _monitor?.Log("BundleEngine: " + msg, FillerLogLevel(msg)), asked, Availability, record.Recipe, banned);
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
                BundleSpec finished = record.Composed!;
                var fishRng = new Random(seed ^ (finished.Index * SlotSaltPrime) ^ FishAskSalt);
                BundleSpec composed = Core.QuantityAskPass.Apply(finished, _difficulty,
                    id => BundleSlotFiller.DeadlineFor(finished, record.Match, finished.Slots, id, Availability), fishRng);
                composed = Core.StackScaling.Apply(composed, _difficulty);
                composed = Core.RequiredSlots.Apply(composed, _difficulty);
                allPicks.Add(Uniquify(composed, usedNameCounts));
            }

            return new GeneratedBundleSet(allPicks);
        }

        /// <summary>One non-Vault pick between the passes of <see cref="Generate"/>: Composed is
        /// null until pass 2 fills it (authored and kept-vanilla picks arrive composed).</summary>
        private sealed class PickRecord
        {
            public PickRecord(BundleSpec pick, DomainMatch match, BundleSpec composed)
            {
                Pick = pick;
                Match = match;
                Composed = composed;
            }

            public BundleSpec Pick { get; }
            public DomainMatch Match { get; }
            public BundleSpec Composed { get; set; }

            /// <summary>This pick's pool recipe, built once in pass 1 (null for every domain but
            /// Recipe). Shared by the fill-order count, the diagnostics line and the fill.</summary>
            public Core.PoolRecipe? Recipe { get; init; }
        }

        /// <summary>A gold ask (a money slot), or a bundle with nothing to re-roll at all. These
        /// are the only picks the classifier may leave at <see cref="PoolDomain.None"/>.</summary>
        private static bool IsMoneyBundle(BundleSpec spec)
            => spec.Slots.Count == 0 || spec.Slots.Any(s => s.ItemId == MoneySlotId);

        /// <summary>Records every concrete item a bundle asks for (money and category slots are
        /// not items) in the qualified form the pools use, so later fills can leave them out.</summary>
        private static void AddAskedItems(HashSet<string> asked, BundleSpec spec)
        {
            foreach (BundleSlotSpec slot in spec.Slots)
            {
                if (slot.ItemId == MoneySlotId || BundleParsing.IsCategoryRef(slot.ItemId))
                    continue;
                string id = BundleParsing.NormalizeItemId(slot.ItemId);
                if (!string.IsNullOrEmpty(id))
                    asked.Add(id);
            }
        }

        /// <summary>Requirements manifest with data-derived season pins merged UNDER the
        /// caller's pins (hand-curated defaults + user config always win; derived pins only
        /// fill gaps for items the curated table has never seen — e.g. re-rolled or modded
        /// ingredients). Call after Generate.</summary>
        public IReadOnlyList<BundleRequirement> BuildRequirements(
            GeneratedBundleSet set,
            IReadOnlyDictionary<string, Core.Season> basePins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            SeasonEase ease = null,
            Core.ItemAvailabilityModel availability = null)
        {
            var merged = new Dictionary<string, Core.Season>(LastDerivedSeasonPins, StringComparer.Ordinal);
            foreach (KeyValuePair<string, Core.Season> pin in basePins)
                merged[pin.Key] = pin.Value;
            return set.BuildRequirements(merged, bundleQuotas, ease, availability);
        }

        /// <summary>Writes the generated set into <c>Game1.netWorldState</c> and re-syncs the CC
        /// location. See the class doc comment for the merge-vs-replace finding this handles.</summary>
        public void WriteToWorld(GeneratedBundleSet set, IMonitor monitor)
        {
            Dictionary<string, string> newData = new Dictionary<string, string>(set.ToBundleData());

            // SetBundleData is MERGE/ADDITIVE, not a replace (NetWorldState.cs: SetBundleData ->
            // netBundleData.CopyFrom(data), and NetDictionary.CopyFrom only upserts keys present
            // in `data` -- it never removes a key that isn't). That's safe here without an
            // explicit clear because Generate() always emits exactly one entry per room-position
            // spanning EVERY position VanillaBundlePool.BuildRoomPools() found this call -- the
            // same fixed vanilla-defined position count every time -- so newData's key space is
            // always the complete key space; there is no shrinking room that could leave a stale
            // key behind.
            Game1.netWorldState.Value.SetBundleData(newData);

            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null && cc.Map != null)
            {
                // Same idiom as WorldResetService.PerformReset step 1a: zero every completion
                // NetArray/NetBool IN PLACE (never Clear() the keys -- vanilla does bundles[i]
                // lookups that would KeyNotFoundException on a missing entry).
                foreach (KeyValuePair<int, Netcode.NetArray<bool, Netcode.NetBool>> kvp in Game1.netWorldState.Value.Bundles.FieldDict)
                {
                    Netcode.NetArray<bool, Netcode.NetBool> arr = kvp.Value;
                    for (int i = 0; i < arr.Length; i++)
                        arr[i] = false;
                }
                foreach (KeyValuePair<int, Netcode.NetBool> kvp in Game1.netWorldState.Value.BundleRewards.FieldDict)
                    kvp.Value.Value = false;
                for (int i = 0; i < cc.areasComplete.Count; i++)
                    cc.areasComplete[i] = false;

                cc.MakeMapModifications(force: true);
            }

            int roomCount = set.Bundles.Select(b => b.Room).Distinct().Count();
            monitor.Log(
                $"BundleEngine: wrote {set.Bundles.Count} bundles across {roomCount} rooms (seed {_lastSeed}).",
                LogLevel.Info);
        }

        /// <summary>Composes every Plan-3 authored bundle def ONCE per generation and appends a
        /// position-specific clone to EVERY position of the def's room's slot pools, mirroring
        /// <see cref="VanillaBundlePool"/>'s own wildcard-widening idiom (its
        /// AddCandidateAtAbsoluteIndex, called once per position for Index == -1 bundles). Each
        /// clone is `composed with { Index = positionAbsoluteIndex }` -- the position's
        /// absolute index is read off that position's EXISTING first candidate
        /// (<c>positions[p][0].Index</c>), which is reliable because BuildRoomPools already
        /// skips empty positions. The composed spec is deliberately NEVER re-composed per
        /// position and NEVER re-indexed by <see cref="RemixSelector"/> -- RemixSelector
        /// preserves whichever candidate's absolute index it picks as-is (see its class doc), so
        /// every position needs its own index-stamped clone up front, not a re-index after the
        /// pick.
        ///
        /// Determinism: each def's RNG stream is seeded from
        /// <c>seed ^ (StableAuthoredSalt(def.Name) * AuthoredSaltPrime)</c> -- salted on the
        /// def's NAME alone, independent of room/position enumeration order (unlike
        /// <see cref="RemixSelector"/>'s per-ROOM salt) -- so a def's composed slots never shift
        /// because another def or room widened first, or because BuildRoomPools' dictionary
        /// enumeration order changes. SeasonSpread defs (Four Seasons Sampler) consume retry
        /// attempts only from their OWN stream inside <see cref="AuthoredBundleComposer"/>; no
        /// other def's stream is ever touched.</summary>
        /// <summary>The COMPLETE candidate set a generation picks from: vanilla's own per-position
        /// pools widened with the mod's authored bundles. Exposed for diagnostics (tly_dumpbundles)
        /// so a catalogue cannot report a narrower set of possibilities than the engine actually
        /// has -- reading BuildRoomPools alone omits every authored bundle and makes positions look
        /// like they have no alternates when they do.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> BuildCandidatePools(
            ItemPools itemPools, int seed)
            => WidenWithAuthoredBundles(_pool.BuildRoomPools(), itemPools, seed, int.MaxValue);

        private IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> WidenWithAuthoredBundles(
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools,
            ItemPools itemPools, int seed, int legendaryAllowance)
        {
            var widened = new Dictionary<string, List<List<BundleSpec>>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomEntry in pools)
                widened[roomEntry.Key] = roomEntry.Value.Select(positions => new List<BundleSpec>(positions)).ToList();

            foreach (AuthoredBundleDef def in AuthoredBundleCatalog.All)
            {
                if (!widened.TryGetValue(def.Room, out List<List<BundleSpec>> positions))
                {
                    _monitor?.Log(
                        $"BundleEngine: authored def '{def.Name}' targets room '{def.Room}' which has no " +
                        "live position pools this generation -- skipped.",
                        LogLevel.Trace);
                    continue;
                }

                var authoredRng = new Random(seed ^ (StableAuthoredSalt(def.Name) * AuthoredSaltPrime));
                // absoluteIndex: 0 is a placeholder -- every position clone below overwrites it
                // with that position's own absolute index (see doc comment above).
                BundleSpec composed = AuthoredBundleComposer.Compose(
                    def, absoluteIndex: 0, itemPools, _tuning, _nonObjectDonationsEnabled, authoredRng,
                    Availability?.Step ?? Core.DifficultyStep.Normal,
                    banned: legendaryAllowance == 0 ? Core.LegendaryFishRules.Ids : null);
                if (composed == null)
                {
                    _monitor?.Log(
                        $"BundleEngine: authored def '{def.Name}' couldn't be composed this generation " +
                        "(source pool too small, or season-spread retry budget exhausted) -- skipped.",
                        LogLevel.Trace);
                    continue;
                }

                for (int position = 0; position < positions.Count; position++)
                {
                    int positionAbsoluteIndex = positions[position][0].Index; // every position has >= 1 candidate
                    positions[position].Add(composed with { Index = positionAbsoluteIndex });
                }
            }

            var result = new Dictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<List<BundleSpec>>> roomEntry in widened)
                result[roomEntry.Key] = roomEntry.Value.Select(p => (IReadOnlyList<BundleSpec>)p).ToList();
            return result;
        }

        /// <summary>Deterministic, culture/runtime-stable salt for an authored bundle def's NAME
        /// (string.GetHashCode is randomized per process in .NET — never use it for persisted
        /// determinism). Same char-walk hash idiom as <see cref="RemixSelector"/>'s own private
        /// StableRoomSalt, copied here (rather than shared) because it salts on a different key
        /// (def name, not room) for a different, independent RNG stream — see
        /// <see cref="WidenWithAuthoredBundles"/>'s doc comment.</summary>
        private static int StableAuthoredSalt(string name)
        {
            int hash = 17;
            foreach (char c in name) hash = unchecked(hash * 31 + c);
            return hash;
        }

        /// <summary>Defensive duplicate-index guard: claims <paramref name="spec"/>'s absolute
        /// index, or -- if another spec already claimed it this generation -- logs an ERROR naming
        /// both bundles and returns false so the caller skips this (later) one. Should be
        /// impossible with vanilla data (see class doc's global-index note); this only prevents a
        /// silent Bundles/BundleRewards NetIntDictionary collision if it somehow happens (e.g. a
        /// malformed RandomBundles Keys entry slipping past VanillaBundlePool's own fallback).</summary>
        private bool TryClaimIndex(BundleSpec spec, Dictionary<int, (string Room, string Name)> claimedIndices)
        {
            if (claimedIndices.TryGetValue(spec.Index, out (string Room, string Name) existing))
            {
                _monitor?.Log(
                    $"BundleEngine: duplicate absolute index {spec.Index} -- '{existing.Room}/{existing.Name}' " +
                    $"already claimed it, skipping '{spec.Room}/{spec.Name}' (should be impossible with vanilla data).",
                    LogLevel.Error);
                return false;
            }
            claimedIndices[spec.Index] = (spec.Room, spec.Name);
            return true;
        }

        /// <summary>Suffixes " II", " III"... on a name collision within this generation
        /// (RandomBundles reuses variant names across positions/rooms; downstream matches by
        /// name, so every name in a generated set must be unique).</summary>
        private static BundleSpec Uniquify(BundleSpec spec, Dictionary<string, int> usedNameCounts)
        {
            if (!usedNameCounts.TryGetValue(spec.Name, out int count))
            {
                usedNameCounts[spec.Name] = 1;
                return spec;
            }
            count++;
            usedNameCounts[spec.Name] = count;
            string suffix = " " + ToRoman(count);
            return spec with { Name = spec.Name + suffix, DisplayName = spec.DisplayName + suffix };
        }

        private static string ToRoman(int n)
        {
            var sb = new StringBuilder();
            foreach ((int value, string symbol) in RomanNumerals)
            {
                while (n >= value)
                {
                    sb.Append(symbol);
                    n -= value;
                }
            }
            return sb.ToString();
        }
    }
}
