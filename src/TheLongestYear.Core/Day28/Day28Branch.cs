namespace TheLongestYear.Core.Day28
{
    /// <summary>Which day-28 bedtime branch is queued (set by RunController.OnDayEnding from the
    /// gate's RunAction). None = no cutscene this morning.</summary>
    public enum Day28Branch
    {
        None,
        Fail,     // gate closed → rewind dialogue → JP shop → reset to Spring 1
        Continue, // gate open → congratulations → roll into the next season
        // 3 was Win (the win card), retired by the Year One Ending. PendingDay28 is persisted, so the
        // number stays unused: an old save holding 3 must never read back as a Restart.
        Restart = 4 // voluntary restart from the Junimo Shrine → no scene → the Fail chain (hold, upgrades, books, reset)
    }
}
