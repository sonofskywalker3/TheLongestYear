using System;
using System.Collections.Generic;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Classifies vanilla per-farmer <c>Stats.Values</c> keys for the loop reset.
    /// WIPE-BY-DEFAULT (user ruling 2026-07-10, reversing the 0.11.24 allow-list): any key not
    /// on the explicit keep-list below is removed at reset, so a future vanilla or mod
    /// progression stat (the <c>Book_*</c>/<c>mastery_*</c> class that kept leaking) can never
    /// silently survive a loop. The keep-list was classified ONCE against the complete 1.6.15
    /// <c>StatKeys</c> universe (decompile StatKeys.cs) — a missed future key over-wipes a
    /// cosmetic counter (recoverable direction for a roguelite) instead of leaking power.
    /// </summary>
    public static class StatResetRules
    {
        // Keys that SURVIVE the reset. Three classes:
        //
        //  ENGINE — daysPlayed: event preconditions + daily RNG seeding; the reset re-establishes
        //  it explicitly (WorldResetService sets DaysPlayed = 1), keeping it here is ordering
        //  defense so the wipe can never race that write. averageBedtime: rolling average, no
        //  run leverage.
        //
        //  RNG-SEQUENCE COUNTERS — timesEnchanted / geodesCracked / MysteryBoxesOpened seed
        //  WHICH enchant/geode-drop/box-drop comes next (e.g. BaseEnchantment.cs:127 seeds on
        //  timesEnchanted). Wiping them would restart the exact same drop sequence every loop —
        //  memorizable and scummable. They grant no power, only variety; they stay.
        //
        //  LIFETIME TALLIES — cosmetic production/found/activity counters (steps taken, crops
        //  shipped, …). The user never approved wiping these; none of them gate in-run content.
        //
        // Falls out WIPED by default (the old allow-list, now implicit): Book_*, mastery_*,
        // MasteryExp, masteryLevelsSpent, trinketSlots, ticketPrizesClaimed,
        // specialOrderPrizeTickets — plus run-relevant ladders the allow-list had missed:
        // BillboardQuestsDone + GoldenTagsTurnedIn (prize/derby reward ladders),
        // blessingOfWaters (day-scoped statue blessing), individualMoneyEarned (money is
        // run-scoped), SquidFestScore_<day>_<year> — and every unknown future key.
        private static readonly HashSet<string> KeptKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            // Engine.
            "daysPlayed",
            "averageBedtime",

            // RNG-sequence counters (see class comment).
            "timesEnchanted",
            "geodesCracked",
            "MysteryBoxesOpened",

            // Lifetime tallies.
            "beachFarmSpawns",
            "beveragesMade",
            "boatRidesToIsland",
            "caveCarrotsFound",
            "cheeseMade",
            "chickenEggsLayed",
            "childrenTurnedToDoves",
            "completedJunimoKart",
            "completedPrairieKing",
            "completedPrairieKingWithoutDying",
            "copperFound",
            "cowMilkProduced",
            "cropsShipped",
            "diamondsFound",
            "dirtHoed",
            "duckEggsLayed",
            "exMemoriesWiped",
            "fishCaught",
            "giftsGiven",
            "goatCheeseMade",
            "goatMilkProduced",
            "goldFound",
            "goodFriends",
            "hardModeMonstersKilled",
            "iridiumFound",
            "ironFound",
            "itemsCooked",
            "itemsCrafted",
            "itemsForaged",
            "itemsShipped",
            "monstersKilled",
            "mossHarvested",
            "mysticStonesCrushed",
            "notesFound",
            "otherPreciousGemsFound",
            "piecesOfTrashRecycled",
            "preservesMade",
            "prismaticShardsFound",
            "questsCompleted",
            "rabbitWoolProduced",
            "rocksCrushed",
            "seedsSown",
            "sheepWoolProduced",
            "slimesKilled",
            "stepsTaken",
            "stoneGathered",
            "stumpsChopped",
            "timesFished",
            "timesUnconscious",
            "totalMoneyGifted",
            "trashCansChecked",
            "trufflesFound",
            "weedsEliminated",
            "wildtreesplanted",
        };

        public static bool IsRunScoped(string statKey)
        {
            if (string.IsNullOrEmpty(statKey))
                return false;
            return !KeptKeys.Contains(statKey);
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
