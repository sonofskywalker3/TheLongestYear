using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Drops the DayStarted SMAPI raises right after the post-reset save (see
    /// <see cref="PostSaveDayStartLatch"/>). SMAPI raises it from the same step as Saved, so the
    /// only place to stop it is its event's Raise. The class is generic and SMAPI-internal, so it is
    /// found by name; if a SMAPI update moves it, the patch is skipped with a warning and the reset
    /// behaves as before. ManagedEvent shares one compiled Raise across every event, so the prefix
    /// lets everything through unless armed, then drops only a DayStarted.</summary>
    internal static class ResetSaveDayStartPatch
    {
        private const string ManagedEventTypeName = "StardewModdingAPI.Framework.Events.ManagedEvent`1";

        internal static readonly PostSaveDayStartLatch Latch = new();

        private static IMonitor _monitor;

        internal static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;
            Type managedEvent = AccessTools.TypeByName(ManagedEventTypeName);
            MethodInfo raise = managedEvent == null
                ? null
                : AccessTools.Method(managedEvent.MakeGenericType(typeof(DayStartedEventArgs)), "Raise", new[] { typeof(DayStartedEventArgs) });
            if (raise == null)
            {
                monitor.Log(
                    "Day-28 save: SMAPI's event type was not found, so the post-reset save still raises a second day start. " +
                    "Mods that spawn things each morning (Farm Type Manager) may spawn twice on Spring 1.", LogLevel.Warn);
                return;
            }
            harmony.Patch(raise, prefix: new HarmonyMethod(typeof(ResetSaveDayStartPatch), nameof(Prefix)));
        }

        private static bool Prefix(object args)
        {
            if (!Latch.Armed || args is not DayStartedEventArgs)
                return true;
            Latch.TryConsume();
            _monitor?.Log(
                "Day-28 save: dropped the day start SMAPI raises after a save. The post-reset save is not a new day, " +
                "and mods already had Spring 1's day start after the reload.", LogLevel.Info);
            return false;
        }
    }
}
