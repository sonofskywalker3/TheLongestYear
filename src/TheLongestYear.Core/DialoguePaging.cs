namespace TheLongestYear.Core;

/// <summary>Appending a page to a dialogue that may already be on screen. Vanilla's DialogueBox closes
/// after a line unless that line is marked "continued on next screen" (Dialogue.cs
/// checkForSpecialDialogueAttributes: a "{" in the text sets the flag when the box reaches that line,
/// DialogueBox.cs receiveLeftClick: no flag, beginOutro). A line appended without the mark is never
/// shown. Which line needs the mark depends on whether the box is already showing the old last line.</summary>
public static class DialoguePaging
{
    /// <summary>The continuation marker vanilla strips from a line as it moves onto it.</summary>
    public const string ContinueMarker = "{";

    /// <summary>Sentinel: the box is on the old last line, so set its live flag instead.</summary>
    public const int SetLiveFlag = -1;

    /// <summary>Index of the line that should get <see cref="ContinueMarker"/> before a page is appended,
    /// or <see cref="SetLiveFlag"/> when the current line is already the last one (its flag was read
    /// when the box reached it, so a marker in its text would never be seen).</summary>
    public static int MarkerIndex(int currentIndex, int lineCount)
    {
        int last = lineCount - 1;
        return last > currentIndex ? last : SetLiveFlag;
    }
}
