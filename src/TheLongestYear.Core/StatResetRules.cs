using System;
using System.Collections.Generic;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Classifies vanilla per-farmer <c>Stats.Values</c> keys as run-scoped: progress earned
    /// inside a single loop that the reset must wipe, or it leaks across runs (2026-07-09
    /// reset-leak audit, Dusklight7's report). Everything not matched here is left alone —
    /// lifetime counters (steps taken, crops shipped, …) are cosmetic and persisting them is
    /// harmless, so the rule set is a targeted allow-list, not a blanket stats wipe.
    /// </summary>
    public static class StatResetRules
    {
        // Key families (verified against the Android decompile's StatKeys.cs):
        //  - "Book_*"      — 1.6 power books; each key grants a permanent passive once read.
        //  - "mastery_*"   — claimed mastery perks (mastery_4 gates the trinket slots).
        private static readonly string[] RunScopedPrefixes = { "Book_", "mastery_" };

        private static readonly HashSet<string> RunScopedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "MasteryExp",               // mastery progress bar (MasteryTrackerMenu reads it live)
            "masteryLevelsSpent",       // claims already spent at the mastery pedestal
            "ticketPrizesClaimed",      // prize-ticket machine ladder position (Lewis' house)
            "specialOrderPrizeTickets", // unclaimed prize tickets the machine decrements
            "trinketSlots",             // combat-mastery trinket slot — InventoryPage gates the
                                        // slot on THIS stat, not on mastery_4 (2026-07-10 review)
        };

        public static bool IsRunScoped(string statKey)
        {
            if (string.IsNullOrEmpty(statKey))
                return false;
            if (RunScopedKeys.Contains(statKey))
                return true;
            foreach (string prefix in RunScopedPrefixes)
            {
                if (statKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>The subset of <paramref name="keys"/> the reset should remove. Materialized
        /// so callers can enumerate it while mutating the source dictionary.</summary>
        public static List<string> SelectRunScoped(IEnumerable<string> keys)
        {
            var selected = new List<string>();
            foreach (string key in keys)
            {
                if (IsRunScoped(key))
                    selected.Add(key);
            }
            return selected;
        }
    }
}
