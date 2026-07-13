namespace TheLongestYear.Core.Day28
{
    /// <summary>Dialogue for the day-28 bedtime Junimo cutscene. Kept in Core so the text is one
    /// source of truth; <c>Day28DialogueScript</c> parses it into pages and the self-drawn
    /// <c>Day28CutsceneMenu</c> renders it over black. It fires on every qualifying 28th — there is
    /// intentionally no cross-loop suppression (unlike the intro's HasSeenIntro).</summary>
    public static class Day28CutsceneContent
    {
        /// <summary>Gate CLOSED: the year will rewind; the Junimo offers a head-start (JP shop
        /// follows). <c>@</c> = player name; <c>#$b#</c> = dialogue-box page break; <c>$h</c> =
        /// happy portrait pose (harmless on the portrait-less Junimo).</summary>
        public static string FailDialogue => Strings.Get("cutscene.day28.fail");

        /// <summary>Gate OPEN: on track; roll into the next season (no shop).</summary>
        public static string ContinueDialogue => Strings.Get("cutscene.day28.continue");
    }
}
