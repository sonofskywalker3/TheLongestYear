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

    /// <summary>True when the hold choice is meaningful for this save's bundle board. In
    /// Vanilla mode (<see cref="BundleSourceNames.IsVanilla"/>) the reset regenerates the board
    /// via <c>loadForNewGame</c> and never consults <see cref="MetaState.BundleSeedLoop"/>, so
    /// holding would be a no-op that still charges JP; the hold prompt must not be offered.</summary>
    public static bool IsOfferable(string? bundleSource) => !BundleSourceNames.IsVanilla(bundleSource);

    /// <summary>Price the player would pay to hold right now.</summary>
    public static long NextCost(MetaState state, IReadOnlyList<long>? curve)
        => BundleHoldPricing.CostFor(state.ConsecutiveHolds, curve);

    public static HoldResult Apply(MetaState state, bool keep, IReadOnlyList<long>? curve)
    {
        if (!keep)
        {
            state.ConsecutiveHolds = 0;
            state.BundleSeedLoop = state.CompletedResets + 1;
            state.HoldChoiceMadeForReset = true;
            return HoldResult.Reshuffled;
        }

        long cost = NextCost(state, curve);
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
    /// counter. Called by WorldResetService.PerformReset BEFORE it bumps CompletedResets. Always
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
        }
        state.HoldChoiceMadeForReset = false;
        return choiceMade;
    }
}
