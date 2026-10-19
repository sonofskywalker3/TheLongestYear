using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core
{
    /// <summary>A marriage or roommate arrangement ends at the rewind (user ruling 2026-10-19): the
    /// world goes back to year one and every relationship starts over. These are the pure parts of
    /// that clear: who was a partner, which mod-data keys belong to a marriage, and which maps are
    /// a farmer's home (where a former spouse must not stay).</summary>
    public static class MarriageResetRules
    {
        /// <summary>Fragments of a mod-data key that mark marriage state. Polyamory mods keep extra
        /// spouses outside Farmer.spouse (PolySweet writes "&lt;mod&gt;/WeddingDate" on each spouse
        /// NPC, "SpouseNeedsPlaced" on a spouse it warped home, "&lt;mod&gt;/OtherParent" on children),
        /// so match by meaning rather than by one mod's exact names.</summary>
        private static readonly string[] MarriageKeyFragments =
            { "spouse", "wedding", "married", "marriage", "roommate", "otherparent" };

        /// <summary>The vanilla farmhouse, the island farmhouse, and every multiplayer cabin.</summary>
        private const string FarmHouseMap = "FarmHouse";
        private const string IslandFarmHouseMap = "IslandFarmHouse";
        private const string CabinPrefix = "Cabin";

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
        {
            if (string.IsNullOrEmpty(key))
                return false;
            foreach (string fragment in MarriageKeyFragments)
            {
                if (key.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Whether a location name is a farmer's home, where only a spouse lives.</summary>
        public static bool IsFarmerHomeMap(string map)
        {
            if (string.IsNullOrEmpty(map))
                return false;
            return map == FarmHouseMap
                || map == IslandFarmHouseMap
                || map.StartsWith(CabinPrefix, StringComparison.Ordinal);
        }
    }
}
