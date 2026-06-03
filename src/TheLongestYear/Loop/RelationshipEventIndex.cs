using System;
using System.Collections.Generic;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Identifies vanilla relationship/heart events — those whose Data/Events key carries a
    /// friendship precondition ("f &lt;npc&gt; &lt;points&gt;"). These must RESET each loop: the player
    /// rebuilds every friendship from zero on a reset, so a heart event watched in a prior run should
    /// be re-watchable as the relationship is rebuilt. <see cref="FarmerReset"/> therefore excludes
    /// these ids from the cross-loop seen re-seed rather than preserving them.
    ///
    /// Built once from content (lazily) and cached. The friendship gate is detected per the vanilla
    /// event-precondition format: a "/"-separated key segment beginning with "f " (f-space).
    /// </summary>
    internal static class RelationshipEventIndex
    {
        // Vanilla event-bearing locations. Missing files are skipped (try/catch per location).
        private static readonly string[] Locations =
        {
            "Farm", "FarmHouse", "Town", "Mountain", "Beach", "Forest", "BusStop", "Backwoods",
            "Railroad", "Saloon", "SeedShop", "Blacksmith", "AnimalShop", "Hospital", "ScienceHouse",
            "JoshHouse", "HaleyHouse", "SamHouse", "ElliottHouse", "LeahHouse", "Tent", "Trailer",
            "ManorHouse", "WizardHouse", "Sewer", "Mine", "Tunnel", "Woods", "CommunityCenter",
            "ArchaeologyHouse", "FishShop", "Sunroom", "AdventureGuild", "Greenhouse", "Cellar",
            "Desert", "Summit", "Club",
        };

        private static HashSet<string> _ids;

        /// <summary>Friendship-gated event ids, built lazily from Data/Events and cached.</summary>
        public static HashSet<string> Ids => _ids ??= Build();

        public static bool Contains(string eventId) => Ids.Contains(eventId);

        private static HashSet<string> Build()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string loc in Locations)
            {
                Dictionary<string, string> data;
                try
                {
                    data = Game1.content.Load<Dictionary<string, string>>($"Data/Events/{loc}");
                }
                catch (Exception)
                {
                    continue; // no event data file for this location
                }
                if (data == null) continue;

                foreach (string key in data.Keys)
                {
                    string[] segs = key.Split('/');
                    if (segs.Length == 0) continue;
                    for (int i = 1; i < segs.Length; i++)
                    {
                        // A friendship precondition segment is "f <npc> <points>".
                        if (segs[i].StartsWith("f ", StringComparison.Ordinal))
                        {
                            set.Add(segs[0]);
                            break;
                        }
                    }
                }
            }
            return set;
        }
    }
}
