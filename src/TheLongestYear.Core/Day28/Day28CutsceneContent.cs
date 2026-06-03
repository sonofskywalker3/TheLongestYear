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
        public const string FailDialogue =
            "At this pace we won't be able to restore the Community Center in time, @.#$b#" +
            "So we will use our magic to rewind the year — but don't worry. " +
            "We have enough power left over to give you a head-start this time.$h";

        /// <summary>Gate OPEN: on track; roll into the next season (no shop).</summary>
        public const string ContinueDialogue =
            "Great job, @ — you're doing well!#$b#" +
            "Keep this up and we'll save the valley together. " +
            "We'll gain even more power from the work you do this season.$h";
    }
}
