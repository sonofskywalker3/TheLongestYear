namespace TheLongestYear.Core.Day28
{
    /// <summary>Which day-28 bedtime branch is queued (set by RunController.OnDayEnding from the
    /// gate's RunAction). None = no cutscene this morning.</summary>
    public enum Day28Branch
    {
        None,
        Fail,     // gate closed → rewind dialogue → JP shop → reset to Spring 1
        Continue  // gate open → congratulations → roll into the next season
    }
}
