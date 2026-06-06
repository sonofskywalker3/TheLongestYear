using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// v1 design: the Joja shortcut is forbidden. The Junimo time-loop requires the player to
    /// restore the Community Center bundle-by-bundle — buying a JojaMart membership trivializes
    /// the gate by replacing bundles with money-only rooms, breaking the loop's selection +
    /// donation mechanics. So we intercept the "JojaSignUp_Yes" dialogue response and refuse it.
    ///
    /// Story note from the user (2026-05-26): future Junimo dialogue will explain in-fiction
    /// why this option is unavailable. For v1 we just block the answer and show a generic
    /// message so the player isn't confused by a no-op click.
    /// </summary>
    [HarmonyPatch(typeof(JojaMart), nameof(JojaMart.answerDialogue))]
    internal static class JojaMembershipBlock
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(JojaMart __instance, Response answer, ref bool __result)
        {
            if (!Core.RunActivation.IsActive)
                return true; // dormant on non-TLY saves — allow the vanilla Joja membership
            // Mirror vanilla's check (JojaMart.cs:42-46): build "<questionKey>_<answerKey>"
            // and compare. We block ONLY the JojaSignUp_Yes case so other dialogue answers
            // in JojaMart (shop interactions etc) pass through untouched.
            if (__instance?.lastQuestionKey == null || answer?.responseKey == null)
                return true;
            string questionRoot = StardewValley.ArgUtility.SplitBySpaceAndGet(__instance.lastQuestionKey, 0);
            string composite = questionRoot + "_" + answer.responseKey;
            if (composite != "JojaSignUp_Yes") return true;

            // v1: refuse the membership purchase. Vanilla would otherwise deduct 5000g and
            // add the JojaMember mail (JojaMart.cs:48-54), which would break the time-loop
            // gate by switching the CC to the money-only Joja path.
            Game1.drawObjectDialogue(
                "The Junimos shake their heads. Something about \"a debt to the land\" " +
                "they want you to settle yourself.");
            __result = true;
            return false;
        }
    }
}
