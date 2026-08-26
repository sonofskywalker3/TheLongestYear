using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Runs a festival's main event (Egg Hunt, ice fishing contest, Flower Dance, grange judging)
    /// at most once per day.
    ///
    /// Vanilla has no guard because it does not need one: a festival ends the day, the clock jumps
    /// past the festival's end time, and the map cannot be re-entered. <see cref="FestivalTimeFlow"/>
    /// deliberately removes that (the hours inside a festival have to count in a time-loop), which
    /// leaves the festival re-entrant for the rest of its window: leave the map, walk back in, and
    /// vanilla starts the whole festival again with the host offering the main event as if it were
    /// the first time. Jeff caught it on emmalution's stream, where the Egg Hunt ran three times in
    /// one day and paid out each time.
    ///
    /// The block sits on <c>Event.answerDialogueQuestion</c>, the single place a "yes" to the host
    /// starts a main event (Event.cs:11821), so it covers every festival rather than the Egg Hunt
    /// alone. Everything else about a repeat visit still works: the stalls, the shop, the NPCs.
    /// </summary>
    [HarmonyPatch(typeof(Event), nameof(Event.answerDialogueQuestion))]
    internal static class FestivalMainEventOncePatch
    {
        /// <summary>Set by ModEntry.OnSaveLoaded (null when no TLY run is loaded).</summary>
        internal static Func<RunState> RunProvider;

        /// <summary>Set by ModEntry so the block can be seen in the log.</summary>
        internal static IMonitor Monitor;

        /// <summary>Vanilla's answer key for "yes, start it" (Event.cs:11824).</summary>
        private const string YesKey = "yes";

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix(Event __instance, string answerKey)
        {
            if (!RunActivation.IsActive) return true;              // dormant on non-TLY saves
            if (__instance == null || !__instance.isFestival) return true;
            if (!string.Equals(answerKey, YesKey, StringComparison.Ordinal)) return true;

            string festivalId = __instance.id;
            if (string.IsNullOrEmpty(festivalId)) return true;

            RunState run = RunProvider?.Invoke();
            if (run == null) return true;

            int today = Game1.Date?.TotalDays ?? -1;
            if (today < 0) return true;

            if (FestivalMainEvent.AlreadyPlayed(run, festivalId, today))
            {
                // Say why, or it reads as the host being broken.
                Game1.drawObjectDialogue(Strings.Get("dialog.festival.already-done"));
                Monitor?.Log(
                    $"Festival main event blocked: '{festivalId}' already ran today (day {today}).",
                    LogLevel.Info);
                return false;   // skip vanilla — no second run
            }

            FestivalMainEvent.MarkPlayed(run, festivalId, today);
            Monitor?.Log($"Festival main event starting: '{festivalId}' (day {today}); further runs today are blocked.", LogLevel.Info);
            return true;
        }
    }
}
