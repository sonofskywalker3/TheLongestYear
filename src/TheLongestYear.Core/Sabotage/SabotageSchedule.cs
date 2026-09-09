using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The three fronts the darkness opens across the year (spec 2026-09-09 darkness pushback).</summary>
public enum SabotageKind { Blight, Reversion, Tampering }

/// <summary>Every number the darkness runs on, in one place so Jeff can retune without a hunt.
/// All values are the 2026-09-09 opening assumptions.</summary>
public static class SabotageTuning
{
    // Blight: crops on the farm die in the night. Summer and Fall only (Winter is the hall's
    // season, and the turn line promises "it will not rot your crops").
    public const double BlightChanceSummer = 0.25;
    public const double BlightChanceFall = 0.35;
    public const double BlightShareSummer = 0.04;
    public const double BlightShareFall = 0.06;
    public const int BlightMinPerNight = 1;
    public const int BlightMaxSummer = 6;
    public const int BlightMaxFall = 10;
    public const int BlightNightsPerWeek = 2;

    // Reversion: one filled slot in an unfinished bundle empties. Fall and Winter.
    public const double ReversionChanceFall = 0.20;
    public const double ReversionChanceWinter = 0.30;
    /// <summary>First quiet day of a season: no reversion from here to day 28, so a redo has time.</summary>
    public const int ReversionQuietFromDay = 25;

    // Tampering: an unfilled slot asks for a different item. Winter only, unavoidable.
    public const double TamperChance = 0.15;
    public const int TamperPerSeason = 2;
    public const int TamperMinDaysApart = 5;
    /// <summary>First quiet day: no tampering from here to Winter 28.</summary>
    public const int TamperQuietFromDay = 21;
    /// <summary>How many of the closest-in-effort replacement candidates the roll picks among.</summary>
    public const int TamperCandidatePool = 5;
    public const int TamperStack = 1;
    public const int TamperQuality = 0;
}

/// <summary>Which fronts are open, whether tonight rolls, and the per-week and per-season caps.
/// Pure: the glue supplies the run and the calendar, this decides.</summary>
public static class SabotageSchedule
{
    public static bool IsOpen(SabotageKind kind, Season season) => kind switch
    {
        SabotageKind.Blight => season == Season.Summer || season == Season.Fall,
        SabotageKind.Reversion => season == Season.Fall || season == Season.Winter,
        SabotageKind.Tampering => season == Season.Winter,
        _ => false,
    };

    public static double NightlyChance(SabotageKind kind, Season season) => (kind, season) switch
    {
        (SabotageKind.Blight, Season.Summer) => SabotageTuning.BlightChanceSummer,
        (SabotageKind.Blight, Season.Fall) => SabotageTuning.BlightChanceFall,
        (SabotageKind.Reversion, Season.Fall) => SabotageTuning.ReversionChanceFall,
        (SabotageKind.Reversion, Season.Winter) => SabotageTuning.ReversionChanceWinter,
        (SabotageKind.Tampering, Season.Winter) => SabotageTuning.TamperChance,
        _ => 0.0,
    };

    /// <summary>A front's quiet stretch at the end of a season. Day 28 itself is never rolled by
    /// the caller (the gate owns that night).</summary>
    public static bool IsQuietDay(SabotageKind kind, int dayOfMonth) => kind switch
    {
        SabotageKind.Reversion => dayOfMonth >= SabotageTuning.ReversionQuietFromDay,
        SabotageKind.Tampering => dayOfMonth >= SabotageTuning.TamperQuietFromDay,
        _ => false,
    };

    /// <summary>The caps: blight at most N nights a week, reversion once a week, tampering twice a
    /// Winter and never within a few days of the last one.</summary>
    public static bool WithinCaps(SabotageKind kind, RunState run, int weekOfYear, int dayOfYear)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        switch (kind)
        {
            case SabotageKind.Blight:
                return run.BlightWeek != weekOfYear || run.BlightNightsThisWeek < SabotageTuning.BlightNightsPerWeek;
            case SabotageKind.Reversion:
                return run.LastReversionWeek != weekOfYear;
            case SabotageKind.Tampering:
                if (run.TamperDays.Count >= SabotageTuning.TamperPerSeason) return false;
                foreach (int day in run.TamperDays)
                    if (Math.Abs(dayOfYear - day) < SabotageTuning.TamperMinDaysApart) return false;
                return true;
            default:
                return false;
        }
    }

    /// <summary>One decision: open, not quiet, under the caps, and the dice say yes.</summary>
    public static bool StrikesTonight(SabotageKind kind, RunState run, Season season, int dayOfMonth, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (!IsOpen(kind, season)) return false;
        if (IsQuietDay(kind, dayOfMonth)) return false;
        int week = Calendar.WeekOfYear((int)season, dayOfMonth);
        int day = Calendar.DayOfYear((int)season, dayOfMonth);
        if (!WithinCaps(kind, run, week, day)) return false;
        return rng.NextDouble() < NightlyChance(kind, season);
    }

    /// <summary>Record that a front struck, so the caps see it.</summary>
    public static void RecordStrike(SabotageKind kind, RunState run, int weekOfYear, int dayOfYear)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        switch (kind)
        {
            case SabotageKind.Blight:
                if (run.BlightWeek != weekOfYear) { run.BlightWeek = weekOfYear; run.BlightNightsThisWeek = 0; }
                run.BlightNightsThisWeek++;
                break;
            case SabotageKind.Reversion:
                run.LastReversionWeek = weekOfYear;
                break;
            case SabotageKind.Tampering:
                run.TamperDays.Add(dayOfYear);
                break;
        }
    }

    /// <summary>A night's random stream, fixed by the run seed, the day and the front: re-sleeping the
    /// same night rolls the same outcome, so there is nothing to save-scum.</summary>
    public static Random Rng(int runSeed, int dayOfYear, SabotageKind kind)
    {
        unchecked
        {
            int hash = runSeed;
            hash = hash * 397 ^ dayOfYear * 7919;
            hash = hash * 397 ^ ((int)kind + 1) * 104729;
            return new Random(hash);
        }
    }
}
