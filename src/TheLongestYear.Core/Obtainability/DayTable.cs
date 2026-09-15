using System;
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

    public bool IsEmpty => _lands[0] == Never;   // monotone: if day 1 never lands, nothing does

    public int? LandingWeek(int startDay) => Lands(startDay) is int l ? WeekMask.WeekOfDay(l) : null;

    public bool CanObtain(int startDay, int deadlineDay) => Lands(startDay) is int l && l <= deadlineDay;

    public DayTable Delay(int days) => days <= 0 ? this : Map(l => l + days <= Days ? l + days : (int?)null);

    /// <summary>Land this, then start <paramref name="next"/> on that day.</summary>
    public DayTable Then(DayTable next) => Map(l => next.Lands(l));

    /// <summary>Either route: the sooner landing per start day.</summary>
    public DayTable Earliest(DayTable other) => Combine(other, (a, b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value));

    /// <summary>Both needed (a recipe's ingredients): the later landing per start day, never if either never.</summary>
    public DayTable Latest(DayTable other) => Combine(other, (a, b) => a is null || b is null ? null : Math.Max(a.Value, b.Value));

    /// <summary>The starts where this table lands and <paramref name="dependable"/> lands later or never:
    /// what luck alone adds.</summary>
    public DayTable Except(DayTable dependable)
        => Combine(dependable, (a, d) => a is null ? null : d is null || d.Value > a.Value ? a : null);

    public static int SeasonStartDay(Season season) => (int)season * Calendar.DaysPerMonth + 1;

    private DayTable Map(Func<int, int?> f)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
            if (_lands[i] != Never && f(_lands[i]) is int l && l >= FirstDay && l <= Days) lands[i] = (byte)l;
        return new DayTable(lands);
    }

    private DayTable Combine(DayTable other, Func<int?, int?, int?> f)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
        {
            int? a = _lands[i] == Never ? null : _lands[i];
            int? b = other._lands[i] == Never ? null : other._lands[i];
            if (f(a, b) is int l) lands[i] = (byte)l;
        }
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
