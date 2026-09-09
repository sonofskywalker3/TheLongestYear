using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gifts of the Junimos restore the five room-completion mails at reset, and vanilla's
    /// Farmer.hasCompletedCommunityCenter() is purely mail-based (Farmer.cs:7174): a player who
    /// owns every Gift and then finishes only the Bulletin Board would flip the whole CC to
    /// "complete" (exterior refurbished, Robin's community upgrade, the completion GSQ) with five
    /// rooms still open on the board. While the loop is active the answer comes from the board
    /// instead: every area's bundles complete (review 2026-08-29). Once the hall itself is
    /// restored (every vanilla area flag, or the goodbye-dance ccIsComplete mail) the hall is the
    /// authority and the answer is yes: ChaoticMindset's save (Nexus bug 1130863) had all six
    /// areas restored and ccIsComplete set, yet this prefix said no, so Willy's back-room letter
    /// never came. Rule and its cases: <see cref="CommunityCenterCompletionRule"/>.
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.hasCompletedCommunityCenter))]
    internal static class CommunityCenterCompletePatch
    {
        // The five item rooms the board walk checks; vanilla's mail check also wants ccBulletin.
        private static readonly string[] Rooms = { "Pantry", "CraftsRoom", "FishTank", "BoilerRoom", "Vault" };

        internal static StardewModdingAPI.IMonitor Monitor;
        private static bool _loggedNo, _loggedYes;

        // ReSharper disable once InconsistentNaming
        private static bool Prefix(Farmer __instance, ref bool __result)
        {
            if (!RunActivation.IsActive) return true;
            if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter cc) return true;

            bool everyRoomDone = true;
            foreach (string room in Rooms)
            {
                if (!Integration.RunReachEvaluator.RoomComplete(room)) { everyRoomDone = false; break; }
            }
            bool allAreas = cc.areAllAreasComplete();
            bool ccMail = __instance.mailReceived.Contains("ccIsComplete")
                          || (Game1.MasterPlayer?.mailReceived.Contains("ccIsComplete") ?? false);

            switch (CommunityCenterCompletionRule.Decide(RunActivation.IsActive, allAreas, ccMail, everyRoomDone))
            {
                case CcCompletionAnswer.Yes:
                    if (!_loggedYes && !everyRoomDone)
                    {
                        _loggedYes = true;
                        Monitor?.Log(
                            "CC completion: the hall is restored (areasComplete/ccIsComplete) but the board walk says a room is open; answering complete from the hall.",
                            StardewModdingAPI.LogLevel.Info);
                    }
                    __result = true;
                    return false;
                case CcCompletionAnswer.No:
                    if (!_loggedNo)
                    {
                        _loggedNo = true;
                        Monitor?.Log(
                            "CC completion: a room is still open on the board; answering not complete regardless of mail.",
                            StardewModdingAPI.LogLevel.Trace);
                    }
                    __result = false;
                    return false;
                default:
                    return true;   // every room really is done: vanilla's mail answer stands
            }
        }

        /// <summary>Reset the once-per-session log guards (save load).</summary>
        internal static void ResetLogGuards() { _loggedNo = false; _loggedYes = false; }
    }
}
