using System;
using System.Reflection;
using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Hands tonight's strike scene to the overnight slot (spec 2026-09-21). A random
    /// vanilla event (fairy, witch, meteorite, owl, capsule) gives way: it comes round again. A
    /// wedding, a WorldChangeEvent, the day-31 earthquake, the raccoon windstorm or anything
    /// unrecognised wins the slot, and the strike lands at once instead.
    ///
    /// <see cref="FarmEventSuppressionPatch"/> postfixes the same method and nulls the event on a
    /// fail night. Either order is safe: this one asks the same fail-night question itself and
    /// returns without touching anything.</summary>
    [HarmonyPatch(typeof(Utility), nameof(Utility.pickFarmEvent))]
    [HarmonyPriority(Priority.Last)]
    internal static class StrikeScenePatch
    {
        /// <summary>Set by ModEntry: tonight's scene, or null when none is waiting.</summary>
        internal static Func<FarmEvent> SceneFor;

        /// <summary>Set by ModEntry: land the waiting strike with no scene.</summary>
        internal static Action<string> ApplyNow;

        /// <summary>Set by ModEntry: true on a fail night (the same test the suppression patch uses).</summary>
        internal static Func<bool> FailNight;

        internal static IMonitor Monitor;

        /// <summary>SoundInTheNightEvent's behaviour values that a random roll can produce. The
        /// earthquake (4) is the scripted day-31 one and the windstorm (5) is the raccoon stump, so
        /// neither gives up its night.</summary>
        private const int SoundCapsule = 0;
        private const int SoundMeteorite = 1;
        private const int SoundOwl = 3;

        /// <summary>SoundInTheNightEvent keeps which one it is in a private net field.</summary>
        private static readonly FieldInfo SoundBehaviorField = AccessTools.Field(typeof(SoundInTheNightEvent), "behavior");

        private static bool IsRandom(FarmEvent e)
        {
            if (e is FairyEvent || e is WitchEvent) return true;
            if (e is SoundInTheNightEvent sound)
            {
                int which = SoundBehaviorOf(sound);
                return which == SoundCapsule || which == SoundMeteorite || which == SoundOwl;
            }
            return false;
        }

        /// <summary>Which sound-in-the-night this is, or -1 when it cannot be read. Unknown counts as
        /// scripted, so the doubtful case keeps its night and the strike lands at once.</summary>
        private static int SoundBehaviorOf(SoundInTheNightEvent sound)
        {
            try
            {
                return SoundBehaviorField?.GetValue(sound) is NetInt behavior ? behavior.Value : -1;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Darkness: could not read which sound-in-the-night tonight's event is, so it keeps the slot. {ex}", LogLevel.Trace);
                return -1;
            }
        }

        // ReSharper disable once InconsistentNaming (Harmony convention).
        // ReSharper disable once UnusedMember.Local (discovered by the patch pass).
        private static void Postfix(ref FarmEvent __result)
        {
            if (!RunActivation.IsActive || SceneFor == null) return;
            // A fail night is rewound in the morning, so nothing overnight is allowed to run. Nothing
            // should be pending either (RunController only runs the night pass on ordinary nights),
            // but leave the slot exactly as it is either way.
            if (FailNight != null && FailNight()) return;
            if (__result != null && !IsRandom(__result))
            {
                ApplyNow?.Invoke($"{__result.GetType().Name} has the overnight slot");
                return;
            }
            FarmEvent scene = SceneFor();
            if (scene == null) return;
            if (__result != null)
                Monitor?.Log($"Darkness: the strike scene takes the overnight slot from {__result.GetType().Name}, which comes round again.", LogLevel.Trace);
            __result = scene;
        }
    }
}
