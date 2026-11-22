using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core
{
    /// <summary>A marriage or roommate arrangement ends at the rewind (user ruling 2026-10-19): the
    /// world goes back to year one and every relationship starts over. These are the pure parts of
    /// that clear: who was a partner, which mod-data keys belong to a marriage, and when a saved
    /// spouse name is a leftover.</summary>
    public static class MarriageResetRules
    {
        /// <summary>Mod-data keys that record marriage state. Vanilla keeps none (its marriage
        /// lives in Farmer.spouse and friendshipData); these are PolySweet's
        /// (ApryllForever.PolyamorySweetLove): the wedding date on each spouse NPC, the "warped home"
        /// marker on a spouse, and the other parent on a child. Exact keys only, so another mod's
        /// unrelated data is never touched.</summary>
        private static readonly HashSet<string> MarriageModDataKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "ApryllForever.PolyamorySweetLove/WeddingDate",
            "SpouseNeedsPlaced",
            "ApryllForever.PolyamorySweetLove/OtherParent",
        };

        /// <summary>Everyone the farmer was married, engaged or roommates to: the official spouse
        /// plus any friendship marked married or engaged (a polyamory mod's extra spouses).</summary>
        public static IReadOnlyList<string> FormerPartners(string spouse,
            IEnumerable<(string Name, bool MarriedOrEngaged)> friendships)
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(spouse))
                names.Add(spouse);
            foreach ((string name, bool partner) in friendships)
            {
                if (partner && !string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.Ordinal))
                    names.Add(name);
            }
            return names;
        }

        /// <summary>Whether a Farmer or NPC mod-data key records marriage state.</summary>
        public static bool IsMarriageModDataKey(string key)
            => !string.IsNullOrEmpty(key) && MarriageModDataKeys.Contains(key);

        /// <summary>A spouse name with no Married, Engaged or roommate friendship behind it: left
        /// over from an earlier loop on a save hit before the rewind cleared Farmer.spouse. A real
        /// marriage keeps its status, so it never counts.</summary>
        public static bool IsOrphanedSpouse(string spouse, bool hasFriendship, bool marriedEngagedOrRoommate)
            => !string.IsNullOrEmpty(spouse) && !(hasFriendship && marriedEngagedOrRoommate);

        /// <summary>The villager name the game's pre-1.3 friendship migration looks up for a saved
        /// spouse value (old saves wrote an engagement as the name plus "engaged").</summary>
        public static string MigratedSpouseName(string spouse)
            => string.IsNullOrEmpty(spouse) ? spouse : spouse.Replace("engaged", "");

        /// <summary>Whether that migration would throw on this farmer: it indexes the friendship
        /// list by the spouse name without checking, and it runs for every save the load screen
        /// lists. A spouse with no friendship entry (the rewind bug before 0.19.117) makes the
        /// save vanish from the list. An old save still carrying pre-1.3 friendships is left to
        /// the game.</summary>
        public static bool SpouseMigrationWouldFail(string spouse, bool hasPre13Friendships, bool hasFriendshipEntry)
            => !string.IsNullOrEmpty(spouse) && !hasPre13Friendships && !hasFriendshipEntry;
    }
}
