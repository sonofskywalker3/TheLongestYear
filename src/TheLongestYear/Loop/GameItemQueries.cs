using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Internal;

namespace TheLongestYear.Loop
{
    /// <summary>Item queries resolved through the game's own ItemQueryResolver, so a line that sells or
    /// spawns by context tag (a PerItemCondition) is read exactly as the game reads it.</summary>
    internal static class GameItemQueries
    {
        private const string RandomItemsQuery = "RANDOM_ITEMS";
        private const string AllItemsQuery = "ALL_ITEMS";

        /// <summary>True when a data line's query names a set the game can list in full once its
        /// PerItemCondition is applied: <c>ALL_ITEMS</c> or <c>RANDOM_ITEMS</c> with a condition.</summary>
        public static bool IsFilteredSet(string query, string perItemCondition)
        {
            if (string.IsNullOrWhiteSpace(perItemCondition) || string.IsNullOrWhiteSpace(query)) return false;
            string key = query.Trim().Split(' ', 2)[0];
            return key == RandomItemsQuery || key == AllItemsQuery;
        }

        /// <summary>True when the line hands out only some of its set (a random pick), so a route from
        /// it is luck.</summary>
        public static bool IsRandomPick(string query, int? maxItems)
            => maxItems != null || query.Trim().StartsWith(RandomItemsQuery, StringComparison.Ordinal);

        /// <summary>Every qualified item id the query offers with <paramref name="perItemCondition"/>
        /// applied, or null when the game could not resolve it (logged at Trace; one bad line from one
        /// mod must not cost the whole read).</summary>
        public static List<string> Resolve(string query, string perItemCondition, IMonitor monitor, string reader)
        {
            var ids = new List<string>();
            try
            {
                var results = ItemQueryResolver.TryResolve(
                    query, new ItemQueryContext(), ItemQuerySearchMode.All, perItemCondition,
                    logError: (q, error) => monitor?.Log($"{reader}: item query '{q}' failed: {error}", LogLevel.Trace));
                foreach (var result in results)
                    if ((result?.Item as Item)?.QualifiedItemId is string qualified)
                        ids.Add(qualified);
            }
            catch (Exception ex)
            {
                monitor?.Log($"{reader}: could not resolve item query '{query}' ({ex.GetType().Name}: {ex.Message}); skipped.", LogLevel.Trace);
                return null;
            }
            return ids;
        }
    }
}
