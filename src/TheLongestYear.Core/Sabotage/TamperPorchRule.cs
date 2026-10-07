using System;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>When the Junimos' "tainted" scene plays after a tamper (Jeff, 2026-10-07). Not on
    /// waking: the old wake-up scene blinked and warped the farmer to his doorstep. It plays where
    /// the farmer already is, the first time he steps onto the Farm while a tamper report is
    /// waiting, which on an ordinary morning is walking out of the farmhouse door. A day he never
    /// leaves the house leaves the report waiting, and it plays on his next Farm entry. Once it has
    /// started the report is consumed, so it plays once.</summary>
    public static class TamperPorchRule
    {
        /// <summary>The farm's location name, which every farm type shares.</summary>
        public const string FarmLocationName = "Farm";

        /// <param name="tamperPending">A tamper report is waiting for its scene.</param>
        /// <param name="enteredLocationName">The location the warp just put the player in.</param>
        /// <param name="isLocalPlayer">The warp was this player's own.</param>
        /// <param name="busy">An event, a farm event or a menu is up, so a scene cannot start now;
        /// the report waits for the next entry.</param>
        public static bool ShouldStart(bool tamperPending, string enteredLocationName, bool isLocalPlayer, bool busy)
            => tamperPending
               && isLocalPlayer
               && !busy
               && string.Equals(enteredLocationName, FarmLocationName, StringComparison.Ordinal);
    }
}
