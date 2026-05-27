using System;

namespace TheLongestYear.Core;

/// <summary>Pure helpers over Stardew's fixed 28-day month / 4-season year.</summary>
public static class Calendar
{
    public const int DaysPerWeek = 7;
    public const int DaysPerMonth = 28;
    public const int WeeksPerMonth = 4;
    public const int MonthsPerYear = 4;
    public const int WeeksPerYear = WeeksPerMonth * MonthsPerYear; // 16

    /// <summary>Week within the month (1-4) for a 1-based day of month.</summary>
    public static int WeekInMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > DaysPerMonth)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), dayOfMonth, "Day must be 1-28.");
        return ((dayOfMonth - 1) / DaysPerWeek) + 1;
    }

    public static bool IsWeekEnd(int dayOfMonth) => dayOfMonth % DaysPerWeek == 0;

    /// <summary>True on days 1, 8, 15, 22 — the morning of each in-month week.</summary>
    public static bool IsWeekStart(int dayOfMonth) => dayOfMonth % DaysPerWeek == 1;

    public static bool IsMonthEnd(int dayOfMonth) => dayOfMonth == DaysPerMonth;

    /// <summary>Week index across the whole year (1-16). monthIndex: Spring=0..Winter=3.</summary>
    public static int WeekOfYear(int monthIndex, int dayOfMonth)
        => monthIndex * WeeksPerMonth + WeekInMonth(dayOfMonth);
}
