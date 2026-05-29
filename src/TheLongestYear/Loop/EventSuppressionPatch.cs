using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Blocks vanilla cutscenes that don't make sense inside the TLY year-loop. Each entry in
    /// <see cref="SuppressedEventIds"/> is matched against the event-key prefix in
    /// <see cref="GameLocation.checkEventPrecondition"/>; returning "-1" from the prefix
    /// short-circuits the dispatch in <c>checkForEvents</c> (see line 15518 of GameLocation:
    /// <c>if (!string.IsNullOrEmpty(text) &amp;&amp; text != "-1" &amp;&amp; ...)</c>), so the
    /// candidate event is silently skipped without firing.
    ///
    /// 2026-05-29 round 8 — suppress event 191393 (Demetrius + Lewis "have you seen the
    /// Community Center?" cutscene, vanilla Spring 5 Y1 in Town). The TLY player has been
    /// donating from day 1 of every loop, so the introduction beat is stale and out of order.
    /// Spec'd to be co-opted as TLY's day-1 narrative intro in a later pass (see TODO.md) —
    /// for now it's just turned off.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkEventPrecondition))]
    internal static class EventSuppressionPatch
    {
        // Event keys can appear as "191393" or "191393/precondition1/precondition2/..."
        // depending on the Data/Events/&lt;Location&gt; entry; we match the leading id segment
        // so both forms are caught.
        private static readonly System.Collections.Generic.HashSet<string> SuppressedEventIds
            = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        {
            "191393", // Demetrius + Lewis CC intro in Town (Spring 5 Y1)
        };

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix(string precondition, ref string __result)
        {
            if (string.IsNullOrEmpty(precondition)) return true;

            int slashIdx = precondition.IndexOf('/');
            string eventId = slashIdx < 0 ? precondition : precondition.Substring(0, slashIdx);

            if (SuppressedEventIds.Contains(eventId))
            {
                __result = "-1";
                return false; // skip the vanilla precondition logic entirely
            }

            return true; // every other event: defer to vanilla.
        }
    }
}
