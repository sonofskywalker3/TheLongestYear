using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>The Cellar location a level-3 farmhouse warps into. A new game only includes it when
    /// the house was already at level 3 when the save was made, so a basement that arrives later
    /// (the kept basement at reset, or the basement focus card overnight) would warp into a map that
    /// does not exist. Creating it is idempotent.</summary>
    internal static class CellarHelper
    {
        private const string CellarName = "Cellar";
        private const string CellarMap = "Maps\\Cellar";

        /// <summary>Adds the Cellar if it is missing and binds it to the master player. Returns
        /// true when it had to be created.</summary>
        public static bool EnsureCellar()
        {
            if (Game1.getLocationFromName(CellarName) != null) return false;
            Game1.locations.Add(new Cellar(CellarMap, CellarName));
            Game1.updateCellarAssignments();
            return true;
        }
    }
}
