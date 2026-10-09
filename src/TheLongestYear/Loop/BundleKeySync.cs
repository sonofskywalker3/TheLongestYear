using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Keeps the live bundle keys and the Community Center's own lookups in step with a TLY
    /// Custom board whose rooms changed size (spec 2026-10-09-bundle-count-dial).
    ///
    /// Two vanilla facts make this necessary. <c>NetWorldState.SetBundleData</c> only ever adds or
    /// updates keys, so a bundle the board dropped stays on the room page. And the CommunityCenter
    /// builds its private bundle-to-room lookups once, in its constructor, from whatever
    /// <c>BundleData</c> holds then (on a save load that is the game's default <c>Data/Bundles</c>,
    /// read before the save's own board is applied), so an index outside the defaults is missing
    /// from it and <c>checkForMissedRewards</c> throws KeyNotFoundException.
    ///
    /// The lookups are rebuilt in place by reflection rather than by re-calling
    /// <c>initAreaBundleConversions</c>, which would also add a second set of mutexes and net
    /// fields.</summary>
    internal static class BundleKeySync
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const int AreaCount = 7;

        private static readonly FieldInfo NetBundleDataField = typeof(NetWorldState).GetField("netBundleData", Instance);
        private static readonly FieldInfo AreaToBundleField = typeof(CommunityCenter).GetField("areaToBundleDictionary", Instance);
        private static readonly FieldInfo BundleToAreaField = typeof(CommunityCenter).GetField("bundleToAreaDictionary", Instance);

        /// <summary>Removes every live key in a room <paramref name="board"/> writes that the board
        /// does not contain, with its completion and reward entries. Returns how many keys went.
        /// <paramref name="keepIndicesInUse"/> (the load-time pass) leaves an index the board still
        /// uses under another key alone, so a live bundle's donations survive a reload; a reset
        /// clears it, because the reset wipes completion anyway and the write rebuilds it.</summary>
        public static int RemoveStaleKeys(IReadOnlyDictionary<string, string> board, IMonitor monitor, bool keepIndicesInUse = false)
        {
            NetWorldState worldState = Game1.netWorldState?.Value;
            if (worldState == null || board == null || board.Count == 0)
                return 0;
            if (NetBundleDataField?.GetValue(worldState) is not NetStringDictionary<string, NetString> netBundleData)
            {
                monitor?.Log("BundleKeySync: NetWorldState.netBundleData not found; stale bundles were not removed.", LogLevel.Warn);
                return 0;
            }

            StaleBundleKeys.Result stale = StaleBundleKeys.Find(board, netBundleData.Keys.ToList());
            if (stale.Keys.Count == 0)
                return 0;

            foreach (string key in stale.Keys)
                netBundleData.Remove(key);
            // Cleared so the write rebuilds them at the new bundle's size (SetBundleData only ever
            // grows an existing array). Completion is wiped at a reset anyway, and a dropped
            // bundle's progress has nowhere to go.
            HashSet<int> inUse = keepIndicesInUse
                ? new HashSet<int>(StaleBundleKeys.IndexToRoom(board.Keys).Keys)
                : new HashSet<int>();
            foreach (int index in stale.Indices.Where(i => !inUse.Contains(i)))
            {
                worldState.Bundles.Remove(index);
                worldState.BundleRewards.Remove(index);
            }
            monitor?.Log(
                $"BundleKeySync: removed {stale.Keys.Count} bundle(s) the board no longer has: {string.Join(", ", stale.Keys)}.",
                LogLevel.Info);
            return stale.Keys.Count;
        }

        /// <summary>Gives every board key a completion and reward entry (SetBundleData does this for
        /// keys it writes; a key re-added by the game's defaults may lack them after a removal).</summary>
        public static void EnsureCompletionEntries()
        {
            NetWorldState worldState = Game1.netWorldState?.Value;
            worldState?.SetBundleData(new Dictionary<string, string>(worldState.BundleData));
        }

        /// <summary>Rebuilds the Community Center's bundle-to-room lookups and its ingredient cache
        /// from the live board.</summary>
        public static void RefreshCommunityCenter(IMonitor monitor)
        {
            if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter cc)
                return;
            Dictionary<string, string> bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null)
                return;

            if (AreaToBundleField?.GetValue(cc) is Dictionary<int, List<int>> areaToBundle
                && BundleToAreaField?.GetValue(cc) is Dictionary<int, int> bundleToArea)
            {
                areaToBundle.Clear();
                bundleToArea.Clear();
                for (int area = 0; area < AreaCount; area++)
                    areaToBundle[area] = new List<int>();
                foreach (KeyValuePair<int, string> entry in StaleBundleKeys.IndexToRoom(bundleData.Keys))
                {
                    int area = CommunityCenter.getAreaNumberFromName(entry.Value);
                    if (area < 0)
                        continue;
                    if (!areaToBundle.TryGetValue(area, out List<int> list))
                        areaToBundle[area] = list = new List<int>();
                    list.Add(entry.Key);
                    bundleToArea[entry.Key] = area;
                }
            }
            else
            {
                monitor?.Log("BundleKeySync: CommunityCenter bundle lookups not found; the room lookups were not refreshed.", LogLevel.Warn);
            }
            cc.refreshBundlesIngredientsInfo();
        }

        /// <summary>The load-time pass for an engine board: drop keys the stored board does not have,
        /// make sure every key has its entries, and refresh the CC. Host only.</summary>
        public static void SyncToStoredBoard(IReadOnlyDictionary<string, string> board, IMonitor monitor)
        {
            if (board == null || board.Count == 0 || !Context.IsMainPlayer)
                return;
            if (RemoveStaleKeys(board, monitor, keepIndicesInUse: true) > 0)
                EnsureCompletionEntries();
            RefreshCommunityCenter(monitor);
        }
    }
}
