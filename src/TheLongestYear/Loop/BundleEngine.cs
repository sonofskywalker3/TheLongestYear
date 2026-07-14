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
    /// silently share one completion NetArray. <see cref="RemixSelector.PickForRoom"/> resets
    /// every room's picks to a room-local 0..n-1 index (by design, per its own doc comment), so
    /// this method re-numbers every non-Vault room's picks onto a single global, collision-free
    /// index space AFTER picking, reserving the Vault room's real (unmodified) indices first.
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
        private int _lastSeed;

        public BundleEngine(IMonitor monitor)
        {
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
            var reservedIndices = new HashSet<int>();

            // Vault passes through UNMODIFIED (single-candidate positions, real indices kept) --
            // reserve its indices first so the global re-numbering pass below can never collide
            // with them.
            if (pools.TryGetValue(VaultRoomName, out IReadOnlyList<IReadOnlyList<BundleSpec>> vaultPositions))
            {
                foreach (IReadOnlyList<BundleSpec> candidates in vaultPositions)
                {
                    if (candidates.Count == 0)
                        continue; // already WARN-logged by BuildRoomPools
                    BundleSpec spec = candidates[0];
                    reservedIndices.Add(spec.Index);
                    allPicks.Add(Uniquify(spec, usedNameCounts));
                }
            }

            int nextGlobalIndex = 0;
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
                    while (reservedIndices.Contains(nextGlobalIndex))
                        nextGlobalIndex++;
                    reservedIndices.Add(nextGlobalIndex);
                    BundleSpec reindexed = pick with { Index = nextGlobalIndex };
                    nextGlobalIndex++;
                    allPicks.Add(Uniquify(reindexed, usedNameCounts));
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
