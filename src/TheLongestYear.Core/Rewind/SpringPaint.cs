namespace TheLongestYear.Core.Rewind;

/// <summary>What Spring 1 has to look like from the end of the pan until the player takes control.
/// Jeff, 2026-09-11: the reset itself can happen afterwards, but the paint must be on the last
/// screen and must hold through the questions and the purchasing.</summary>
public readonly record struct SpringPaintValues(
    Season Season,
    int DayOfMonth,
    int TimeOfDay,
    bool Raining,
    bool Snowing,
    bool DebrisWeather);

public static class SpringPaint
{
    /// <summary>The default wake time, matching vanilla's 6am.</summary>
    public const int DefaultWakeTime = 600;

    public static SpringPaintValues Values(int wakeTime = DefaultWakeTime)
        => new(Season.Spring, 1, wakeTime, Raining: false, Snowing: false, DebrisWeather: false);
}
