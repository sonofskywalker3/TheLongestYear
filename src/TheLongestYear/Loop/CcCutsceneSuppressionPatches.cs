using System.Reflection;
using HarmonyLib;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Suppress the vanilla Community Center area-restoration cutscene during a TLY run.
    ///
    /// The player re-restores the whole CC every loop, so the animated "we are the Junimos…"
    /// restoration (screen glow, frozen controls, junimo speech) — and the final all-areas goodbye
    /// dance it culminates in — would replay every run and grate fast. The final ceremony is also an
    /// event that, when the CC is finished on a day-28 boundary, stays up and races TLY's own day-28
    /// gate/shrine (it leaves <c>Game1.eventUp</c>/frozen controls, which makes <c>ForceFullSave</c>
    /// skip) — bug #1b. TLY shows its OWN win cutscene on the breaking-the-loop run, so the vanilla
    /// ceremony is pure interference here.
    ///
    /// <para>The mark-complete is invoked INSIDE <c>doRestoreAreaCutscene</c> (decompile
    /// CommunityCenter.cs:866), and the final goodbye is reached through that same cutscene's phase
    /// progression, so we can't simply no-op the method — that would leave areas uncompleted. The
    /// prefix preserves completion via <c>markAreaAsComplete</c> (which also flips the internal
    /// "watching goodbye" flag when the last area lands, harmlessly, since the phase loop that reads
    /// it is skipped), restores the room art instantly with <c>loadArea(area, showEffects:false)</c>,
    /// keeps the missed-rewards backstop, then skips the animated sequence + goodbye. Non-TLY saves
    /// are untouched.</para>
    /// </summary>
    [HarmonyPatch(typeof(CommunityCenter), "doRestoreAreaCutscene")]
    internal static class SuppressCcAreaCutscenePatch
    {
        // doRestoreAreaCutscene's own tail call — private, parameterless. Cached so we keep the
        // vanilla reward backstop without re-reflecting on every area completion.
        private static readonly MethodInfo CheckForMissedRewards =
            AccessTools.Method(typeof(CommunityCenter), "checkForMissedRewards");

        private static bool Prefix(CommunityCenter __instance, int whichArea)
        {
            if (!RunActivation.IsActive)
                return true;   // vanilla cutscene on non-TLY saves

            __instance.markAreaAsComplete(whichArea);
            __instance.loadArea(whichArea, false);     // restore room art, no animation/effects
            CheckForMissedRewards?.Invoke(__instance, null);

            return false;      // skip the animated restoration + final goodbye ceremony
        }
    }
}
