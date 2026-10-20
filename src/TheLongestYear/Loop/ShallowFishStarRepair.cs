using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Load-time repair for boards rolled before 0.19.121: a silver or gold ask for a fish
    /// that only bites in shallow water (<see cref="ItemPools.ShallowWaterFishIds"/>, the Forest
    /// waterfall pool's Goby) drops to base quality, on the live board and on the stored board of
    /// record, the way <see cref="BoardRepairService.ClampUnstackableAsks"/> rewrites both. The fish
    /// stays on the board: it can be caught, only the star could not. Host only; a donated slot
    /// loses nothing, since a plainer ask for an item already handed in changes nothing.</summary>
    internal static class ShallowFishStarRepair
    {
        /// <summary>Returns the number of bundles rewritten.</summary>
        public static int Run(IMonitor monitor, MetaState state, ItemPools pools)
        {
            if (!Context.IsMainPlayer) return 0;
            IReadOnlySet<string> shallow = pools?.ShallowWaterFishIds;
            if (shallow == null || shallow.Count == 0) return 0;
            monitor?.Log($"Fish depth: only shallow water for {string.Join(", ", shallow.OrderBy(id => id, StringComparer.Ordinal))}; they are never asked for with a star.", LogLevel.Trace);

            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return 0;

            if (state?.WrittenBoard != null)
                foreach (string key in state.WrittenBoard.Keys.ToList())
                {
                    string stored = ShallowFishStars.RepairBundleValue(state.WrittenBoard[key], shallow);
                    if (stored != null) state.WrittenBoard[key] = stored;
                }

            var updates = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in worldState.BundleData)
            {
                string repaired = ShallowFishStars.RepairBundleValue(entry.Value, shallow);
                if (repaired == null) continue;
                updates[entry.Key] = repaired;
                monitor?.Log(
                    $"Board repair: '{entry.Key}' asked for {string.Join(", ", ShallowFishStars.LoweredIds(entry.Value, shallow))} " +
                    "with a quality star, which that fish never gets where it bites; lowered to base quality.",
                    LogLevel.Info);
            }
            if (updates.Count == 0) return 0;
            worldState.SetBundleData(updates);
            (Game1.getLocationFromName("CommunityCenter") as CommunityCenter)?.refreshBundlesIngredientsInfo();
            return updates.Count;
        }
    }
}
