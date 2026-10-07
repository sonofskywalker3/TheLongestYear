using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard night_event (spec section 8): on the wildcard day's night one of the game's own random
    /// night events plays: the crop fairy, the witch, the meteorite, the stone owl or the strange
    /// capsule. Keyed on <see cref="RunState.WildcardNightTwist"/>, set at that day's DayEnding (the
    /// overnight <c>Utility.pickFarmEvent</c> already sees tomorrow's date); never the day-28 night.
    ///
    /// The postfix only fills an empty night: vanilla's own pick (a CC room scene, a vanilla random
    /// event) and <c>Game1.farmEventOverride</c> win, a wedding night stays empty as in vanilla, and a
    /// night <see cref="FarmEventSuppressionPatch"/> suppresses (a rewind morning) gets nothing.
    ///
    /// An event can cancel itself in <c>setUp()</c> (no ripe crop for the fairy, no coop for the witch,
    /// no clear spot for the meteorite). Game1 calls setUp right after the pick and drops the event
    /// when it returns true, so the try order (<see cref="WildcardNightEvents.TryOrder"/>) is walked
    /// in a setUp postfix: when the forced event cancels, the next one is set up in its place and
    /// written to <c>Game1.farmEvent</c>. Every cancel path in those setUps returns before it touches
    /// the screen, so a cancelled try leaves nothing behind. When all five cancel, the night is empty.
    /// </summary>
    [HarmonyPatch(typeof(Utility), nameof(Utility.pickFarmEvent))]
    internal static class WildcardNightEventPatch
    {
        private const int MeteoriteBehavior = 1;
        private const int OwlBehavior = 3;
        private const int CapsuleBehavior = 0;

        internal static IMonitor Monitor;

        /// <summary>The forced event Game1 is about to set up, and the ones to try after it.</summary>
        private static FarmEvent _forced;
        private static readonly Queue<(string id, FarmEvent ev)> Fallbacks = new();

        // ReSharper disable once InconsistentNaming: Harmony convention.
        // ReSharper disable once UnusedMember.Local: discovered by PatchAll.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref FarmEvent __result)
        {
            _forced = null;
            Fallbacks.Clear();
            if (!RunActivation.IsActive) return;
            if (__result != null) return;
            if (WildcardDayService.NightTwist() != WildcardSchedule.NightEvent) return;
            if (FarmEventSuppressionPatch.SuppressTonight?.Invoke() == true) return;
            if (Game1.farmEventOverride != null || IsWeddingNight()) return;
            RunState run = WildcardDayService.NightRun?.Invoke();
            if (run == null) return;

            var order = WildcardNightEvents.TryOrder(run.Seed, run.WildcardWeek);
            foreach (string id in order)
                Fallbacks.Enqueue((id, Create(id)));
            var (firstId, first) = Fallbacks.Dequeue();
            _forced = first;
            __result = first;
            Monitor?.Log($"Wildcard night event: forcing {firstId} tonight (try order {string.Join(", ", order)}).", LogLevel.Info);
        }

        /// <summary>Vanilla's pick returns no event on a wedding night; neither does this.</summary>
        private static bool IsWeddingNight()
        {
            if (Game1.weddingToday) return true;
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                Friendship spouse = farmer.GetSpouseFriendship();
                if (spouse != null && spouse.IsMarried() && spouse.WeddingDate == Game1.Date) return true;
            }
            return false;
        }

        private static FarmEvent Create(string id) => id switch
        {
            WildcardNightEvents.Fairy => new FairyEvent(),
            WildcardNightEvents.Witch => new WitchEvent(),
            WildcardNightEvents.Meteorite => new SoundInTheNightEvent(MeteoriteBehavior),
            WildcardNightEvents.Owl => new SoundInTheNightEvent(OwlBehavior),
            _ => new SoundInTheNightEvent(CapsuleBehavior),
        };

        /// <summary>Postfix on the three events' setUp: a cancelled forced event hands over to the
        /// next one in the try order.</summary>
        [HarmonyPatch]
        internal static class SetUpFallback
        {
            // ReSharper disable once UnusedMember.Local: Harmony.
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(FairyEvent), nameof(FairyEvent.setUp));
                yield return AccessTools.Method(typeof(WitchEvent), nameof(WitchEvent.setUp));
                yield return AccessTools.Method(typeof(SoundInTheNightEvent), nameof(SoundInTheNightEvent.setUp));
            }

            // ReSharper disable once InconsistentNaming: Harmony convention.
            // ReSharper disable once UnusedMember.Local: Harmony.
            private static void Postfix(FarmEvent __instance, ref bool __result)
            {
                if (_forced == null || !ReferenceEquals(__instance, _forced)) return;
                _forced = null;   // the nested setUp calls below must not re-enter this
                if (!RunActivation.IsActive || !__result)
                {
                    Fallbacks.Clear();
                    return;
                }
                while (Fallbacks.Count > 0)
                {
                    var (id, next) = Fallbacks.Dequeue();
                    if (next.setUp()) continue;
                    // Game1 keeps whatever is in farmEvent when setUp reports "go".
                    Game1.farmEvent = next;
                    __result = false;
                    Fallbacks.Clear();
                    Monitor?.Log($"Wildcard night event: the first pick cancelled itself; {id} plays instead.", LogLevel.Info);
                    return;
                }
                Monitor?.Log("Wildcard night event: every night event cancelled itself tonight; the night stays quiet.", LogLevel.Info);
            }
        }
    }
}
