using TheLongestYear.Core.Day28;

namespace TheLongestYear.Integration
{
    /// <summary>Builds the day-28 bedtime cutscene as a vanilla Event script: fade to black, a
    /// Junimo meep, one page of branch dialogue, then <c>end</c>. No <c>changeLocation</c> — the
    /// scene plays wherever the player wakes (the FarmHouse) and the <c>fade</c> command hides it.
    /// Not skippable (no <c>skippable</c> token): the driver detects "scene ended" by the event
    /// going inactive, and a skip would race that.</summary>
    internal static class Day28CutsceneInjector
    {
        public static string BuildEvent(Day28Branch branch)
        {
            string line = branch == Day28Branch.Fail
                ? Day28CutsceneContent.FailDialogue
                : Day28CutsceneContent.ContinueDialogue;

            return string.Join("/", new[]
            {
                "none",                         // music
                "3 9",                          // initial viewport (FarmHouse interior; irrelevant once black)
                "farmer 3 9 2",                 // place the farmer; restored to pre-event tile on end
                // (no "skippable" — forced scene)
                "addTemporaryActor Junimo 16 16 5 9 2 false character Junimo",
                "fade",                         // fade to black and HOLD (Event.Fade)
                "pause 700",
                "playSound junimoMeep1",
                "pause 400",
                $"speak Junimo \"{line}\"",
                "pause 500",
                "playSound junimoMeep1",
                "pause 900",
                "end"
            });
        }
    }
}
