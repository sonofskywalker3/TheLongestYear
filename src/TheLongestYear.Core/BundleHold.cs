using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The pure rule for the Fail-night "hold the town's wishes" choice (spec 2026-08-24).
/// Called BEFORE WorldResetService.PerformReset bumps CompletedResets. Mutates MetaState only
/// on Kept / Reshuffled; persistence is FinalizeReset's existing _store.Save().</summary>
public static class BundleHold
{
    public enum HoldResult
    {
        /// <summary>Board kept: JP deducted, ConsecutiveHolds incremented, BundleSeedLoop pinned.</summary>
        Kept,
        /// <summary>Board will reshuffle: counter reset, BundleSeedLoop advanced to the upcoming loop.</summary>
        Reshuffled,
        /// <summary>Player cannot afford this hold; nothing changed.</summary>
        NotEnoughJp
    }

    /// <summary>True for every bundle source (Jeff's ruling 2026-08-27: all three options can
    /// hold the current board).
    ///
    /// This used to return false for a vanilla board. The reasoning was sound at the time: a
    /// vanilla reset regenerates through <c>loadForNewGame</c> and never consults
    /// <see cref="MetaState.BundleSeedLoop"/>, so a hold would have charged JP for nothing.
    /// WorldResetService now snapshots the live board before the reset and writes it back
    /// afterwards, which reproduces a vanilla board exactly, so the hold is real on every source
    /// and the offer is no longer suppressed.
    ///
    /// Kept as a method rather than deleted: it documents the rule, and a future source that
    /// genuinely cannot hold would have somewhere to say so.</summary>
    public static bool IsOfferable(string? bundleSource) => true;

    /// <summary>Price the player would pay to hold right now.</summary>
    public static long NextCost(MetaState state, IReadOnlyList<long>? curve, double priceFactor = 1.0)
        => BundleHoldPricing.CostFor(state.ConsecutiveHolds, curve, priceFactor);

    /// <param name="priceFactor">The run's difficulty hold-price factor. MUST match what
    /// <see cref="NextCost"/> quoted the player, or the prompt and the deduction disagree.</param>
    public static HoldResult Apply(
        MetaState state, bool keep, IReadOnlyList<long>? curve, double priceFactor = 1.0)
    {
        if (!keep)
        {
            state.ConsecutiveHolds = 0;
            state.BundleSeedLoop = state.CompletedResets + 1;
            state.HoldChoiceMadeForReset = true;
            return HoldResult.Reshuffled;
        }

        long cost = NextCost(state, curve, priceFactor);
        if (state.JunimoPoints < cost)
            return HoldResult.NotEnoughJp;

        state.JunimoPoints -= cost;
        state.BundleSeedLoop = state.EffectiveBundleSeedLoop;   // materialize -1 to the current loop
        state.ConsecutiveHolds += 1;
        state.HoldChoiceMadeForReset = true;
        return HoldResult.Kept;
    }

    /// <summary>A reset that skipped the Fail-night hold choice (console tly_reset, post-win new
    /// loop) must behave like a reshuffle: pin the seed loop back to CompletedResets and zero the
    /// counter. Called by WorldResetService.PerformReset AFTER it bumps CompletedResets and before
    /// the board is generated. Always
    /// clears <see cref="MetaState.HoldChoiceMadeForReset"/>. Returns whether a choice had in fact
    /// been made (true = the hold/reshuffle answer from ShowHoldChoice or ApplyHoldChoice already
    /// applied its own state changes and this call is a no-op besides clearing the flag).</summary>
    public static bool ConsumeChoiceAtReset(MetaState state)
    {
        bool choiceMade = state.HoldChoiceMadeForReset;
        if (!choiceMade)
        {
            state.BundleSeedLoop = state.CompletedResets;
            state.ConsecutiveHolds = 0;
            SeasonPity.ClearBoardTrim(state);
            SeasonPity.ClearBoardEase(state);
        }
        state.HoldChoiceMadeForReset = false;
        return choiceMade;
    }
}
