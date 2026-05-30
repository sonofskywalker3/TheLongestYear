using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Festival exit fix — TWO patches that together let the player treat the festival map
    /// like any other location: walk through any warp normally (no "are you ready to leave?"
    /// prompt + no Farm warp), and let the timer-expiry exit leave the player where they
    /// actually are instead of forcing them back to the farm.
    ///
    /// User refinement 2026-05-29 (rounds 1-3 of this patch):
    /// 1. First cut warped player to the host map's pedestrian entrance — wrong, the user
    ///    would be teleported across the entire map if they were mid-Town when the timer
    ///    fired.
    /// 2. Second cut warped to player's current tile on the host map — also wrong because
    ///    the prompt at <see cref="Event.TryStartEndFestivalDialogue"/> still fires when
    ///    walking through map-edge warps. User: "we already disabled the prompt but it
    ///    still warps."
    /// 3. This cut handles BOTH: prefix on TryStartEndFestivalDialogue swaps the dialog for
    ///    a real warpFarmer through the colliding warp (festival keeps running on the
    ///    temporaryLocation; player goes to the adjacent real map normally). And the
    ///    endBehaviors postfix now distinguishes "player still on the festival temp clone"
    ///    (warp to host at same tile) vs "player already wandered onto a real map" (don't
    ///    override — they stay where they are).
    ///
    /// User intent: "imagine that there were no festival at all I go in the top entrance
    /// and I'm inside the top entrance of the town if I go out the top entrance then I'm
    /// on the road where the bus is …" — i.e. all warps behave like vanilla non-festival
    /// movement.
    /// </summary>
    [HarmonyPatch(typeof(Event), nameof(Event.endBehaviors),
        new System.Type[] { typeof(string[]), typeof(GameLocation) })]
    internal static class FestivalExitPatch
    {
        /// <summary>Name vanilla gives to the festival's cloned host-map location — set at
        /// <c>Event.cs:2773</c> as <c>new Town("Maps\\Town", "Temp")</c> (and the other map
        /// variants). All festivals use the literal string "Temp" as their location name.</summary>
        internal const string FestivalTempLocationName = "Temp";

        internal static readonly FieldInfo FestivalDataField =
            AccessTools.Field(typeof(Event), "festivalData");

        // ReSharper disable InconsistentNaming — Harmony convention.

        /// <summary>Capture isFestival BEFORE endBehaviors runs — vanilla resets it to false
        /// inside the method body (Event.cs:4580), so the postfix would see it as false
        /// without the prefix snapshot.</summary>
        private static void Prefix(Event __instance, out bool __state)
        {
            __state = __instance != null && __instance.isFestival;
        }

        private static void Postfix(Event __instance, bool __state)
        {
            if (!__state) return;
            if (__instance == null) return;

            Farmer who = Game1.player;
            if (who == null) return;
            GameLocation currentLoc = who.currentLocation;
            if (currentLoc == null) return;

            int tileX = (int)who.Tile.X;
            int tileY = (int)who.Tile.Y;

            if (currentLoc.Name == FestivalTempLocationName)
            {
                // Player is still on the festival's temporaryLocation when the timer fires.
                // Warp them to the same tile on the REAL host map — the geometry is identical,
                // so the tile is guaranteed valid.
                string hostMap = TryGetHostMapName(__instance);
                if (string.IsNullOrEmpty(hostMap)) return;
                if (Game1.getLocationFromName(hostMap) == null) return;
                __instance.setExitLocation(hostMap, tileX, tileY);
            }
            else
            {
                // Player already walked out onto a real map during the festival. Override
                // vanilla's forced "Farm" warp with a same-location no-op exit so they stay
                // exactly where they were. Vanilla's exit-warp logic accepts a same-location
                // request (it just fades + redraws without movement).
                __instance.setExitLocation(currentLoc.Name, tileX, tileY);
            }
        }

        internal static string TryGetHostMapName(Event ev)
        {
            if (FestivalDataField == null) return null;
            var data = FestivalDataField.GetValue(ev) as Dictionary<string, string>;
            if (data == null) return null;
            if (!data.TryGetValue("conditions", out string conditions)) return null;
            if (string.IsNullOrEmpty(conditions)) return null;
            string[] parts = conditions.Split('/');
            return parts.Length > 0 ? parts[0] : null;
        }
    }

    /// <summary>
    /// Sister patch to <see cref="FestivalExitPatch"/>: replaces vanilla's "are you ready
    /// to leave the festival?" prompt (and its subsequent forceEndFestival → Farm warp)
    /// with a normal map-edge warp through the colliding warp tile. The festival itself
    /// keeps running on the temporaryLocation; the player just transitions to whichever
    /// adjacent real map their map-edge warp targets (Town south → Forest, Town north →
    /// BusStop, Beach south → ocean exit, etc).
    ///
    /// Hook is <see cref="Event.TryStartEndFestivalDialogue"/>, called from
    /// <c>Farmer.MovePositionImpl</c> (Farmer.cs:8624) when the player walks into a warp
    /// while inside a festival event. We find the warp closest to the player's position
    /// (vanilla already confirmed they're colliding with one, but doesn't pass it to
    /// TryStartEndFestivalDialogue) and call <see cref="Game1.warpFarmer(string, int, int, int, bool)"/>
    /// with its TargetName + TargetX/Y + the player's facing direction.
    /// </summary>
    [HarmonyPatch(typeof(Event), nameof(Event.TryStartEndFestivalDialogue))]
    internal static class FestivalSkipExitPromptPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(Event __instance, Farmer who, ref bool __result)
        {
            if (__instance == null || who == null || !who.IsLocalPlayer || !__instance.isFestival)
                return true;  // not our case — run vanilla

            GameLocation loc = who.currentLocation;
            if (loc?.warps == null || loc.warps.Count == 0)
                return true;  // no warps to use — fall back to vanilla dialog

            // Pick the warp nearest the player's pixel position. Vanilla's MovePositionImpl
            // already established a warp collision via isCollidingWithWarp; the closest warp
            // by Euclidean distance is the one they walked into.
            Warp nearest = null;
            float minSq = float.MaxValue;
            foreach (Warp w in loc.warps)
            {
                if (w == null || w.npcOnly.Value) continue;
                float dx = w.X * 64f - who.Position.X;
                float dy = w.Y * 64f - who.Position.Y;
                float sq = dx * dx + dy * dy;
                if (sq < minSq) { minSq = sq; nearest = w; }
            }

            if (nearest == null) return true;

            // Skip vanilla's dialog + forceEndFestival path entirely. Festival keeps running.
            Game1.warpFarmer(nearest.TargetName, nearest.TargetX, nearest.TargetY, who.FacingDirection);
            __result = true;
            return false;  // skip vanilla
        }
    }
}
