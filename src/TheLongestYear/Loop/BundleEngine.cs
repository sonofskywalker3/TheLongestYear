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

        private static readonly (int Value, string Symbol)[] RomanNumerals =
        {
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };

        private readonly VanillaBundlePool _pool;
        private readonly IMonitor _monitor;
        private int _lastSeed;

        public BundleEngine(IMonitor monitor)
        {
            _monitor = monitor;
            _pool = new VanillaBundlePool(monitor);
        }

        /// <summary>Draws one bundle per room-position (Vault unmodified) and returns the
        /// generated set. Deterministic for a given seed (see <see cref="BundleEngineSeed"/>).</summary>
        public GeneratedBundleSet Generate(int seed)
        {
            _lastSeed = seed;
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools = _pool.BuildRoomPools();

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
                    BundleSpec spec = candidates[0];
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
                    allPicks.Add(Uniquify(pick, usedNameCounts));
                }
            }

            return new GeneratedBundleSet(allPicks);
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
