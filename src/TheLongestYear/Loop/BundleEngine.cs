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
    /// <see cref="RemixSelector"/>; <see cref="WriteToWorld"/> commits the result into
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

        // Per-bundle RNG salt for slot composition (trim + Plan-2 slot filling). spec.Index is
        // vanilla's own absolute bundle index — unique per generation — so each bundle gets an
        // independent deterministic stream from the loop seed.
        private const int SlotSaltPrime = 6151;

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
        private int _lastSeed;

        public IReadOnlyDictionary<string, Core.Season> LastDerivedSeasonPins { get; private set; }
            = new Dictionary<string, Core.Season>();

        /// <summary>Every non-Vault pick's domain classification from the last <see
        /// cref="Generate"/> call, keyed by absolute index (diagnostics; see tly_genbundles).</summary>
        public IReadOnlyDictionary<int, DomainMatch> LastDomains => _lastDomains;

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
            ItemPools itemPools = new GameDataPools(_monitor).Build(_tuning, _extraExcludedIds);
            // Item-rarity modifier (spec 2026-08-26): bias the pool weights the sampler already
            // reads, rather than teaching the sampler about difficulty. A bias of 1.0 returns the
            // same instance, so the default path is untouched.
            itemPools = Core.RarityBias.Apply(itemPools, _difficulty.RarityBias, _thresholds);
            LastDerivedSeasonPins = itemPools.DerivedSeasonPins;

            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools =
                WidenWithAuthoredBundles(_pool.BuildRoomPools(), itemPools, seed);

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

            // Deterministic room order (ordinal by name) rather than the dictionary's own
            // enumeration order -- Dictionary<TKey,TValue> enumeration order is an implementation
            // detail, not a contract, so relying on it would make the fixed-key-space guarantee
            // below fragile across process launches/.NET versions even though the seed is the same.
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

                    BundleSpec composed;
                    if (AuthoredBundleNames.Contains(pick.Name))
                    {
                        // Authored slots (Plan-3) are composed ONCE per def by
                        // AuthoredBundleComposer (see WidenWithAuthoredBundles) and are FINAL --
                        // the composer already made deliberate stack-1/quality-0 choices for
                        // every slot (e.g. Weatherman's = all-fish, Preserver's = all-artisan).
                        // Those authored picks clear PoolDomainClassifier's 2/3 majority just as
                        // easily as a coincidentally-themed vanilla pick, so running them through
                        // the classify/fill/trim chain below would silently RE-ROLL an authored
                        // bundle's already-final slots and make them position-dependent (final-
                        // review finding). Skip the chain entirely for authored picks.
                        composed = pick;
                        _lastDomains[pick.Index] = new DomainMatch(PoolDomain.None, null);
                    }
                    else
                    {
                        var slotRng = new Random(seed ^ (pick.Index * SlotSaltPrime));
                        DomainMatch match = PoolDomainClassifier.Classify(pick, itemPools);
                        composed = BundleSlotFiller.Fill(pick, match, itemPools, _tuning, slotRng, trim, _thresholds,
                            msg => _monitor?.Log("BundleEngine: " + msg, LogLevel.Info));
                        if (ReferenceEquals(composed, pick))
                        {
                            if (match.Domain != PoolDomain.None)
                                _monitor?.Log(
                                    $"BundleEngine: '{pick.Room}/{pick.Name}' matched domain {match.Domain} but its " +
                                    "filtered pool couldn't fill every slot — keeping vanilla slots.",
                                    LogLevel.Trace);
                            composed = SlotTrimmer.Trim(pick, slotRng);
                        }
                        _lastDomains[pick.Index] = match; // for diagnostics (see below)
                    }
                    // Required-slots modifier: adjust the pick-X count only. Applied after
                    // composition so it sees the FINAL shown-slot count (SlotTrimmer and the
                    // filler can both shrink it), and never to the Vault, which RequiredSlots
                    // skips on its own.
                    composed = Core.RequiredSlots.Apply(composed, _difficulty);
                    allPicks.Add(Uniquify(composed, usedNameCounts));
                }
            }

            return new GeneratedBundleSet(allPicks);
        }

        /// <summary>Requirements manifest with data-derived season pins merged UNDER the
        /// caller's pins (hand-curated defaults + user config always win; derived pins only
        /// fill gaps for items the curated table has never seen — e.g. re-rolled or modded
        /// ingredients). Call after Generate.</summary>
        public IReadOnlyList<BundleRequirement> BuildRequirements(
            GeneratedBundleSet set,
            IReadOnlyDictionary<string, Core.Season> basePins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            SeasonEase ease = null)
        {
            var merged = new Dictionary<string, Core.Season>(LastDerivedSeasonPins, StringComparer.Ordinal);
            foreach (KeyValuePair<string, Core.Season> pin in basePins)
                merged[pin.Key] = pin.Value;
            return set.BuildRequirements(merged, bundleQuotas, ease);
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
        private IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> WidenWithAuthoredBundles(
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools,
            ItemPools itemPools, int seed)
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
                    def, absoluteIndex: 0, itemPools, _tuning, _nonObjectDonationsEnabled, authoredRng);
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
