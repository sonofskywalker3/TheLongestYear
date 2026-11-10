using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class BundleEngine
    {
        /// <summary>The board's rewards, the last pass of <see cref="Generate"/> (Jeff, 2026-11-10:
        /// "it should stay vanilla, but it should draw from the same limited pool"). Random rewards
        /// on: every bundle but Joja's draws from its room's pool, marked off board-wide. Off:
        /// vanilla and remix bundles keep theirs, and each authored bundle takes an unused reward
        /// from its room's standard set, so Orchard no longer repeats Quality Crops' Preserves Jar.
        /// <paramref name="authoredIndices"/> names the authored bundles by index, taken before
        /// <see cref="Uniquify"/> suffixed any name.</summary>
        private IReadOnlyList<BundleSpec> AssignRewards(
            IReadOnlyList<BundleSpec> allPicks, IReadOnlySet<int> authoredIndices,
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> roomPools, int seed, bool randomRewards)
        {
            if (!randomRewards)
            {
                IReadOnlyList<BundleSpec> kept = Core.BundleRewardShuffle.KeepVanillaRewards(
                    allPicks, seed, Core.BundleRewardShuffle.StandardRoomSets(Core.VanillaBundleBoard.Standard, VanillaFilter),
                    spec => authoredIndices.Contains(spec.Index));
                int changed = kept.Where((spec, i) => !ReferenceEquals(spec, allPicks[i])).Count();
                if (changed > 0)
                    _monitor?.Log($"BundleEngine: {changed} authored bundle reward(s) drawn from the rooms' unused vanilla rewards.", LogLevel.Trace);
                return kept;
            }

            IReadOnlyDictionary<string, IReadOnlyList<string>> rewardPools = RewardRoomPools(roomPools, VanillaFilter);
            IReadOnlyList<BundleSpec> rewarded = Core.BundleRewardShuffle.Apply(allPicks, seed, rewardPools, IsRewardShuffleSkippedRoom);
            _monitor?.Log(
                $"Randomizer: bundle rewards shuffled ({rewarded.Count(b => !IsRewardShuffleSkippedRoom(b.Room))} bundles, " +
                $"pool {rewardPools.Values.SelectMany(p => p).Distinct(StringComparer.Ordinal).Count()}).",
                LogLevel.Info);
            return rewarded;
        }
    }
}
