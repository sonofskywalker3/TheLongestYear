using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Festival exit fix — postfix on <see cref="Event.endBehaviors(string[], GameLocation)"/>
    /// that overrides vanilla's unconditional warp-to-Farm exit when the closed event was a
    /// festival. Lands the player on the festival's host map (Town for Egg / Fair / Spirit's
    /// Eve / Winter Star, Beach for Luau / Jellies / Night Market, Forest for Flower Dance /
    /// Festival of Ice) instead of the farm-house porch.
    ///
    /// Vanilla code at Event.cs:4561-4577 reads (paraphrased):
    ///   if (isFestival)
    ///   {
    ///       Point mfhe = Game1.getFarm().GetMainFarmHouseEntry();
    ///       setExitLocation("Farm", mfhe.X, mfhe.Y);
    ///   }
    /// We reach in via reflection on the private <c>festivalData</c> field to look up
    /// <c>data["conditions"]</c> — that string's first segment is the host map name (see
    /// <c>Game1.cs:6619</c>, <c>DebugCommands.cs:2965</c> for canonical parsing).
    ///
    /// 2026-05-29 user spec refinement: land the player AT THEIR CURRENT TILE on the real
    /// host map — not at the map's pedestrian entrance. The user's framing was "just let me
    /// exit like there wasn't a festival going on at all." During a festival the player
    /// stands on a temporary cloned location (<c>temporaryLocation</c> at Event.cs:2773);
    /// the cloned map has identical tile geometry to the real one, so the same (X, Y) is
    /// always a valid landing position. Walking off the south Town edge after the Egg
    /// Festival should drop you at south Town, not at "Town's first non-NPC warp" which
    /// might be the bus stop entrance on the other side of the map.
    /// </summary>
    [HarmonyPatch(typeof(Event), nameof(Event.endBehaviors),
        new System.Type[] { typeof(string[]), typeof(GameLocation) })]
    internal static class FestivalExitPatch
    {
        private static readonly FieldInfo FestivalDataField =
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
            if (FestivalDataField == null) return;

            // festivalData is private; reflect to get the conditions string.
            var data = FestivalDataField.GetValue(__instance) as Dictionary<string, string>;
            if (data == null) return;
            if (!data.TryGetValue("conditions", out string conditions)) return;
            if (string.IsNullOrEmpty(conditions)) return;

            // "conditions" format: "<HostMap>/<startTime> <endTime>" — only the host map matters.
            string[] parts = conditions.Split('/');
            if (parts.Length == 0) return;
            string hostMap = parts[0];
            if (string.IsNullOrEmpty(hostMap) || hostMap == "Farm") return;

            GameLocation host = Game1.getLocationFromName(hostMap);
            if (host == null) return;

            // Land the player at their current tile on the real host map. The festival's
            // temporaryLocation is a clone of the host map with identical geometry, so
            // whatever tile the player was standing on when the festival closed (typically
            // wherever they happened to be when the auto-fade fired at 22:00, or wherever
            // they walked off the map edge) is a valid tile on the real one too. This is
            // the "exit like there wasn't a festival going on at all" behaviour the user
            // asked for — no forced warp to an entrance the player wasn't anywhere near.
            int tileX = (int)Game1.player.Tile.X;
            int tileY = (int)Game1.player.Tile.Y;
            __instance.setExitLocation(hostMap, tileX, tileY);
        }
    }
}
