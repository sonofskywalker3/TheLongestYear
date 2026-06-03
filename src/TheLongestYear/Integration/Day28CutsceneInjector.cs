using TheLongestYear.Core.Day28;

namespace TheLongestYear.Integration
{
    /// <summary>Builds the day-28 bedtime cutscene as a vanilla Event script: fade the screen to
    /// black and hold, a Junimo meep, one page of branch dialogue, then <c>end</c>. No
    /// <c>changeLocation</c> — the scene plays wherever the player wakes (the FarmHouse). We use
    /// <c>globalFade</c> (→ <c>Game1.globalFadeToBlack</c>), which fades to black and HOLDS it while
    /// the event continues — so the dialogue renders over a true black screen and neither the
    /// farmer nor the Junimo is visible. (The plain <c>fade</c> command sets <c>fadeIn</c> and
    /// reveals the room instead — 2026-06-03 playtest: "I can see the farm and the tip of my head".)
    /// The driver clears the hold (<c>Game1.globalFadeToClear</c>) after the event ends.
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
                "farmer 3 9 2",                 // place the farmer; hidden behind the black, restored on end
                // (no "skippable" — forced scene)
                "addTemporaryActor Junimo 16 16 5 9 2 false character Junimo",
                "globalFade",                   // fade to black and HOLD while the event continues
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
