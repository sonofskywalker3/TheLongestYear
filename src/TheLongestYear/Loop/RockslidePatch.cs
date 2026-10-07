using System.Reflection;
using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard rockslide (spec section 8): the landslide rubble on the mine path, for the day. It is
    /// vanilla's own private <c>Mountain.landslide</c> NetBool (the rubble drawing and the collision
    /// block on the path); <see cref="MountainUnlock"/> clears it every loop. The minecart stop is
    /// inside the mine entrance, so the minecart route still reaches the mines.
    ///
    /// Set at the reveal (and on load), and again after every <c>Mountain.resetSharedState</c> that
    /// day, since vanilla clears it there once DaysPlayed reaches 5. Cleared at the day's end, before
    /// the save, so a save never holds the rubble; and only when this class set it, so the
    /// vanilla/MountainUnlock state is never undone on another day.
    /// </summary>
    [HarmonyPatch(typeof(Mountain), "resetSharedState")]
    internal static class RockslidePatch
    {
        private const string MountainName = "Mountain";

        private static readonly FieldInfo LandslideField = AccessTools.Field(typeof(Mountain), "landslide");

        /// <summary>True while the rubble on the Mountain is ours to remove.</summary>
        private static bool _weSet;

        // ReSharper disable once InconsistentNaming: Harmony convention.
        // ReSharper disable once UnusedMember.Local: discovered by PatchAll.
        private static void Postfix(Mountain __instance)
        {
            if (!RunActivation.IsActive || !DayEffects.Has(WildcardSchedule.Rockslide)) return;
            Block(__instance, null);
        }

        /// <summary>Today's state: the rubble on a rockslide day, our rubble gone on any other.</summary>
        public static void Sync(IMonitor monitor)
        {
            if (DayEffects.Has(WildcardSchedule.Rockslide))
                Block(Game1.getLocationFromName(MountainName) as Mountain, monitor);
            else
                Release(monitor);
        }

        /// <summary>The day's end or a rewind: remove the rubble if this class put it there.</summary>
        public static void Release(IMonitor monitor)
        {
            if (!_weSet) return;
            _weSet = false;
            if (Landslide(Game1.getLocationFromName(MountainName) as Mountain) is NetBool slide && slide.Value)
            {
                slide.Value = false;
                monitor?.Log("Wildcard rockslide: the path to the mines is clear again.", LogLevel.Info);
            }
        }

        /// <summary>Dormant / title: forget ownership (the location goes with the save).</summary>
        public static void Forget() => _weSet = false;

        private static void Block(Mountain mountain, IMonitor monitor)
        {
            if (!(Landslide(mountain) is NetBool slide)) return;
            if (slide.Value) return;   // already down: ours from earlier today, or vanilla's own
            slide.Value = true;
            _weSet = true;
            monitor?.Log("Wildcard rockslide: rubble blocks the path to the mines today.", LogLevel.Info);
        }

        private static NetBool Landslide(Mountain mountain)
            => mountain != null && LandslideField != null ? LandslideField.GetValue(mountain) as NetBool : null;
    }
}
