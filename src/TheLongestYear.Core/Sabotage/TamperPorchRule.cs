using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>When and where the Junimos' "tainted" scene plays after a tamper (Jeff, 2026-10-07).
    /// Not on waking. It plays the first time the farmer arrives on the Farm by ANY route while a
    /// tamper report waits: the farmhouse door, the Bus Stop, Forest or Backwoods edge, a building
    /// door, a totem ("however you get to the farm map, just show the scene, vanilla does this
    /// too"). Like vanilla's Community Center cutscene on entering Town, it is staged somewhere fixed,
    /// the farmhouse porch, and the farmer is put back on the tile and facing he arrived at when it
    /// ends. An arrival while something else is up keeps the report for the next one. Once it has
    /// started the report is consumed, so it plays once.</summary>
    public static class TamperPorchRule
    {
        /// <summary>The farm's location name, which every farm type shares.</summary>
        public const string FarmLocationName = "Farm";

        /// <summary>The main farmhouse's location name.</summary>
        public const string FarmHouseLocationName = "FarmHouse";

        /// <param name="tamperPending">A tamper report is waiting for its scene.</param>
        /// <param name="enteredLocationName">The location the warp just put the player in.</param>
        /// <param name="isLocalPlayer">The warp was this player's own.</param>
        /// <param name="busy">An event, a farm event, a menu or a season turn is up, so a scene
        /// cannot start now; the report waits for the next arrival.</param>
        /// <param name="porch">Where the scene is staged (<see cref="PorchTile"/>); none known means
        /// no scene.</param>
        public static bool ShouldStart(
            bool tamperPending, string? enteredLocationName, bool isLocalPlayer, bool busy, (int X, int Y)? porch)
        {
            if (!tamperPending || !isLocalPlayer || busy || porch == null) return false;
            return string.Equals(enteredLocationName, FarmLocationName, StringComparison.Ordinal);
        }

        /// <summary>The porch tile the scene is staged on: where the farmhouse's own warp onto the
        /// Farm puts the farmer (read from the house's warp data, so every farm type and a moved
        /// house are right), else the farm's own main-house entry tile, which is the same tile on
        /// the standard farm (64,15), else none.</summary>
        public static (int X, int Y)? PorchTile(IReadOnlyList<(int X, int Y)>? doorExits, (int X, int Y)? doorTile)
        {
            if (doorExits != null && doorExits.Count > 0) return doorExits[0];
            if (doorTile is (int x, int y)) return (x, y);
            return null;
        }
    }
}
