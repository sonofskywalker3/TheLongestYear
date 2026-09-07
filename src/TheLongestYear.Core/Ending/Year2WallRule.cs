namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 §5): Spring 1 of year 2 on a keep-playing save shows the
/// "Year 2 is coming" wall until the Year 2 update sets Year2Started. Legacy saves never armed it.</summary>
public static class Year2WallRule
{
    public static bool ShouldShow(int year, bool wallArmed, bool year2Started)
        => year >= 2 && wallArmed && !year2Started;
}
