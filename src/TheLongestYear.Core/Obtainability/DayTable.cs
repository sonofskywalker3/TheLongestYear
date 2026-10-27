using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Start day to landing day, 112 slots (Spring 1 = 1 .. Winter 28 = 112). The table is
/// monotone: <see cref="Lands"/>(d) is the earliest landing starting on ANY day at or after d, so
/// waiting is free and an item once had is held. That is the phase 2 meaning (spec
/// 2026-09-14-obtainability-phase2, decision 1 and 2): "start from nothing on day d, when does it
/// first land". Weeks are the language outside this type.</summary>
public sealed class DayTable : IEquatable<DayTable>
{
    public const int Days = Calendar.DaysPerYear;
    private const byte Never = 0;
    private const int FirstDay = 1;

    private readonly byte[] _lands;   // _lands[d - 1] = landing day for start day d, or Never

    private DayTable(byte[] lands) => _lands = lands;

    public static readonly DayTable None = new(new byte[Days]);
    public static readonly DayTable Always = Available(_ => true);

    /// <summary>A same-day source: lands on the first day at or after the start that is available.</summary>
    public static DayTable Available(Func<int, bool> availableOnDay)
    {
        var lands = new byte[Days];
        int next = Never;
        for (int day = Days; day >= FirstDay; day--)
        {
            if (availableOnDay(day)) next = day;
            lands[day - 1] = (byte)next;
        }
        return new DayTable(lands);
    }

    public static DayTable InWeeks(WeekMask weeks) => Available(day => weeks.Contains(WeekMask.WeekOfDay(day)));

    /// <summary>From an exact "start on p, land on x or never" rule; closes over waiting so a start
    /// day that fails still lands on the best later start.</summary>
    public static DayTable Exact(Func<int, int?> landingForStart)
    {
        var lands = new byte[Days];
        int best = Never;
        for (int day = Days; day >= FirstDay; day--)
        {
            int? landing = landingForStart(day);
            if (landing is int l && l >= day && l <= Days && (best == Never || l < best)) best = l;
            lands[day - 1] = (byte)best;
        }
        return new DayTable(lands);
    }

    public int? Lands(int startDay)
        => startDay < FirstDay || startDay > Days || _lands[startDay - 1] == Never ? null : _lands[startDay - 1];

    /// <summary>Tables built from <see cref="Available"/>, <see cref="Exact"/>, <see cref="Delay"/>,
    /// <see cref="Earliest"/> and <see cref="Latest"/> are waiting-closed, and <see cref="Then"/> is
    /// waiting-closed exactly when both tables it joins are: if day 1 never lands, nothing does,
    /// since waiting from day 1 reaches every later start. A table from <see cref="Except"/> is a
    /// per-start difference and can have a gap on an early day with real landings later, so this scans
    /// the whole table rather than trusting day 1 alone.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (byte b in _lands)
                if (b != Never) return false;
            return true;
        }
    }

    public int? LandingWeek(int startDay) => Lands(startDay) is int l ? WeekMask.WeekOfDay(l) : null;

    public bool CanObtain(int startDay, int deadlineDay) => Lands(startDay) is int l && l <= deadlineDay;

    public DayTable Delay(int days) => days <= 0 ? this : Map(l => l + days <= Days ? l + days : (int?)null);

    /// <summary>Land this, then start <paramref name="next"/> on that day.</summary>
    public DayTable Then(DayTable next) => Map(l => next.Lands(l));

    /// <summary>Either route: the sooner landing per start day.</summary>
    public DayTable Earliest(DayTable other)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
        {
            byte a = _lands[i], b = other._lands[i];
            lands[i] = a == Never ? b : b == Never ? a : Math.Min(a, b);
        }
        return new DayTable(lands);
    }

    /// <summary>The <see cref="Earliest"/> of every table, folded in one buffer; <see cref="None"/> when
    /// there are none.</summary>
    public static DayTable EarliestOf(IReadOnlyList<DayTable> tables)
    {
        if (tables.Count == 0) return None;
        var lands = (byte[])tables[0]._lands.Clone();
        for (int t = 1; t < tables.Count; t++)
        {
            byte[] other = tables[t]._lands;
            for (int i = 0; i < Days; i++)
            {
                byte a = lands[i], b = other[i];
                lands[i] = a == Never ? b : b == Never ? a : Math.Min(a, b);
            }
        }
        return new DayTable(lands);
    }

    /// <summary>Both needed (a recipe's ingredients): the later landing per start day, never if either never.</summary>
    public DayTable Latest(DayTable other)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
        {
            byte a = _lands[i], b = other._lands[i];
            lands[i] = a == Never || b == Never ? Never : Math.Max(a, b);
        }
        return new DayTable(lands);
    }

    /// <summary>The starts where this table lands and <paramref name="dependable"/> lands later or never:
    /// what luck alone adds. Deliberately not waiting-closed: this is a per-start difference ("what
    /// luck alone adds on THIS start day"), not a route of its own, so it is never folded over waiting
    /// the way <see cref="Available"/>/<see cref="Exact"/> are. Folding it would make a Chance source
    /// claim a landing earlier than the Dependable one it was subtracted from.</summary>
    public DayTable Except(DayTable dependable)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
        {
            byte a = _lands[i], d = dependable._lands[i];
            lands[i] = a != Never && (d == Never || d > a) ? a : Never;
        }
        return new DayTable(lands);
    }

    public static int SeasonStartDay(Season season) => (int)season * Calendar.DaysPerMonth + 1;

    private DayTable Map(Func<int, int?> f)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
            if (_lands[i] != Never && f(_lands[i]) is int l && l >= FirstDay && l <= Days) lands[i] = (byte)l;
        return new DayTable(lands);
    }

    public bool Equals(DayTable? other) => other is not null && _lands.AsSpan().SequenceEqual(other._lands);
    public override bool Equals(object? obj) => Equals(obj as DayTable);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_lands);
        return hash.ToHashCode();
    }

    /// <summary>"lands wk1/wk5/wk9/wk13": the landing week starting on each season's first day.</summary>
    public override string ToString()
    {
        if (IsEmpty) return "never";
        var parts = Enum.GetValues<Season>().Select(s => LandingWeek(SeasonStartDay(s)) is int w ? $"wk{w}" : "never");
        return "lands " + string.Join("/", parts);
    }
}
