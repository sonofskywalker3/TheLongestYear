using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gifts of the Junimos restore the five room-completion mails at reset, and vanilla's
    /// Farmer.hasCompletedCommunityCenter() is purely mail-based (Farmer.cs:7291): a player who
    /// owns every Gift and then finishes only the Bulletin Board would flip the whole CC to
    /// "complete" (exterior refurbished, Robin's community upgrade, the completion GSQ) with five
    /// rooms still open on the board. While the loop is active the answer comes from the board
    /// instead: every area's bundles complete (review 2026-08-29).
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.hasCompletedCommunityCenter))]
    internal static class CommunityCenterCompletePatch
    {
        // The same five rooms vanilla's mail check reads (the Bulletin Board is not one of them).
        private static readonly string[] Rooms = { "Pantry", "CraftsRoom", "FishTank", "BoilerRoom", "Vault" };

        // ReSharper disable once InconsistentNaming
        private static bool Prefix(ref bool __result)
        {
            if (!RunActivation.IsActive) return true;
            if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter) return true;
            foreach (string room in Rooms)
            {
                if (!Integration.RunReachEvaluator.RoomComplete(room))
                {
                    __result = false;
                    return false;
                }
            }
            return true;   // every room really is done: vanilla's mail answer stands
        }
    }
}
