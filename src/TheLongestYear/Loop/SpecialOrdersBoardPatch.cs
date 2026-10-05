using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SpecialOrders;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Keep Special Orders Board: the town board outside Pierre's is open from Spring 1 of every loop.
    /// Vanilla gates it on <c>SpecialOrder.IsSpecialOrdersBoardUnlocked()</c>, which is
    /// <c>Game1.stats.DaysPlayed &gt;= 58</c> (SpecialOrder.cs:432, PC and Android alike), and the
    /// rewind sets DaysPlayed back to 1. The postfix returns true while the keep is owned.
    ///
    /// Inlining: the gate is a one-line static getter, exactly the shape the JIT inlines into its
    /// callers once they are re-compiled at tier 1, and an inlined copy never runs the postfix.
    /// Its callers are Town.MakeMapModifications (the board tiles and their "SpecialOrders" action,
    /// the only caller that actually opens the board), Town.draw (the "!" over the board) and
    /// ManorHouse's lost-and-found prompt (text only). So <see cref="TownBoardFallback"/> postfixes
    /// Town.MakeMapModifications: if the keep is owned and vanilla did not put the board up, it puts
    /// the board up itself, with the same tiles as Town.cs:548. When the gate postfix did run, the
    /// board is already showing and the fallback does nothing. An inlined draw or ManorHouse call
    /// costs only the "!" marker or the prompt wording, never the board.
    /// </summary>
    [HarmonyPatch(typeof(SpecialOrder), nameof(SpecialOrder.IsSpecialOrdersBoardUnlocked))]
    internal static class SpecialOrdersBoardPatch
    {
        /// <summary>Owned check; UpgradeChecker.HasUpgrade is null on non-TLY saves (dormant).</summary>
        internal static bool Owned()
            => UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade(SpecialOrdersBoardKeep.UpgradeId);

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(ref bool __result)
        {
            if (!__result && Owned())
                __result = true;
        }
    }

    /// <summary>Inlining fallback for <see cref="SpecialOrdersBoardPatch"/>: puts the town board up
    /// when the keep is owned and vanilla's own (possibly inlined) gate left it down.</summary>
    [HarmonyPatch(typeof(Town), nameof(Town.MakeMapModifications))]
    internal static class TownBoardFallback
    {
        private static readonly System.Reflection.FieldInfo ShowingField
            = AccessTools.Field(typeof(Town), "isShowingSpecialOrdersBoard");

        // The board's tiles, its action and the prize-ticket machine, as Town.MakeMapModifications
        // lays them (PC decompile Town.cs:548; the installed 1.6.15 DLL has the setMapTile form the
        // Android decompile shows at Town.cs:519).
        private const int BoardX = 61;
        private const int BoardY = 93;
        private const int BoardFrontY = 92;
        private const int TicketMachineX = 60;
        private const string BuildingsLayer = "Buildings";
        private const string FrontLayer = "Front";
        private const string TownSheet = "Town";

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(Town __instance)
        {
            if (ShowingField == null || !SpecialOrdersBoardPatch.Owned()) return;
            if (ShowingField.GetValue(__instance) is true) return;   // vanilla (with the postfix) already did it

            ShowingField.SetValue(__instance, true);
            LargeTerrainFeature bush;
            while ((bush = __instance.getLargeTerrainFeatureAt(BoardX, BoardY)) != null)
                __instance.largeTerrainFeatures.Remove(bush);

            int[] boardTiles = { 2045, 2046, 2047 };
            int[] boardFront = { 2013, 2014, 2015 };
            for (int i = 0; i < boardTiles.Length; i++)
            {
                int x = BoardX + i;
                __instance.setMapTile(x, BoardY, boardTiles[i], BuildingsLayer, TownSheet, "SpecialOrders");
                __instance.setMapTile(x, BoardFrontY, boardFront[i], FrontLayer, TownSheet);
            }
            __instance.cleanUpTileForMapOverride(new Microsoft.Xna.Framework.Point(TicketMachineX, BoardY));
            __instance.setMapTile(TicketMachineX, BoardY, 2034, BuildingsLayer, TownSheet, "SpecialOrdersPrizeTickets");
            __instance.setMapTile(TicketMachineX, BoardFrontY, 2002, FrontLayer, TownSheet);
        }
    }
}
