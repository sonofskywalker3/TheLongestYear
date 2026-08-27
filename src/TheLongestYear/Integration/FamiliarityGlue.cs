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
            var heartEventsToday = new Dictionary<string, int>();
            foreach (string id in p.eventsSeen)
            {
                if (previous.Contains(id)) continue;
                string npc = RelationshipEventIndex.NpcFor(id);
                if (npc == null) continue;
                heartEventsToday.TryGetValue(npc, out int n);
                heartEventsToday[npc] = n + 1;
            }

            var signals = new List<VillagerDaySignals>();
            foreach (string name in p.friendshipData.Keys)
            {
                Friendship f = p.friendshipData[name];
                heartEventsToday.TryGetValue(name, out int hearts);
                signals.Add(new VillagerDaySignals(name, f.TalkedToToday, f.GiftsToday, hearts));
            }

            int added = FamiliarityRollup.Apply(meta, signals);
            run.EventsSeenAtDayStart = p.eventsSeen.ToList();
            if (added > 0)
            {
                int touched = signals.Count(s => s.Talked || s.Gifts > 0 || s.HeartEvents > 0);
                monitor.Log($"Familiarity rollup: +{added} across {touched} villagers.", LogLevel.Trace);
            }
        }
    }
}
