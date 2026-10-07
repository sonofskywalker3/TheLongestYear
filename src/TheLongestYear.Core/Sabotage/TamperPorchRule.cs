using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>When the Junimos' "tainted" scene plays after a tamper (Jeff, 2026-10-07). Not on
    /// waking: the old wake-up scene blinked and warped the farmer to his doorstep. It plays where
    /// the farmer stands the first time he steps OUT OF THE FARMHOUSE onto the Farm while a tamper
    /// report is waiting. The scene's marks are the porch's, taken from his tile as the step below
    /// the door, so any other way onto the Farm (the Forest, Backwoods or Bus Stop edge, a Warp
    /// Totem or the Return Scepter used inside the house) would put the Junimos on cliffs, water or
    /// buildings. Those entries leave the report waiting for the next farmhouse-door exit (review
    /// ruling, fix round 1). Once it has started the report is consumed, so it plays once.</summary>
    public static class TamperPorchRule
    {
        /// <summary>The farm's location name, which every farm type shares.</summary>
        public const string FarmLocationName = "Farm";

        /// <summary>The main farmhouse's location name.</summary>
        public const string FarmHouseLocationName = "FarmHouse";

        /// <summary>How far, in tiles either way, the farmer may stand from the door's exit tile.
        /// A totem used inside the house also reports the FarmHouse as where he came from, but lands
        /// him somewhere else on the farm; the door exit is the only arrival near the porch.</summary>
        public const int DoorReachTiles = 1;

        /// <param name="tamperPending">A tamper report is waiting for its scene.</param>
        /// <param name="enteredLocationName">The location the warp just put the player in.</param>
        /// <param name="previousLocationName">The location the warp took the player from.</param>
        /// <param name="isLocalPlayer">The warp was this player's own.</param>
        /// <param name="busy">An event, a farm event or a menu is up, so a scene cannot start now;
        /// the report waits for the next entry.</param>
        /// <param name="farmerX">The farmer's tile on arrival.</param>
        /// <param name="farmerY">The farmer's tile on arrival.</param>
        /// <param name="doorExits">Where the farmhouse's own warps onto the Farm put the farmer, read
        /// from the house's warp data. None known means the door cannot be confirmed: no scene.</param>
        public static bool ShouldStart(
            bool tamperPending, string enteredLocationName, string previousLocationName, bool isLocalPlayer, bool busy,
            int farmerX, int farmerY, IReadOnlyList<(int X, int Y)> doorExits)
        {
            if (!tamperPending || !isLocalPlayer || busy) return false;
            if (!string.Equals(enteredLocationName, FarmLocationName, StringComparison.Ordinal)) return false;
            if (!string.Equals(previousLocationName, FarmHouseLocationName, StringComparison.Ordinal)) return false;
            return AtDoor(farmerX, farmerY, doorExits);
        }

        /// <summary>Is the farmer on, or within <see cref="DoorReachTiles"/> of, one of the door's
        /// exit tiles?</summary>
        public static bool AtDoor(int farmerX, int farmerY, IReadOnlyList<(int X, int Y)> doorExits)
        {
            if (doorExits == null) return false;
            foreach ((int x, int y) in doorExits)
                if (Math.Abs(farmerX - x) <= DoorReachTiles && Math.Abs(farmerY - y) <= DoorReachTiles)
                    return true;
            return false;
        }
    }
}
