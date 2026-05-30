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
    /// <c>Game1.cs:6619</c>, <c>DebugCommands.cs:2965</c> for canonical parsing). Then we
    /// call <see cref="Event.setExitLocation(Warp)"/> with the host map's first non-NPC
    /// warp tile so the player lands at the natural map entrance instead of the farm.
    ///
    /// 2026-05-29 STATUS.md small-followup item — was tagged as ~20 lines (endBehaviors
    /// postfix or transpiler); postfix path was the cleaner option.
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

            // Use the first non-NPC warp on the host map as the entry point — that's
            // typically the map's natural pedestrian entrance (south Town, south Beach,
            // north Forest, etc).
            Warp entry = host.GetFirstPlayerWarp();
            if (entry == null) return;

            __instance.setExitLocation(hostMap, entry.X, entry.Y);
        }
    }
}
