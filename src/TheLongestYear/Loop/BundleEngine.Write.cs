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
        /// <summary>Requirements manifest with data-derived season pins merged UNDER the
        /// caller's pins (hand-curated defaults + user config always win; derived pins only
        /// fill gaps for items the curated table has never seen — e.g. re-rolled or modded
        /// ingredients). Call after Generate.</summary>
        public IReadOnlyList<BundleRequirement> BuildRequirements(
            GeneratedBundleSet set,
            IReadOnlyDictionary<string, Core.Season> basePins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            Core.ItemAvailabilityModel availability = null)
        {
            var merged = new Dictionary<string, Core.Season>(LastDerivedSeasonPins, StringComparer.Ordinal);
            foreach (KeyValuePair<string, Core.Season> pin in basePins)
                merged[pin.Key] = pin.Value;
            return set.BuildRequirements(merged, bundleQuotas, availability);
        }

        /// <summary>Writes the generated set into <c>Game1.netWorldState</c> and re-syncs the CC
        /// location. See the class doc comment for the merge-vs-replace finding this handles.</summary>
        public void WriteToWorld(GeneratedBundleSet set, IMonitor monitor)
        {
            Dictionary<string, string> newData = new Dictionary<string, string>(set.ToBundleData());

            // SetBundleData is MERGE/ADDITIVE, not a replace (NetWorldState.cs: SetBundleData ->
            // netBundleData.CopyFrom(data), and NetDictionary.CopyFrom only upserts keys present
            // in `data` -- it never removes a key that isn't). Since the bundle-count dial (spec
            // 2026-10-09) a room can hold fewer bundles than the last board or the game's defaults,
            // so the keys this board no longer has are removed first. With the dial at Normal on a
            // vanilla game the key space is the same every loop and this removes nothing.
            BundleKeySync.RemoveStaleKeys(newData, monitor);
            Game1.netWorldState.Value.SetBundleData(newData);
            // The CC's bundle-to-room lookups were built from whatever board it was constructed
            // with; an extra bundle's reserved index is not in them. Before the map pass below,
            // which reads them to decide where a note shows.
            BundleKeySync.RefreshCommunityCenter(monitor);

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
    }
}
