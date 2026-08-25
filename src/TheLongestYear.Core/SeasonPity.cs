using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Pure rules for season pity (spec 2026-08-25): per-season fail counting, the ease
/// steps beyond the threshold, the keep-path quota factor and the reshuffle-path trim units.
/// Mutates MetaState only in RecordFail / RecordPass / StampReshuffleTrim.</summary>
public static class SeasonPity
{
    private const int NoSeason = -1;

    /// <summary>Ensures the list has exactly MonthsPerYear entries (old saves may be short or null).</summary>
    public static List<int> Counts(MetaState state)
    {
        state.SeasonFailCounts ??= new List<int>();
        while (state.SeasonFailCounts.Count < Calendar.MonthsPerYear)
            state.SeasonFailCounts.Add(0);
        return state.SeasonFailCounts;
    }

    public static void RecordFail(MetaState state, Season season)
    {
        Counts(state)[(int)season] += 1;
        state.LastFailSeason = (int)season;
    }

    public static void RecordPass(MetaState state, Season season, GameplayConfig config)
    {
        List<int> counts = Counts(state);
        int threshold = Math.Max(0, config.PityThreshold);
        counts[(int)season] = Math.Min(counts[(int)season], threshold);
    }

    public static int EaseSteps(MetaState state, Season season, GameplayConfig config)
    {
        if (!config.PityEnabled) return 0;
        return Math.Max(0, Counts(state)[(int)season] - Math.Max(0, config.PityThreshold));
    }

    public static double QuotaFactor(int steps, GameplayConfig config)
    {
        double step = Math.Clamp(config.PityQuotaStep, 0.0, 1.0);
        double floor = Math.Clamp(config.PityQuotaFloor, 0.0, 1.0);
        return Math.Max(floor, 1.0 - step * Math.Max(0, steps));
    }

    public static int TrimUnits(int steps, GameplayConfig config)
        => Math.Max(0, steps) * Math.Max(0, config.PityTrimPerStep);

    /// <summary>Called when the player lets time reshuffle on a Fail night, BEFORE the reset
    /// generates the new board. Records which season's pools get trimmed and by how much, or
    /// clears the stamp when no easing is due.</summary>
    public static void StampReshuffleTrim(MetaState state, GameplayConfig config)
    {
        int season = state.LastFailSeason;
        int units = season >= 0 && season < Calendar.MonthsPerYear
            ? TrimUnits(EaseSteps(state, (Season)season, config), config)
            : 0;
        state.BoardTrimSeason = units > 0 ? season : NoSeason;
        state.BoardTrimSteps = units;
    }

    /// <summary>Clears the reshuffle trim stamp (a reset that skipped the Fail-night choice).</summary>
    public static void ClearBoardTrim(MetaState state)
    {
        state.BoardTrimSeason = NoSeason;
        state.BoardTrimSteps = 0;
    }

    /// <summary>The keep-path quota easing in force for the current board, or null: the last
    /// failed season and its ease steps, only while the board is held (ConsecutiveHolds > 0).</summary>
    public static SeasonEase? CurrentQuotaEase(MetaState state, GameplayConfig config)
    {
        if (state.ConsecutiveHolds <= 0 || state.LastFailSeason < 0 || state.LastFailSeason >= Calendar.MonthsPerYear)
            return null;
        var season = (Season)state.LastFailSeason;
        int steps = EaseSteps(state, season, config);
        return steps > 0 ? new SeasonEase(season, steps, QuotaFactor(steps, config)) : null;
    }

    /// <summary>Steps to show in the Season Goals title: the quota ease while held, else the
    /// trim stamped on the current board expressed in steps.</summary>
    public static int DisplaySteps(MetaState state, GameplayConfig config)
    {
        var ease = CurrentQuotaEase(state, config);
        if (ease != null) return ease.Steps;
        if (state.BoardTrimSeason >= 0 && config.PityTrimPerStep > 0)
            return state.BoardTrimSteps / config.PityTrimPerStep;
        return 0;
    }
}
