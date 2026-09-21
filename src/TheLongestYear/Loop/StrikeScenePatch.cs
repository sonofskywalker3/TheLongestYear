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
    /// unrecognised wins the slot, and the strike lands at once instead. An empty slot is not free
    /// either: vanilla reads it again for another mod's farmEventOverride and for a personal farm
    /// event (a birth, a couple's birth, a pregnancy question), and those win it too.
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
        private const int SoundDogs = 2;
        private const int SoundOwl = 3;

        /// <summary>QuestionEvent's barn birth: a per-animal roll that comes round again. Its 1 and 3
        /// are the pregnancy questions, which do not.</summary>
        private const int QuestionBarnBirth = 2;

        /// <summary>SoundInTheNightEvent keeps which one it is in a private net field.</summary>
        private static readonly FieldInfo SoundBehaviorField = AccessTools.Field(typeof(SoundInTheNightEvent), "behavior");

        /// <summary>QuestionEvent keeps which one it is in a private int field.</summary>
        private static readonly FieldInfo QuestionField = AccessTools.Field(typeof(QuestionEvent), "whichQuestion");

        private static bool IsRandom(FarmEvent e)
        {
            if (e is FairyEvent || e is WitchEvent) return true;
            if (e is SoundInTheNightEvent sound)
            {
                int which = SoundBehaviorOf(sound);
                return which == SoundCapsule || which == SoundMeteorite || which == SoundDogs || which == SoundOwl;
            }
            if (e is QuestionEvent question)
                return QuestionOf(question) == QuestionBarnBirth;
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

        /// <summary>Which question this is, or -1 when it cannot be read. Unknown keeps its night.</summary>
        private static int QuestionOf(QuestionEvent question)
        {
            try
            {
                return QuestionField?.GetValue(question) is int which ? which : -1;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Darkness: could not read which question tonight's event is, so it keeps the slot. {ex}", LogLevel.Trace);
                return -1;
            }
        }

        /// <summary>Would vanilla play a personal farm event tonight that cannot simply come round
        /// again? A birth, a couple's birth or a pregnancy question keeps its night. The barn birth
        /// and the dogs are rolled fresh every night, so they give way like the fairy does.
        ///
        /// Vanilla only reaches <c>pickPersonalFarmEvent</c> when <c>pickFarmEvent</c> came back null
        /// (Game1.cs:8134), so a scene that takes an empty slot would eat a birth, a couple's birth or
        /// a pregnancy question outright. The probe is safe to call: <c>pickPersonalFarmEvent</c>
        /// (Utility.cs:4496) seeds its own Random from the date and the save, reads friendship, the
        /// spouse and a game state query, mutates nothing, and the four events it can build have
        /// trivial constructors. It also never returns null outside a wedding, because it falls
        /// through to the barn birth or the dogs, which is why the answer is classified rather than
        /// null-checked.</summary>
        private static bool PersonalEventHasTheSlot()
        {
            try
            {
                FarmEvent personal = Utility.pickPersonalFarmEvent();
                return personal != null && !IsRandom(personal);
            }
            catch (Exception ex)
            {
                // The doubtful case keeps vanilla's night and the strike lands at once.
                Monitor?.Log($"Darkness: could not tell whether a personal farm event is due tonight, so the night is left to vanilla. {ex}", LogLevel.Error);
                return true;
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
            // An empty slot is not free. Vanilla reads it again twice: farmEventOverride, which
            // another mod may have queued, and then pickPersonalFarmEvent (Game1.cs:8122 and 8134).
            // Either one gets its night, and tonight's strike lands at once instead.
            if (__result == null)
            {
                if (Game1.weddingToday)
                {
                    ApplyNow?.Invoke("a wedding has the overnight slot");
                    return;
                }
                if (Game1.farmEventOverride != null)
                {
                    ApplyNow?.Invoke("another mod's farm event override has the overnight slot");
                    return;
                }
                if (PersonalEventHasTheSlot())
                {
                    ApplyNow?.Invoke("a personal farm event has the overnight slot");
                    return;
                }
            }
            FarmEvent scene = SceneFor();
            if (scene == null) return;
            Monitor?.Log(
                __result == null
                    ? "Darkness: the strike scene takes tonight's empty overnight slot."
                    : $"Darkness: the strike scene takes the overnight slot from {__result.GetType().Name}, which comes round again.",
                LogLevel.Trace);
            __result = scene;
        }
    }
}
