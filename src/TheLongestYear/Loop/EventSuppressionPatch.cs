using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

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
    // 2026-05-29 round 11 fix: PC's GameLocation has two checkEventPrecondition overloads —
    // (string) and (string, bool check_seen). Patching by name alone threw AmbiguousMatchException
    // during PatchAll, which aborted EVERY downstream Harmony patch (OnStoneDestroyed,
    // GetActualCapacity, the rest of MineDropsPatch, AllDropsPatch, TerrainBonusPatches, …) —
    // a single round-8 oversight quietly cratered three rounds of bonus-drop work. Pin the
    // single-string overload explicitly so PatchAll resolves cleanly.
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkEventPrecondition),
        new System.Type[] { typeof(string) })]
    internal static class EventSuppressionPatch
    {
        // Event keys can appear as "191393" or "191393/precondition1/precondition2/..."
        // depending on the Data/Events/&lt;Location&gt; entry; we match the leading id segment
        // so both forms are caught.
        // internal (not private): the replayable-cutscene scanner reuses this as its exclusion seed so
        // an event we explicitly suppress is never auto-flagged as replayable (single source of truth).
        internal static readonly System.Collections.Generic.HashSet<string> SuppressedEventIds
            = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        {
            // 611439 — Lewis walks you to the derelict CC ("what an eyesore"), grants ccDoorUnlock +
            // quest 26. TLY opens the CC on Spring 1 (CommunityCenterUnlock), so the intro is stale.
            // NOT 191393: that is the CC COMPLETION ceremony (Morris driven out), and it gates the
            // Joja shutdown, the Pierre-Wednesday closure and the abandoned-JojaMart lightning.
            // 0.11.60 suppressed 191393 by mistake -> post-completion world froze (Nexus bug 1113630).
            "611439",
        };

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix(string precondition, ref string __result)
        {
            if (!RunActivation.IsActive) return true; // dormant on non-TLY saves — defer to vanilla
            if (string.IsNullOrEmpty(precondition)) return true;

            int slashIdx = precondition.IndexOf('/');
            string eventId = slashIdx < 0 ? precondition : precondition.Substring(0, slashIdx);

            if (SuppressedEventIds.Contains(eventId))
            {
                __result = "-1";
                return false; // skip the vanilla precondition logic entirely
            }

            // Event-gating Phase 2: hold jarring early events until Spring 5, and skip the furnace
            // teach scene while the recipe is already known this run. Curated id sets live in
            // EventGatingTables.Default — EMPTY until the tly_dumpevents audit fills them, so this is
            // a safe pass-through no-op until those ids are wired in.
            if (Context.IsWorldReady && Game1.player != null)
            {
                bool furnaceKnown = Game1.player.craftingRecipes.ContainsKey("Furnace");
                if (EventGatingPolicy.Decide(eventId, (int)Game1.season, Game1.dayOfMonth, furnaceKnown,
                        EventGatingTables.Default) == EventGatingDecision.Suppress)
                {
                    __result = "-1";
                    return false;
                }
            }

            return true; // every other event: defer to vanilla.
        }
    }
}
