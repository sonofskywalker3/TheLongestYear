using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Force <c>Game1.isLocationAccessible("CommunityCenter")</c> to return true so the player can
    /// walk into the CC on Spring 1 of every run. This replaces the earlier approach of adding
    /// event <c>191393</c> to <c>MasterPlayer.eventsSeen</c>, which had too many vanilla side-
    /// effects:
    ///
    ///   - <c>Utility.pickFarmEvent</c> (Utility.cs:4417) fires <c>WorldChangeEvent(12)</c> — the
    ///     lightning strike that destroys Joja Mart — on the first rainy/stormy night once 191393
    ///     is seen. The user observed this on day 3 of a fresh run and reported "Joja is still
    ///     reading as complete and transforming into the movie theater."
    ///   - <c>Town.cs</c> repaints Joja's exterior to the cracked/abandoned look (covered by an
    ///     earlier <see cref="JojaDestroyedBlock"/>, now removed because the root cause is gone).
    ///   - <c>Game1.cs</c> branches around <c>transferredObjectsJojaMart</c> (Game1.cs:8873) and
    ///     <c>SeedShop</c> Wednesday closure (GameLocation.cs:10196) flip once 191393 is seen.
    ///
    /// By NOT setting 191393 and patching <c>isLocationAccessible</c> directly, the world stays
    /// in pre-Demetrius-cutscene state — Joja open, no lightning event, no Wednesday Pierre
    /// closure — while the CC remains walkable. <c>GameLocation.cs:6642</c> warps to the CC also
    /// route through <c>Game1.isLocationAccessible</c>, so one patch covers both gates.
    ///
    /// Identified during the 2026-05-27 playtest. The earlier "block <c>showDestroyedJoja</c>"
    /// approach was a band-aid — it fixed the visual but the underlying world-state flag still
    /// fired the lightning event.
    /// </summary>
    [HarmonyPatch(typeof(Game1), nameof(Game1.isLocationAccessible))]
    internal static class CcLocationAccessiblePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix(string locationName, ref bool __result)
        {
            if (!Core.RunActivation.IsActive)
                return true; // dormant on non-TLY saves — defer to vanilla
            if (locationName == "CommunityCenter")
            {
                __result = true;
                return false; // skip vanilla — would have returned false until 191393 is seen.
            }
            return true; // every other location: defer to vanilla.
        }
    }
}
