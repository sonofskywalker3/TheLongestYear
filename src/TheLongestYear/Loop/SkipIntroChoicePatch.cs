using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Loop
{
    /// <summary>Records the character-creation "Skip intro" checkbox for The Longest Year and
    /// keeps the VANILLA bus-ride intro skipped either way. <c>TitleMenu.createdNewCharacter</c>
    /// is the one place the checkbox value is committed, so a prefix reads it into
    /// <see cref="Choice"/> (consumed by the new-game load, which plants the cc-seen flag) and
    /// forces the argument to true: TLY's own Lewis to Junimo cutscene replaces the vanilla intro,
    /// and letting both play would greet the player twice.</summary>
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
                    ? "SkipIntroChoice: player ticked Skip intro; the opening cutscene will be skipped on this farm."
                    : "SkipIntroChoice: Skip intro left off; the opening cutscene will play.",
                LogLevel.Info);
            skipIntro = true; // the vanilla bus ride never plays under TLY
        }
    }
}
