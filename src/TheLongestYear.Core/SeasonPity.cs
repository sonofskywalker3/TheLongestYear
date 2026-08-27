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

    /// <summary>The run's resolved pity dials. Read through the STAMP (spec 2026-08-26) rather
    /// than straight off config, so the season-pity difficulty step applies and so a GMCM change
    /// only lands at the next reset. A legacy save with no stamp resolves to the config values,
    /// which is exactly what it used before.</summary>
    private static PityProfile Dials(MetaState state, GameplayConfig config)
        => state.EffectiveDifficulty(config).Pity;

    public static void RecordPass(MetaState state, Season season, GameplayConfig config)
    {
        List<int> counts = Counts(state);
        int threshold = Math.Max(0, Dials(state, config).Threshold);
        counts[(int)season] = Math.Min(counts[(int)season], threshold);
    }

    public static int EaseSteps(MetaState state, Season season, GameplayConfig config)
    {
        PityProfile dials = Dials(state, config);
        if (!dials.Enabled) return 0;
        return Math.Max(0, Counts(state)[(int)season] - Math.Max(0, dials.Threshold));
    }

    public static double QuotaFactor(int steps, GameplayConfig config)
        => QuotaFactor(steps, new PityProfile
        {
            QuotaStep = config.PityQuotaStep,
            QuotaFloor = config.PityQuotaFloor,
        });

    public static double QuotaFactor(int steps, PityProfile dials)
    {
        double step = Math.Clamp(dials.QuotaStep, 0.0, 1.0);
        double floor = Math.Clamp(dials.QuotaFloor, 0.0, 1.0);
        return Math.Max(floor, 1.0 - step * Math.Max(0, steps));
    }

    public static int TrimUnits(int steps, GameplayConfig config)
        => Math.Max(0, steps) * Math.Max(0, config.PityTrimPerStep);

    public static int TrimUnits(int steps, PityProfile dials)
        => Math.Max(0, steps) * Math.Max(0, dials.TrimPerStep);

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
            ? TrimUnits(EaseSteps(state, (Season)season, config), Dials(state, config))
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

    // ---- Opt-in offer (Jeff, 2026-08-25): the easing is offered as a separate Fail-night
    // ---- question after the hold choice, priced like the hold, and only applied on "yes".

    public enum PityOffer
    {
        /// <summary>Nothing to offer: no ease steps due, or a kept board on a Winter fail.</summary>
        None,
        /// <summary>Kept board: the failed season's quota comes down.</summary>
        Ease,
        /// <summary>Reshuffled board: the hardest eligible items are trimmed from the roll.</summary>
        Trim,
    }

    public enum PityResult { Applied, Declined, NotEnoughJp }

    /// <summary>What the Junimos can offer after the hold choice, given whether the board was
    /// kept (<paramref name="held"/>) or reshuffled.</summary>
    public static PityOffer OfferFor(MetaState state, bool held, GameplayConfig config)
    {
        int season = state.LastFailSeason;
        if (!IsValidSeasonIndex(season)) return PityOffer.None;
        int steps = EaseSteps(state, (Season)season, config);
        if (steps <= 0) return PityOffer.None;
        if (held)
            return season == (int)Season.Winter ? PityOffer.None : PityOffer.Ease;
        return TrimUnits(steps, config) > 0 ? PityOffer.Trim : PityOffer.None;
    }

    /// <summary>Price of accepting the next offer (first accept free by default).</summary>
    public static long PityCost(MetaState state, GameplayConfig config)
        => BundleHoldPricing.CostFor(
            state.ConsecutivePityUses, config.PityCosts,
            state.EffectiveDifficulty(config).HoldPriceFactor);

    /// <summary>The player said yes: charge the JP, count the accept, and stamp the easing for
    /// the chosen path. Nothing changes on NotEnoughJp.</summary>
    public static PityResult AcceptPity(MetaState state, bool held, GameplayConfig config)
    {
        if (OfferFor(state, held, config) == PityOffer.None)
        {
            DeclinePity(state, held);
            return PityResult.Declined;
        }
        long cost = PityCost(state, config);
        if (state.JunimoPoints < cost)
            return PityResult.NotEnoughJp;
        state.JunimoPoints -= cost;
        state.ConsecutivePityUses += 1;
        if (held)
            StampKeepEase(state, config);
        else
            StampReshuffleTrim(state, config);
        return PityResult.Applied;
    }

    /// <summary>The player said no (or nothing was offered): reset the accept counter and make
    /// sure no easing is stamped for the coming board. A kept board keeps its existing trim
    /// stamp (it is the same board); a reshuffled board starts clean.</summary>
    public static void DeclinePity(MetaState state, bool held)
    {
        state.ConsecutivePityUses = 0;
        ClearBoardEase(state);
        if (!held)
            ClearBoardTrim(state);
    }

    /// <summary>The keep-path quota easing in force for the current board, or null. Reads the
    /// stamp set by <see cref="StampKeepEase"/> at the Fail-night keep choice -- NOT live
    /// SeasonFailCounts/ConsecutiveHolds -- so a reload of a held board reproduces the same
    /// eased requirements even after RecordPass has dropped the fail counter back to the
    /// threshold mid-loop. Winter is never eased.</summary>
    public static SeasonEase? CurrentQuotaEase(MetaState state, GameplayConfig config)
    {
        if (!Dials(state, config).Enabled) return null;
        if (!IsValidSeasonIndex(state.BoardEaseSeason)) return null;
        if (state.BoardEaseSeason == (int)Season.Winter) return null;
        if (state.BoardEaseSteps <= 0) return null;
        var season = (Season)state.BoardEaseSeason;
        return new SeasonEase(season, state.BoardEaseSteps, QuotaFactor(state.BoardEaseSteps, Dials(state, config)));
    }

    /// <summary>Steps to show in the Season Goals title: the quota ease stamp, else the
    /// trim stamped on the current board expressed in steps.</summary>
    public static int DisplaySteps(MetaState state, GameplayConfig config)
    {
        var ease = CurrentQuotaEase(state, config);
        if (ease != null) return ease.Steps;
        int trimPerStep = Dials(state, config).TrimPerStep;
        if (IsValidSeasonIndex(state.BoardTrimSeason) && trimPerStep > 0)
            return state.BoardTrimSteps / trimPerStep;
        return 0;
    }
}
