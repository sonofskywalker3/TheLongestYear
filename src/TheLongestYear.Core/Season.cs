using System;

namespace TheLongestYear.Core;

/// <summary>The four seasons, ordered to match Stardew's month index (Spring=0..Winter=3).</summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3
}

public static class SeasonExtensions
{
    public static Season FromMonthIndex(int monthIndex)
    {
        if (monthIndex is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(monthIndex), monthIndex, "Month index must be 0-3.");
        return (Season)monthIndex;
    }

    public static int ToMonthIndex(this Season season) => (int)season;
}
