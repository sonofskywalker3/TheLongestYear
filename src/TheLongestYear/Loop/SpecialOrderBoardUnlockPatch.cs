using HarmonyLib;
using StardewValley;
using StardewValley.SpecialOrders;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Force <c>SpecialOrder.IsSpecialOrdersBoardUnlocked()</c> to return true so the
    /// Town's Special Orders board (Lewis's monthly quests — Gus's giant omelette, Robin's
    /// wood collection, etc.) is repaired and active from Spring 1 of every TLY loop.
    ///
    /// Vanilla gates the board on <c>Game1.stats.DaysPlayed &gt;= 58</c> (≈Fall 2 Y1).
    /// TLY's <see cref="WorldResetService"/> resets <c>DaysPlayed = 1</c> on every loop
    /// (Spring 1 starts at DaysPlayed = 1), so without this patch the board never appears
    /// — the user reported "I don't see the broken-down sign below Lewis's house" during
    /// the 2026-05-28 playtest.
    ///
    /// Side effects: with the unlock active from day 1, <see cref="Town"/>.resetForPlayerEntry
    /// repaints the board tiles at (60-63, 93) on the player's first warp to Town each loop
    /// (Town.cs:519-539), the gold-star tile flips at line 1083 when an order is available,
    /// and <c>Game1.player.team.availableSpecialOrders</c> populates on Monday rotation.
    /// All independent of the TLY weekly theme layer.
    ///
    /// Mirrors <see cref="CcLocationAccessiblePatch"/>'s approach: patch the gate function
    /// directly rather than mutate world state (DaysPlayed bumps would cascade into mail
    /// timing, NPC schedules, achievements, etc.).
    /// </summary>
    [HarmonyPatch(typeof(SpecialOrder), nameof(SpecialOrder.IsSpecialOrdersBoardUnlocked))]
    internal static class SpecialOrderBoardUnlockPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
