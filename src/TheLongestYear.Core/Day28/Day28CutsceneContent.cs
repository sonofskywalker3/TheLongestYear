namespace TheLongestYear.Core.Day28
{
    /// <summary>Dialogue for the day-28 bedtime Junimo cutscene. Kept in Core so the text is one
    /// source of truth; <c>Day28DialogueScript</c> parses it into pages and the self-drawn
    /// <c>Day28CutsceneMenu</c> renders it over black. It fires on every qualifying 28th — there is
    /// intentionally no cross-loop suppression (unlike the intro's HasSeenIntro).</summary>
    public static class Day28CutsceneContent
    {
        // Gate CLOSED used to show a static "cutscene.day28.fail" card here. Retired 2026-09-11
        // (spec 2026-09-11-rewind-cutscene, task 7): Day28CutsceneDriver now opens the played-out
        // rewind sequence (RewindBedroomScene -> RewindPanScene -> RewindSpringPaint) for the FAIL
        // branch instead of this menu, and that sequence's own bedroom beats 3/4
        // (cutscene.rewind.junimo-3/4) carry the "we will rewind the year" beat this card used to.

        /// <summary>Gate OPEN: on track; roll into the next season (no shop).</summary>
        public static string ContinueDialogue => Strings.Get("cutscene.day28.continue");
    }
}
