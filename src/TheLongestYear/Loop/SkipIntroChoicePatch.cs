using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Loop
{
    /// <summary>Records the character-creation Skip intro checkbox and lets vanilla act on it. Off: GrandpaStory, the bus and the arrival event (replaced by OpeningEventInjector) play. On: vanilla's skip path, which marks 60367 seen and wakes the player in bed; OnSaveLoaded plants the cc-seen flag so the driver opens the picker.</summary>
    [HarmonyPatch(typeof(TitleMenu), nameof(TitleMenu.createdNewCharacter))]
    internal static class SkipIntroChoicePatch
    {
        internal static System.Func<bool> Enabled;
        internal static IMonitor Monitor;
        internal static readonly IntroSkipChoice Choice = new IntroSkipChoice();

        private static void Prefix(ref bool skipIntro)
        {
            if (Enabled == null || !Enabled())
                return;

            Choice.Record(skipIntro);
            Monitor?.Log(
                skipIntro
                    ? "SkipIntroChoice: player ticked Skip intro; vanilla skips to bed and the theme picker opens on Spring 1."
                    : "SkipIntroChoice: Skip intro left off; the opening plays (deathbed, cubicle, bus, arrival).",
                LogLevel.Info);
            // The value is left as the player set it: the vanilla chain now carries our opening
            // (spec 2026-09-16-expanded-opening-design.md, section 2.1).
        }
    }
}
