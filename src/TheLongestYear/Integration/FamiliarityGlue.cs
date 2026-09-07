using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>Nightly deja-vu rollup (spec 2026-08-27): reads today's talk/gift flags off the live
    /// friendship data and new heart events off eventsSeen (by difference with the last snapshot),
    /// then hands the pure rollup the numbers. Runs from RunController.OnDayEnding, before vanilla's
    /// own day-end resets those flags.</summary>
    internal static class FamiliarityGlue
    {
        public static void Rollup(MetaState meta, RunState run, IMonitor monitor)
        {
            Farmer p = Game1.player;
            if (p == null) return;

            var previous = new HashSet<string>(run.EventsSeenAtDayStart);
            var heartEventsToday = new Dictionary<string, List<string>>();
            foreach (string id in p.eventsSeen)
            {
                if (previous.Contains(id)) continue;
                string npc = RelationshipEventIndex.NpcFor(id);
                if (npc == null) continue;
                if (!heartEventsToday.TryGetValue(npc, out List<string> ids))
                    heartEventsToday[npc] = ids = new List<string>();
                ids.Add(id);
            }

            string seasonKey = Game1.currentSeason;   // "spring" .. "winter", the same key NPC.Birthday_Season uses
            int day = Game1.dayOfMonth;
            var signals = new List<VillagerDaySignals>();
            foreach (string name in p.friendshipData.Keys)
            {
                Friendship f = p.friendshipData[name];
                heartEventsToday.TryGetValue(name, out List<string> ids);
                NPC npc = Game1.getCharacterFromName(name);
                bool birthday = npc != null
                    && string.Equals(npc.Birthday_Season, seasonKey, System.StringComparison.OrdinalIgnoreCase)
                    && npc.Birthday_Day == day;
                signals.Add(new VillagerDaySignals(name, f.TalkedToToday, f.GiftsToday, ids?.Count ?? 0,
                    BirthdayGift: birthday && f.GiftsToday > 0, HeartEventIds: ids));
            }

            int loopNumber = meta.CompletedResets + 1;
            int added = FamiliarityRollup.Apply(meta, signals, loopNumber);
            run.EventsSeenAtDayStart = p.eventsSeen.ToList();
            if (added > 0)
            {
                int touched = signals.Count(s => s.Talked || s.Gifts > 0 || s.HeartEvents > 0);
                monitor.Log($"Familiarity rollup: +{added} across {touched} villagers.", LogLevel.Trace);
            }
        }
    }
}
