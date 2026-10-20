using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Museum;
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
            bool resendBetterStartGift,
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
                catch (Exception ex)
                {
                    // No event data file for this location (expected for most maps).
                    PatchLog.Trace($"Data/Events/{loc}: not loaded ({ex.GetType().Name}); skipped.");
                    continue;
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
            // Museum reward markers (Data/MuseumRewards "MarkEventSeen"): the rewind empties the
            // museum and the reward pays out again, so its marker must not be re-seeded either. The
            // 60-donation marker 295672 gates Gunther's Rusty Key visit (Farm 66); re-seeded, the
            // visit played on the next loop's first morning and handed the key back unbought.
            int markers = 0;
            foreach (string id in LoadMuseumRewardMarkers(content))
            {
                if (exclude != null && exclude.Contains(id)) continue;
                if (_ids.Add(id)) markers++;
            }
            // Trigger action markers (Data/TriggerActions "MarkEventSeen"): the rewind clears the
            // trigger-action record so those rows fire again; re-seeding their marker would let a
            // scene gated on it play early next loop.
            int triggerMarkers = 0;
            foreach (string id in LoadTriggerActionMarkers(content, resendBetterStartGift))
            {
                if (exclude != null && exclude.Contains(id)) continue;
                if (_ids.Add(id)) triggerMarkers++;
            }
            int grants = _ids.Count;
            // Chain roots: relationship-gated scenes (skipped by the re-seed, so they replay) and
            // the vanilla always-replayable ids. A scene gated "e <root>" replays with its root.
            var roots = new HashSet<string>(RelationshipEventIndex.Ids, StringComparer.Ordinal);
            roots.UnionWith(baseReplayableIds);
            EventGatingTables.PropagateChains(keys, _ids, roots, exclude);
            monitor.Log(
                $"Replayable-cutscene scan: flagged {grants} unlock-granting event id(s) " +
                $"({markers} museum reward marker(s), {triggerMarkers} trigger action marker(s)) and " +
                $"{_ids.Count - grants} chained follow-up(s) across {locationNames.Count} location(s).",
                LogLevel.Trace);
        }

        /// <summary>Event ids the museum rewards mark seen, read from the live Data/MuseumRewards.</summary>
        private static IEnumerable<string> LoadMuseumRewardMarkers(IGameContentHelper content)
        {
            Dictionary<string, MuseumRewards> rewards;
            try
            {
                rewards = content.Load<Dictionary<string, MuseumRewards>>("Data/MuseumRewards");
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"Data/MuseumRewards: not loaded ({ex.GetType().Name}); no reward markers.");
                return Array.Empty<string>();
            }
            if (rewards == null) return Array.Empty<string>();
            var lists = new List<IEnumerable<string>>();
            foreach (MuseumRewards reward in rewards.Values)
                lists.Add(reward?.RewardActions);
            return MuseumRewardMarkers.EventIds(lists);
        }

        /// <summary>Event ids the trigger actions mark seen, read from the live Data/TriggerActions.</summary>
        private static IEnumerable<string> LoadTriggerActionMarkers(IGameContentHelper content, bool resendBetterStartGift)
        {
            List<TriggerActionData> rows;
            try
            {
                rows = content.Load<List<TriggerActionData>>("Data/TriggerActions");
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"Data/TriggerActions: not loaded ({ex.GetType().Name}); no trigger action markers.");
                return Array.Empty<string>();
            }
            if (rows == null) return Array.Empty<string>();
            var parsed = new List<TriggerActionMarkers.Row>(rows.Count);
            foreach (TriggerActionData row in rows)
                if (row != null)
                    parsed.Add(new TriggerActionMarkers.Row(row.Id, row.Action, row.Actions));
            return TriggerActionMarkers.EventIds(parsed, resendBetterStartGift);
        }
    }
}
