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

    /// <summary>True for a season index in range 0..3 (Spring..Winter). Replaces hand-rolled
    /// range checks against <see cref="Calendar.MonthsPerYear"/> scattered across the mod.</summary>
    public static bool IsValidSeasonIndex(int season) => season >= 0 && season < Calendar.MonthsPerYear;

    /// <summary>Called when the player lets time reshuffle on a Fail night, BEFORE the reset
    /// generates the new board. Records which season's pools get trimmed and by how much, or
    /// clears the stamp when no easing is due. A reshuffled board has no quota easing, so this
    /// also clears the keep-path ease stamp.</summary>
    public static void StampReshuffleTrim(MetaState state, GameplayConfig config)
    {
        int season = state.LastFailSeason;
        int units = IsValidSeasonIndex(season)
            ? TrimUnits(EaseSteps(state, (Season)season, config), config)
            : 0;
        state.BoardTrimSeason = units > 0 ? season : NoSeason;
        state.BoardTrimSteps = units;
        ClearBoardEase(state);
    }

    /// <summary>Clears the reshuffle trim stamp (a reset that skipped the Fail-night choice).</summary>
    public static void ClearBoardTrim(MetaState state)
    {
        state.BoardTrimSeason = NoSeason;
        state.BoardTrimSteps = 0;
    }

    /// <summary>Called on the Fail-night KEEP choice (after <see cref="BundleHold.Apply"/>
    /// succeeds, i.e. not NotEnoughJp), BEFORE the reset generates the new board. Stamps the
    /// season and steps the keep-path quota ease applies to, or clears the stamp when no easing
    /// is due. Winter is never stamped (spec section 2: quota easing never applies to Winter).</summary>
    public static void StampKeepEase(MetaState state, GameplayConfig config)
    {
        int season = state.LastFailSeason;
        if (IsValidSeasonIndex(season) && season != (int)Season.Winter)
        {
            int steps = EaseSteps(state, (Season)season, config);
            if (steps > 0)
            {
                state.BoardEaseSeason = season;
                state.BoardEaseSteps = steps;
                return;
            }
        }
        ClearBoardEase(state);
    }

    /// <summary>Clears the keep-path ease stamp (reshuffle, or a reset that skipped the
    /// Fail-night choice).</summary>
    public static void ClearBoardEase(MetaState state)
    {
        state.BoardEaseSeason = NoSeason;
        state.BoardEaseSteps = 0;
    }

    /// <summary>The keep-path quota easing in force for the current board, or null. Reads the
    /// stamp set by <see cref="StampKeepEase"/> at the Fail-night keep choice -- NOT live
    /// SeasonFailCounts/ConsecutiveHolds -- so a reload of a held board reproduces the same
    /// eased requirements even after RecordPass has dropped the fail counter back to the
    /// threshold mid-loop. Winter is never eased.</summary>
    public static SeasonEase? CurrentQuotaEase(MetaState state, GameplayConfig config)
    {
        if (!config.PityEnabled) return null;
        if (!IsValidSeasonIndex(state.BoardEaseSeason)) return null;
        if (state.BoardEaseSeason == (int)Season.Winter) return null;
        if (state.BoardEaseSteps <= 0) return null;
        var season = (Season)state.BoardEaseSeason;
        return new SeasonEase(season, state.BoardEaseSteps, QuotaFactor(state.BoardEaseSteps, config));
    }

    /// <summary>Steps to show in the Season Goals title: the quota ease stamp, else the
    /// trim stamped on the current board expressed in steps.</summary>
    public static int DisplaySteps(MetaState state, GameplayConfig config)
    {
        var ease = CurrentQuotaEase(state, config);
        if (ease != null) return ease.Steps;
        if (IsValidSeasonIndex(state.BoardTrimSeason) && config.PityTrimPerStep > 0)
            return state.BoardTrimSteps / config.PityTrimPerStep;
        return 0;
    }
}
