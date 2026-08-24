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

    /// <summary>Price the player would pay to hold right now.</summary>
    public static long NextCost(MetaState state, IReadOnlyList<long> curve)
        => BundleHoldPricing.CostFor(state.ConsecutiveHolds, curve);

    public static HoldResult Apply(MetaState state, bool keep, IReadOnlyList<long> curve)
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
}
