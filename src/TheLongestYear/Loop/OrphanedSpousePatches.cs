using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.SaveMigrations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Keeps a save with a left-over spouse on the load screen. The game's pre-1.3 friendship
    /// migration runs for every farmer the load screen lists and reads
    /// <c>friendshipData[spouse]</c> without checking the entry exists. Before 0.19.117 the rewind
    /// wiped the friendships but left <c>Farmer.spouse</c>, so a proposal on the last day of a loop
    /// left a save the load screen could not read: it threw, and the save vanished from the list.
    /// The migration has nothing to do for such a farmer, so skip it; once the save loads,
    /// <c>FarmerReset.RepairOrphanedMarriage</c> clears the name and sends the villager home.
    /// Runs at the title screen, before any save is active, so it cannot be gated on the run.
    /// </summary>
    [HarmonyPatch(typeof(SaveMigrator_1_3), nameof(SaveMigrator_1_3.MigrateFriendshipData))]
    internal static class OrphanedSpouseLoadPatch
    {
        public static IMonitor Monitor;

        private static bool Prefix(Farmer player)
        {
            try
            {
                if (player == null) return true;
                string spouse = player.spouse;
                bool pre13 = player.obsolete_friendships != null && player.friendshipData.Length == 0;
                bool hasEntry = !string.IsNullOrEmpty(spouse)
                    && player.friendshipData.ContainsKey(MarriageResetRules.MigratedSpouseName(spouse));
                if (!MarriageResetRules.SpouseMigrationWouldFail(spouse, pre13, hasEntry)) return true;
                Monitor?.Log($"Save for {player.Name}: {spouse} is recorded as the spouse with no friendship behind it; " +
                    "skipped the game's old friendship migration so the save can still be read.", LogLevel.Trace);
                return false;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Left-over spouse check failed; the game's own migration runs.\n{ex}", LogLevel.Trace);
                return true;
            }
        }
    }

    /// <summary>
    /// The same left-over spouse breaks the morning: <c>NPC.marriageDuties</c> reads the farmer's
    /// friendship with the spouse and throws when there is none, which stops
    /// <c>Game1.OnDayStarted</c> part way (later villagers and every animal miss their day start).
    /// On load that runs before SaveLoaded, so before the repair can clear the name. The check
    /// is the one value the game goes on to read unguarded (<c>GetSpouseFriendship</c>), so a
    /// real marriage of any kind is never skipped.
    /// </summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.marriageDuties))]
    internal static class OrphanedSpouseDutiesPatch
    {
        private static bool Prefix(NPC __instance)
        {
            try
            {
                Farmer spouse = __instance.getSpouse();
                return spouse == null || spouse.GetSpouseFriendship() != null;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
