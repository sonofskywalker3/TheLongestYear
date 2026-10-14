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
    internal sealed partial class BundleEngine
    {
        private const string VaultRoomName = "Vault";

        /// <summary>The Abandoned Joja Mart's room key in <c>Data/Bundles</c> (it holds exactly one
        /// bundle, The Missing, which vanilla only offers once the hall is finished and the year has
        /// turned). Not a TLY room: <see cref="Core.RoomThemeMap"/> rejects it alongside the Vault,
        /// so it never carries a theme, never counts toward a season gate and never appears in the
        /// donation catalog.</summary>
        private const string AbandonedJojaRoomName = "Abandoned Joja Mart";

        private const string MoneySlotId = "-1";

        /// <summary>Rooms the engine emits but never re-rolls. Both are outside the loop's economy
        /// (<see cref="Core.RoomThemeMap"/> rejects both), so their contents stay exactly as vanilla
        /// authored them.
        ///
        /// The Joja room was missing from this list until 0.17.13, which is how The Missing Bundle
        /// ended up asking for a legendary fish (Nexus bugs 1130863, ChaoticMindset). The read side
        /// had always excluded the room, but the WRITE side exempted only the Vault by name, so the
        /// pool that <see cref="VanillaBundlePool.BuildRoomPools"/> builds for every room key in
        /// <c>Data/Bundles</c> went through <see cref="PoolDomainClassifier"/> like a CC room's. That
        /// was harmless while an unrecognised bundle kept its vanilla slots, but since spec
        /// 2026-08-28-obtainable-board-3-pools only a money or empty bundle classifies to
        /// <see cref="PoolDomain.None"/> -- everything else falls through to its recipe. The Missing
        /// (Wine, Dinosaur Mayonnaise, Prismatic Shard, Ancient Fruit, Void Salmon, Caviar) has no
        /// two-thirds majority in any one pool, so it took the recipe path and re-rolled.
        ///
        /// These rooms are PASSED THROUGH, not skipped. Dropping their keys would leave a save that
        /// already received a re-rolled board stuck with it forever: <c>SetBundleData</c> merges and
        /// upserts but never removes, so a key we stop emitting keeps its last written value (see the
        /// fixed-key-space note in the class doc). Emitting vanilla's own entry overwrites it.</summary>
        private static readonly IReadOnlyList<string> PassThroughRooms = new[] { VaultRoomName, AbandonedJojaRoomName };

        /// <summary>Whether <see cref="Generate"/> emits this room's vanilla entry untouched instead
        /// of re-rolling it. Exposed so the tly_dumpbundles catalogue reports these rooms the way the
        /// engine actually treats them: that report classifies each candidate on its own, so without
        /// this it describes the pool a pass-through room WOULD have drawn from and reads as though
        /// the room still re-rolls.</summary>
        public static bool IsPassThroughRoom(string room) => PassThroughRooms.Contains(room);

        /// <summary>Whether the Randomizer's "Random bundle rewards" leaves this room's rewards
        /// alone and keeps them out of the reward pool. Only the Abandoned Joja Mart: the Vault's
        /// rewards are ordinary item rewards (field 1, e.g. "O 220 3") and shuffle like any other
        /// bundle's, while its gold amounts (field 2) are never touched by the shuffle.</summary>
        public static bool IsRewardShuffleSkippedRoom(string room) => Core.BundleRewardShuffle.SkipsRoom(room);

        /// <summary>Rooms the Prismatic Shard / Mystery Box board count leaves out: the Vault and
        /// the Abandoned Joja Mart are outside the year's goal (no theme, no season gate), so their
        /// asks are not the year's. The Missing's own Prismatic Shard is not counted against the
        /// allowance; its contents stay exactly as vanilla wrote them. Pass 2 never counts these
        /// rooms either, because they are emitted before it and never enter its pick records.</summary>
        private static bool CappedCountSkipsRoom(string room) => PassThroughRooms.Contains(room);

        // Per-bundle RNG salt for slot composition (trim + Plan-2 slot filling). spec.Index is
        // vanilla's own absolute bundle index — unique per generation — so each bundle gets an
        // independent deterministic stream from the loop seed.
        private const int SlotSaltPrime = 6151;

        /// <summary>Salt for the fish-ask roll, so it draws from its own stream and cannot shift
        /// the slot roll of a board generated before it existed.</summary>
        private const int FishAskSalt = 0x5F15;

        /// <summary>Salt for the bundle-count dial's per-room roll (how many, which to drop, which
        /// to add), so it cannot move any stream that existed before it.</summary>
        private const int BundleCountSalt = 0x0BC7;
        private const int BundleCountSaltPrime = 4243;

        /// <summary>Salt for the board-level legendary allowance roll (LegendaryFishRules.BoardAllowance).</summary>
        private const int LegendarySalt = 0x1E6D;

        /// <summary>Remixed's Helper's bundle, whose only items are the Prize Ticket and the Mystery
        /// Box (Data/RandomBundles). A board with no Mystery Box allowance leaves it out of its
        /// position's candidates; a board that picks it keeps one Mystery Box back for it
        /// (CappedAsks, spec 2026-09-30-quantity-rules section 2).</summary>
        private const string HelpersBundleName = "Helper's";

        /// <summary>The Mystery Boxes a picked Helper's keeps back from the rest of the board.</summary>
        private const int HelpersMysteryBoxReservation = 1;

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

        /// <summary>TLY Custom boards ask only for vanilla items and give only vanilla rewards
        /// (spec 2026-10-08-custom-board-vanilla-only): balancing every item of every other mod is
        /// not possible, so their items never enter this board's item pools, templates or reward
        /// pool. This class only ever builds the Engine board; Normal and Remixed never construct
        /// it, so they keep other mods' items exactly as before. Category slots ("any fish") still
        /// take a modded fish when donated.</summary>
        public static IReadOnlySet<string> VanillaOnlyIds => Core.VanillaItemIds.All;

        /// <summary>The filter for a board built with "Allow mod items in custom bundles" set to
        /// <paramref name="allowModItems"/>: null (no filter, the pre-0.19.2 board) when allowed,
        /// <see cref="VanillaOnlyIds"/> otherwise (spec addendum 1).</summary>
        public static IReadOnlySet<string> VanillaOnlyIdsFor(bool allowModItems)
            => allowModItems ? null : VanillaOnlyIds;

        /// <summary>"Allow mod items in custom bundles" for THIS board. Every construction site
        /// sets it from the save: a reset from <see cref="Core.CustomBoardModItems.ForReset"/>, a
        /// load-time re-derivation or diagnostic from the board's stamp
        /// (<see cref="Core.MetaState.BoardAllowsModItems"/>), never the live choice. Off (the
        /// default) is the vanilla-only board.</summary>
        public bool AllowModItems { get; set; }

        private IReadOnlySet<string> VanillaFilter => VanillaOnlyIdsFor(AllowModItems);

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
            public Core.PoolRecipe Recipe { get; init; }
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
