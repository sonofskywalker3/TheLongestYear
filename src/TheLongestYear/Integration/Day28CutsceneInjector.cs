using TheLongestYear.Core.Day28;

namespace TheLongestYear.Integration
{
    /// <summary>Builds the day-28 bedtime cutscene as a vanilla Event script: a Junimo meep, one
    /// page of branch dialogue (shown with the portrait-less <c>message</c> box), then <c>end</c>.
    /// Design decisions, each from a 2026-06-03 playtest:
    /// <list type="bullet">
    /// <item>No vanilla fade command — plain <c>fade</c> reveals the room and <c>globalFade</c>
    ///   slow-fades then blinks back. Instead <see cref="Day28CutsceneDriver"/> paints a full-screen
    ///   black rectangle every frame while this event is active (matched by its event id), so the
    ///   dialogue renders over true black with neither the farmer nor a Junimo sprite visible.</item>
    /// <item><c>message</c>, not <c>speak Junimo</c> — Junimo has no portrait asset, so
    ///   <c>speak</c> made the dialogue box retry a portrait load every frame
    ///   (<c>NPC.TryLoadPortraits</c> FileNotFoundException spam, "smapi going nuts"). <c>message</c>
    ///   shows a plain text box with no speaker portrait; the meep SFX carry the Junimo voice. With
    ///   no speaker we also don't need an <c>addTemporaryActor</c>.</item>
    /// </list>
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
                "3 9",                          // initial viewport (FarmHouse interior; hidden by the black overlay)
                "farmer 3 9 2",                 // place the farmer; hidden behind the black, restored on end
                // (no "skippable" — forced scene)
                "pause 700",
                "playSound junimoMeep1",
                "pause 400",
                $"message \"{line}\"",
                "pause 500",
                "playSound junimoMeep1",
                "pause 900",
                "end"
            });
        }
    }
}
