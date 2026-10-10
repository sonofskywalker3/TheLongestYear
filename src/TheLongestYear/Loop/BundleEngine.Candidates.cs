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
        /// <summary>Randomizer reward pool: every reward vanilla's standard and remixed bundles
        /// can give, from every room except the Abandoned Joja Mart (the Vault's item rewards are
        /// included). Shared by the Engine board and the Vanilla/Remixed reset pass so
        /// both sources draw from the same list. The Engine passes <see cref="VanillaOnlyIds"/>,
        /// which leaves out any reward that gives another mod's item; the Vanilla/Remixed pass
        /// passes nothing and keeps every reward, as it always has.</summary>
        public static IReadOnlyList<string> RewardPool(
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomPools,
            IReadOnlySet<string> vanillaOnlyIds = null)
            => Core.BundleRewardShuffle.CleanPool(
                roomPools
                    .Where(room => !IsRewardShuffleSkippedRoom(room.Key))
                    .SelectMany(room => room.Value)
                    .SelectMany(candidates => candidates)
                    .Select(spec => spec.RewardField),
                vanillaOnlyIds);

        /// <summary>The bundle templates (Data/Bundles + Data/RandomBundles) with every other
        /// mod's item and reward taken out (<see cref="Core.VanillaOnlyBoard.FilterRoomPools"/>).
        /// On an unmodded game nothing changes and every candidate is the same instance. With
        /// <see cref="AllowModItems"/> on, the templates come back as they are, as before 0.19.2.</summary>
        private IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> VanillaRoomPools()
        {
            if (AllowModItems)
                return _pool.BuildRoomPools();
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> filtered =
                Core.VanillaOnlyBoard.FilterRoomPools(
                    _pool.BuildRoomPools(), VanillaOnlyIds, Core.VanillaBundleBoard.Standard, out int changed);
            if (changed > 0)
                _monitor?.Log(
                    $"BundleEngine: TLY Custom boards use vanilla items only; {changed} bundle template(s) from other mods " +
                    "had items or rewards left out.",
                    LogLevel.Info);
            return filtered;
        }

        /// <summary>Drops Helper's from every position that has another candidate to pick instead,
        /// for a board with no Mystery Box allowance: its only items are the Prize Ticket and the
        /// Mystery Box, so a fill with the box banned cannot fill it and would fall back to
        /// vanilla's own box ask. A position where Helper's is the only candidate keeps it.</summary>
        private static IReadOnlyList<IReadOnlyList<BundleSpec>> WithoutHelpers(IReadOnlyList<IReadOnlyList<BundleSpec>> positions)
            => positions
                .Select(candidates =>
                {
                    List<BundleSpec> others = candidates.Where(c => c.Name != HelpersBundleName).ToList();
                    return others.Count > 0 && others.Count < candidates.Count
                        ? (IReadOnlyList<BundleSpec>)others
                        : candidates;
                })
                .ToList();

        /// <summary>What is left of each capped item's allowance for <paramref name="record"/>'s
        /// fill: the allowance, less what the bundles composed so far ask for (read as clamped,
        /// since every capped slot ends at one), less a Mystery Box kept back for each picked
        /// Helper's still waiting to fill. Helper's itself fills against its own reservation.</summary>
        private static IReadOnlyDictionary<string, int> CappedBudget(
            IReadOnlyList<PickRecord> picked, PickRecord record, int cappedAllowance)
        {
            List<BundleSpec> composedSoFar = picked
                .Where(r => r.Composed != null)
                .Select(r => Core.CappedAsks.ClampBundle(r.Composed))
                .ToList();
            var budget = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in Core.CappedAsks.Ids)
                budget[id] = cappedAllowance - Core.CappedAsks.CountOnBoard(id, composedSoFar);

            if (record.Pick.Name == HelpersBundleName)
            {
                // Never hand Helper's more than the board has left. Known exception: when the Mystery
                // Box allowance is 0 and Helper's is the only candidate at its position, it is kept
                // (a position cannot be left empty) and its vanilla Mystery Box slots clamp to one box.
                budget[Core.CappedAsks.MysteryBox] = Math.Min(HelpersMysteryBoxReservation, budget[Core.CappedAsks.MysteryBox]);
                return budget;
            }
            int helpersWaiting = picked.Count(r => r != record && r.Composed == null && r.Pick.Name == HelpersBundleName);
            budget[Core.CappedAsks.MysteryBox] -= helpersWaiting * HelpersMysteryBoxReservation;
            return budget;
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
            => WidenWithAuthoredBundles(VanillaRoomPools(), itemPools, seed, int.MaxValue, int.MaxValue);

        private IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> WidenWithAuthoredBundles(
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools,
            ItemPools itemPools, int seed, int legendaryAllowance, int cappedAllowance)
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
                Core.DifficultyStep step = Availability?.Step ?? Core.DifficultyStep.Normal;
                // Easy keeps the slow-route books (gold tools, 1,000 kills) off the Book bundle.
                var bannedIds = new HashSet<string>(Core.BookRouteRules.BannedFor(step), StringComparer.Ordinal);
                if (legendaryAllowance == 0) bannedIds.UnionWith(Core.LegendaryFishRules.Ids);
                if (cappedAllowance == 0) bannedIds.UnionWith(Core.CappedAsks.Ids);
                BundleSpec composed = AuthoredBundleComposer.Compose(
                    def, absoluteIndex: 0, itemPools, _tuning, _nonObjectDonationsEnabled, authoredRng,
                    step, banned: bannedIds.Count > 0 ? bannedIds : null);
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
    }
}
