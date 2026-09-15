using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The three fronts the darkness opens across the year (spec 2026-09-09 darkness pushback).</summary>
public enum SabotageKind { Blight, Reversion, Tampering }

/// <summary>Every number the darkness runs on, in one place so Jeff can retune without a hunt.
/// All values are the 2026-09-09 opening assumptions.</summary>
public static class SabotageTuning
{
    // Blight: crops on the farm die and perishables in chests spoil in the night. Summer, Fall
    // and Winter (Jeff, 2026-09-09: Winter blight is wanted; Winter fields are near empty so the
    // chests carry it).
    public const int BlightMinPerNight = 1;
    public const int BlightNightsPerWeek = 2;

    // Reversion: one filled slot in an unfinished bundle empties. Fall and Winter.
    /// <summary>First quiet day of a season: no reversion from here to day 28, so a redo has time.</summary>
    public const int ReversionQuietFromDay = 25;

    // Tampering: an unfilled slot asks for a different item. Winter only, unavoidable.
    public const int TamperPerSeason = 2;
    public const int TamperMinDaysApart = 5;
    /// <summary>First quiet day: no tampering from here to Winter 28.</summary>
    public const int TamperQuietFromDay = 21;
    /// <summary>How many of the closest-in-effort replacement candidates the roll picks among.</summary>
    public const int TamperCandidatePool = 5;
    public const int TamperQuality = 0;
    /// <summary>The tampered stack (Jeff, 2026-09-09): the item's normal max count, divided by the
    /// Winter weeks already passed, then a roll of this wide a slice per difficulty step: Easy
    /// 0 to 10%, Normal 10 to 20%, Hard 20 to 30%, Extreme 30 to 40%. A scramble, never a
    /// trade-down to something trivial.</summary>
    public const double TamperSliceWidth = 0.10;

    // The one nightly roll (spec 2026-09-15 Part B, section 2.1; Jeff's numbers 2026-09-14).
    public const double NightChanceSummer = 0.25;
    public const double NightChanceFall = 0.35;
    public const double NightChanceWinter = 0.35;
    /// <summary>Each strike lowers the week's chance by this much until the week resets.</summary>
    public const double NightChanceDecay = 0.05;

    // The gap tables the fairness picker adds on Normal and Hard (spec section 1.3; Jeff's first
    // draft, 2026-09-15). Index = skill level; days for a player deliberately working the skill.
    public static readonly int[] SkillDaysToLevel = { 0, 1, 2, 3, 5, 7, 10, 14, 19, 25, 32 };
    /// <summary>Regular mine: one day per this many floors below the deepest reached ("call it 10
    /// floors per day, we want it to be a stretch").</summary>
    public const int MineFloorsPerDay = 10;
    /// <summary>A missing machine the player can craft costs this many days on Normal.</summary>
    public const int MachineCraftDays = 1;
    /// <summary>Skull Cavern is a condition, not a wait: Staircases need this Mining level.</summary>
    public const int StaircaseMiningLevel = 2;
}

/// <summary>Which fronts are open, whether tonight rolls, and the per-week and per-season caps.
/// Pure: the glue supplies the run and the calendar, this decides.</summary>
public static class SabotageSchedule
{
    public static bool IsOpen(SabotageKind kind, Season season) => kind switch
    {
        SabotageKind.Blight => season != Season.Spring,
        SabotageKind.Reversion => season == Season.Fall || season == Season.Winter,
        SabotageKind.Tampering => season == Season.Winter,
        _ => false,
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

    /// <summary>Transitional: the three-dice roll the rework replaces. Task 6 removes the last
    /// caller; NightRoll owns the real decision.</summary>
    [System.Obsolete("Replaced by NightRoll; removed with the SabotageService rewrite.")]
    public static bool StrikesTonight(SabotageKind kind, RunState run, Season season, int dayOfMonth, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (!IsOpen(kind, season) || IsQuietDay(kind, dayOfMonth)) return false;
        int week = Calendar.WeekOfYear((int)season, dayOfMonth);
        int day = Calendar.DayOfYear((int)season, dayOfMonth);
        return WithinCaps(kind, run, week, day) && rng.NextDouble() < NightRoll.SeasonChance(season);
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

    /// <summary>The single night roll's stream (spec 2026-09-15 Part B): seed and day only, so it is
    /// distinct from every front's own stream.</summary>
    public static Random Rng(int runSeed, int dayOfYear)
    {
        unchecked
        {
            int hash = runSeed;
            hash = hash * 397 ^ dayOfYear * 7919;
            hash = hash * 397 ^ 15485863;
            return new Random(hash);
        }
    }
}
