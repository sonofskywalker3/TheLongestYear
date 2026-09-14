using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core.Obtainability;

/// <summary>A set of the year's 16 weeks, one bit each (bit 0 is week 1). Weeks are the unit the
/// obtainability model speaks in (Jeff, 2026-09-14): the same unit gates, goals and pacing use.</summary>
public readonly record struct WeekMask(ushort Bits)
{
    public const int FirstWeek = 1;
    public const int LastWeek = Calendar.WeeksPerYear;
    private const ushort AllBits = 0xFFFF;

    public static readonly WeekMask None = new(0);
    public static readonly WeekMask All = new(AllBits);

    public bool IsEmpty => Bits == 0;

    public bool Contains(int week)
        => week >= FirstWeek && week <= LastWeek && (Bits & (1 << (week - 1))) != 0;

    public static WeekMask Of(int week)
        => week < FirstWeek || week > LastWeek ? None : new((ushort)(1 << (week - 1)));

    public static WeekMask Range(int fromWeek, int toWeek)
    {
        ushort bits = 0;
        for (int w = Math.Max(FirstWeek, fromWeek); w <= Math.Min(LastWeek, toWeek); w++)
            bits |= (ushort)(1 << (w - 1));
        return new(bits);
    }

    public static WeekMask FromWeekOnwardOf(int week) => Range(week, LastWeek);

    public static WeekMask ForSeason(Season season)
    {
        int first = (int)season * Calendar.WeeksPerMonth + 1;
        return Range(first, first + Calendar.WeeksPerMonth - 1);
    }

    public static WeekMask ForSeasons(IEnumerable<Season> seasons)
    {
        WeekMask mask = None;
        foreach (Season s in seasons) mask |= ForSeason(s);
        return mask;
    }

    /// <summary>1-based day of the year (Spring 1 = 1, Winter 28 = 112) to its week.</summary>
    public static int WeekOfDay(int dayOfYear) => (dayOfYear - 1) / Calendar.DaysPerWeek + 1;

    public static WeekMask ForDays(int firstDayOfYear, int lastDayOfYear)
        => Range(WeekOfDay(firstDayOfYear), WeekOfDay(lastDayOfYear));

    public int? Earliest
    {
        get
        {
            for (int w = FirstWeek; w <= LastWeek; w++)
                if (Contains(w)) return w;
            return null;
        }
    }

    /// <summary>Every week from the earliest one on: a thing once had stays had (a fish pond, a
    /// learned recipe).</summary>
    public WeekMask FromWeekOnward() => Earliest is int e ? FromWeekOnwardOf(e) : None;

    /// <summary>Moves every week later by <paramref name="weeks"/>; weeks pushed past 16 are gone,
    /// because a loop ends at Winter 28.</summary>
    public WeekMask ShiftLater(int weeks) => weeks <= 0 ? this : new((ushort)((Bits << weeks) & AllBits));

    public WeekMask Except(WeekMask other) => new((ushort)(Bits & ~other.Bits));

    public static WeekMask operator |(WeekMask a, WeekMask b) => new((ushort)(a.Bits | b.Bits));
    public static WeekMask operator &(WeekMask a, WeekMask b) => new((ushort)(a.Bits & b.Bits));

    /// <summary>Compact ranges, e.g. "1-4,13-16". Empty is "none".</summary>
    public override string ToString()
    {
        if (IsEmpty) return "none";
        var sb = new StringBuilder();
        int w = FirstWeek;
        while (w <= LastWeek)
        {
            if (!Contains(w)) { w++; continue; }
            int start = w;
            while (w + 1 <= LastWeek && Contains(w + 1)) w++;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(start == w ? $"{start}" : $"{start}-{w}");
            w++;
        }
        return sb.ToString();
    }
}
