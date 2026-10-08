using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The live Community Center board for <see cref="TechBoardOfRecord"/>: reads
    /// <c>NetWorldState.BundleData</c> and writes through <c>SetBundleData</c>, then refreshes the
    /// CC's ingredient cache, which SetBundleData does not do (same write as
    /// <see cref="BoardRepairService.ClampUnstackableAsks"/>).</summary>
    internal sealed class LiveBundleBoard : ILiveBundleBoard
    {
        public IReadOnlyDictionary<string, string> Read()
            => Game1.netWorldState?.Value?.BundleData
               ?? new Dictionary<string, string>(StringComparer.Ordinal);

        public void Write(IReadOnlyDictionary<string, string> updates)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState == null) return;
            worldState.SetBundleData(new Dictionary<string, string>(updates, StringComparer.Ordinal));
            (Game1.getLocationFromName("CommunityCenter") as CommunityCenter)?.refreshBundlesIngredientsInfo();
        }
    }
}
