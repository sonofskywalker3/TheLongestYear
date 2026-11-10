using System;
using System.Collections.Generic;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class BundleEngine
    {
        /// <summary>The bundle-count dial for one room: drops or adds bundles to reach the room's
        /// target count (<see cref="Core.BundleCountRule"/>, <see cref="Core.RoomBundleCountPlanner"/>).
        /// A profile stamped before the dial existed has no rule and keeps every pick. Rolls from
        /// its own per-room stream, so the picks and every other stream stay where they were. Extras
        /// skip themes the board already fields (<paramref name="boardThemes"/>).</summary>
        private IReadOnlyList<BundleSpec> ApplyBundleCount(
            string room, IReadOnlyList<BundleSpec> picks, IReadOnlyList<IReadOnlyList<BundleSpec>> positions,
            int seed, Core.ReservedBundleIndices reservedIndices, IReadOnlySet<string> boardThemes)
        {
            if (_difficulty.BundleCount is not Core.BundleCountRule rule)
                return picks;

            var countRng = new Random(seed ^ BundleCountSalt ^ unchecked(StableAuthoredSalt(room) * BundleCountSaltPrime));
            int target = rule.Target(picks.Count, countRng);
            if (target == picks.Count)
                return picks;

            IReadOnlyList<BundleSpec> planned = Core.RoomBundleCountPlanner.Plan(
                picks, positions, target, countRng, reservedIndices.Next, out int shortfall, boardThemes);
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
