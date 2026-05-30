using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Festival time-flow for the time-loop: vanilla freezes <see cref="Game1.timeOfDay"/> the
    /// moment the player enters a festival map and warps them back to the farm at 2200 (or
    /// 2400 for Moonlight Jellies / Spirit's Eve) on exit, which "wastes the whole day" the
    /// player reports. The roguelite needs every in-game hour to count, so:
    ///
    ///   1. Time advances normally inside the festival (player walks in at 10:00, can leave
    ///      at 10:50 with that real time intact). Patches <see cref="Game1.shouldTimePass"/>.
    ///
    ///   2. On exit, <see cref="Event.endBehaviors"/> would clobber <c>timeOfDayAfterFade</c>
    ///      with 2200; we restore it to the actual <see cref="Game1.timeOfDay"/> via prefix/
    ///      postfix state passing. Vanilla's object time-advance (which uses 2200 - startTime)
    ///      runs unchanged — it slightly over-advances crops + machines, which v1 tolerates.
    ///
    ///   3. If the player overstays past the festival's scheduled end time, ModEntry's update
    ///      tick checks <see cref="ShouldAutoEnd"/> and calls <see cref="ForceEnd"/> to warp
    ///      them out with a "Festival is over" message at the festival's endTime.
    /// </summary>
    internal static class FestivalTimeFlow
    {
        /// <summary>Guard so OnUpdateTicked's auto-eject only triggers once per festival.</summary>
        private static bool _pendingAutoEnd;

        public static void Reset() => _pendingAutoEnd = false;

        /// <summary>Returns true if a festival is active AND the in-game clock has reached its
        /// scheduled end time. Called from ModEntry.OnUpdateTicked.</summary>
        public static bool ShouldAutoEnd()
        {
            if (_pendingAutoEnd) return false; // already firing — wait for end-behaviours to clear it
            if (!Game1.isFestival()) return false;
            if (Game1.CurrentEvent == null) return false;

            if (!TryGetFestivalEndTime(Game1.CurrentEvent, out int endTime)) return false;
            return Game1.timeOfDay >= endTime;
        }

        /// <summary>Force-end the festival with a HUD message. Idempotent within one festival
        /// thanks to the <see cref="_pendingAutoEnd"/> guard.</summary>
        public static void ForceEnd(IMonitor monitor)
        {
            if (_pendingAutoEnd) return;
            _pendingAutoEnd = true;

            // Dismiss whatever menu might be open (vanilla forceEndFestival does this) so the
            // confirmation prompt doesn't linger past auto-eject.
            if (Game1.activeClickableMenu != null)
                Game1.exitActiveMenu();

            Game1.addHUDMessage(new HUDMessage("The festival is over.", HUDMessage.newQuest_type));
            Game1.CurrentEvent?.forceEndFestival(Game1.player);
            monitor.Log("Festival auto-ejected: end time reached.", LogLevel.Info);
        }

        /// <summary>Strip the "festival_" prefix from an event id (vanilla format is
        /// "festival_&lt;name&gt;" — see Event.cs:11487).</summary>
        private static bool TryGetFestivalEndTime(Event ev, out int endTime)
        {
            endTime = 0;
            if (ev?.id == null || !ev.id.StartsWith("festival_")) return false;
            string festivalName = ev.id.Substring("festival_".Length);
            return Event.tryToLoadFestivalData(
                festivalName, out _, out _, out _, out _, out endTime);
        }

        /// <summary>
        /// Lets time tick during festivals. Vanilla's <see cref="Game1.shouldTimePass"/>
        /// short-circuits to false the instant <see cref="Game1.isFestival"/> is true; we
        /// re-implement the remaining gates so paused / menu / dialogue states still freeze
        /// time the way the rest of the game expects.
        /// </summary>
        [HarmonyPatch(typeof(Game1), nameof(Game1.shouldTimePass))]
        internal static class ShouldTimePassPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
            private static bool Prefix(bool ignore_multiplayer, ref bool __result)
            {
                if (!Game1.isFestival()) return true; // defer to vanilla

                // We only override the festival short-circuit. Replicate vanilla's other gates
                // so a paused/menu/dialogue state still pauses time inside the festival.
                if (Game1.CurrentEvent != null && Game1.CurrentEvent.isWedding) { __result = false; return false; }
                if (Game1.farmEvent != null) { __result = false; return false; }
                if (Game1.IsMultiplayer && !ignore_multiplayer) { __result = !Game1.netWorldState.Value.IsTimePaused; return false; }
                if (Game1.paused || Game1.freezeControls || Game1.overlayMenu != null || Game1.isTimePaused)
                { __result = false; return false; }
                if (Game1.eventUp && Game1.CurrentEvent != null && !Game1.CurrentEvent.isFestival)
                { __result = false; return false; } // pause for non-festival events even mid-festival map
                if (Game1.activeClickableMenu != null && !(Game1.activeClickableMenu is BobberBar))
                { __result = false; return false; }
                if (!Game1.player.CanMove && !Game1.player.UsingTool)
                { __result = Game1.player.forceTimePass; return false; }

                __result = true;
                return false;
            }
        }

        /// <summary>
        /// Walking off the festival map normally pops a <see cref="ConfirmationDialog"/>
        /// ("Are you sure you want to leave [Festival Name]?") so the player doesn't
        /// accidentally end the day. With <see cref="FestivalTimeFlow"/> active the festival
        /// no longer ends the day — leaving just leaves — so the warning is misleading. Skip
        /// the prompt entirely and call <see cref="Event.forceEndFestival"/> directly.
        /// Multiplayer's ReadyCheckDialog is also skipped; v1 is single-player only.
        /// </summary>
        [HarmonyPatch(typeof(Event), nameof(Event.TryStartEndFestivalDialogue))]
        internal static class SkipExitFestivalPromptPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
            private static bool Prefix(Event __instance, Farmer who, ref bool __result)
            {
                if (__instance == null || who == null || !who.IsLocalPlayer || !__instance.isFestival)
                {
                    __result = false;
                    return false; // mirror vanilla's early-out
                }

                // Identify the warp the player was about to step through. vanilla's
                // MovePositionImpl (Farmer.cs:8619) just established a collision via
                // isCollidingWithWarp; we pick the warp nearest the player's current pixel
                // position so the post-end-festival exit goes through THAT edge instead of
                // vanilla's hard-coded "Farm" warp. Done BEFORE Halt() so who.Position is
                // still at the about-to-warp tile.
                Warp targetWarp = null;
                if (who.currentLocation?.warps != null)
                {
                    float minSq = float.MaxValue;
                    foreach (Warp w in who.currentLocation.warps)
                    {
                        if (w == null || w.npcOnly.Value) continue;
                        float dx = w.X * 64f - who.Position.X;
                        float dy = w.Y * 64f - who.Position.Y;
                        float sq = dx * dx + dy * dy;
                        if (sq < minSq) { minSq = sq; targetWarp = w; }
                    }
                }

                // Capture the walking direction BEFORE Halt() so we can carry it through the
                // exit-warp. Vanilla's Game1.cs:7986 reads player.orientationBeforeEvent for
                // the post-warp facing direction; Event.setUpCharacters seeded that field
                // with the festival-entry orientation (usually facing an NPC). For an edge-
                // warp continuation we want the direction the player was MOVING when they
                // hit the warp, so a south walk out of Town lands them facing south into
                // Forest, not facing north back at Town.
                int walkingDirection = who.FacingDirection;

                // Mirror vanilla's halt-and-snap-back-to-last-position so the player doesn't
                // walk into the off-map collision area, then exit straight away. (PC vanilla
                // doesn't have GameLocation.tapToMove — that field is Android-only — so we
                // skip the touch-input reset that vanilla Android does here.)
                who.Halt();
                who.Position = who.lastPosition;
                __instance.forceEndFestival(who);

                // forceEndFestival → endBehaviors sets exitLocation to ("Farm", main farm-
                // house entry). Override that with the warp the player actually walked into,
                // so a festival exit on Town's south edge lands them on Forest, north edge
                // on BusStop, etc — like vanilla non-festival movement. 2026-05-29 user
                // request: "not warped me to the farm when I walk out of a festival."
                if (targetWarp != null)
                {
                    __instance.setExitLocation(targetWarp.TargetName, targetWarp.TargetX, targetWarp.TargetY);
                    Game1.player.orientationBeforeEvent = walkingDirection;
                }

                __result = true;
                return false; // skip vanilla — no dialog
            }
        }

        /// <summary>
        /// Restores <see cref="Game1.timeOfDayAfterFade"/> to the actual in-game time at the
        /// moment the festival was exited, undoing vanilla's hard-coded jump to 2200 / 2400.
        /// Uses Harmony __state to pass the captured time from prefix → postfix. The state is
        /// -1 (skip) for non-festival events so this patch only affects festival exits.
        /// </summary>
        [HarmonyPatch(typeof(Event), nameof(Event.endBehaviors), new[] { typeof(string[]), typeof(GameLocation) })]
        internal static class EndBehaviorsPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
            private static void Prefix(Event __instance, out int __state)
            {
                __state = __instance != null && __instance.isFestival ? Game1.timeOfDay : -1;
            }

            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
            private static void Postfix(int __state)
            {
                if (__state < 0) return; // wasn't a festival exit
                Game1.timeOfDayAfterFade = __state;
                _pendingAutoEnd = false; // clear the auto-eject guard for next festival
            }
        }
    }
}
