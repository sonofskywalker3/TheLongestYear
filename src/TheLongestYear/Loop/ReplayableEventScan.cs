using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Runtime-built set of event ids that GRANT a run-wipe-able unlock (recipe / mail flag / quest)
    /// and therefore must re-fire each loop — the general analogue of the hardcoded vanilla furnace/cave
    /// ids in <see cref="EventGatingTables.Default"/>. Scanned from the live save's Data/Events at
    /// SaveLoaded (so a MOD's teach/unlock cutscene replays too), then OR'd with Default by
    /// <see cref="FarmerReset"/> when re-seeding eventsSeen. Cleared on deactivate. The detection
    /// predicate + collection are pure in Core (unit-tested); this is the content-loading shell.
    /// </summary>
    internal static class ReplayableEventScan
    {
        private static HashSet<string> _ids = new(StringComparer.Ordinal);

        /// <summary>True if the scan flagged this event id as a wipe-able unlock grant.</summary>
        public static bool IsReplayable(string eventId) => _ids.Contains(eventId);

        /// <summary>Drop the scan (deactivate / non-TLY save / return to title).</summary>
        public static void Clear() => _ids = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Rebuild the flagged-id set from the live save's events. <paramref name="enabled"/> = false
        /// (config kill-switch) leaves the set empty so only Default's vanilla ids apply.
        /// </summary>
        public static void Populate(
            IGameContentHelper content,
            IEnumerable<GameLocation> liveLocations,
            IEnumerable<string> baseReplayableIds,
            ISet<string> exclude,
            bool enabled,
            IMonitor monitor)
        {
            if (!enabled)
            {
                Clear();
                monitor.Log(
                    "Replayable-cutscene auto-detection disabled (config) — only vanilla furnace/cave ids replay.",
                    LogLevel.Trace);
                return;
            }

            // Primary source: every loaded location's name. Using the live world (not a hardcoded list)
            // covers mod-added locations such as SVE's Custom_AdventurerSummit. Deduped.
            var locationNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameLocation loc in liveLocations)
                if (!string.IsNullOrEmpty(loc?.Name))
                    locationNames.Add(loc.Name);

            var events = new List<(string id, string script)>();
            var keys = new List<(string id, string key)>();
            foreach (string loc in locationNames)
            {
                Dictionary<string, string> data;
                try
                {
                    data = content.Load<Dictionary<string, string>>($"Data/Events/{loc}");
                }
                catch (Exception)
                {
                    continue; // no event data file for this location
                }
                if (data == null) continue;

                foreach (KeyValuePair<string, string> kv in data)
                {
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    events.Add((id, kv.Value ?? ""));
                    keys.Add((id, kv.Key));
                }
            }

            _ids = EventGatingTables.CollectReplayableIds(events, baseReplayableIds, exclude);
            int grants = _ids.Count;
            // Chain roots: relationship-gated scenes (skipped by the re-seed, so they replay) and
            // the vanilla always-replayable ids. A scene gated "e <root>" replays with its root.
            var roots = new HashSet<string>(RelationshipEventIndex.Ids, StringComparer.Ordinal);
            roots.UnionWith(baseReplayableIds);
            EventGatingTables.PropagateChains(keys, _ids, roots, exclude);
            monitor.Log(
                $"Replayable-cutscene scan: flagged {grants} unlock-granting event id(s) and " +
                $"{_ids.Count - grants} chained follow-up(s) across {locationNames.Count} location(s).",
                LogLevel.Trace);
        }
    }
}
