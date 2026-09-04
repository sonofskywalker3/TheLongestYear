using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Gives every fish and forage slot on a finished bundle its banded ask
/// (<see cref="FishAskBasis"/> or <see cref="ForageAskBasis"/> x <see cref="AskBands"/>), on the
/// engine path, right before the stack multiplier, which then skips those slots so difficulty is
/// not applied twice. Runs on every bundle the engine emits,
/// re-rolled or kept verbatim from vanilla, for the same reason StackScaling does: one dial, one
/// meaning, everywhere (Jeff, 2026-08-27).
///
/// A slot is banded when the item has a basis in a season the slot's deadline can reach; the
/// deadline comes from the caller (BundleSlotFiller.DeadlineFor, the same answer the classifier
/// will give). A gold ask keeps three quarters. Legendaries are pinned to one by
/// <see cref="LegendaryFishRules"/> before anything else. Returns the same reference when no slot
/// changed.</summary>
public static class QuantityAskPass
{
    private const int QualityGold = 2;

    public static BundleSpec Apply(BundleSpec spec, DifficultyProfile profile, Func<string, Season?> deadlineFor, Random rng)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (deadlineFor == null) throw new ArgumentNullException(nameof(deadlineFor));
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        List<BundleSlotSpec>? banded = null;
        for (int i = 0; i < spec.Slots.Count; i++)
        {
            BundleSlotSpec slot = spec.Slots[i];
            if (LegendaryFishRules.IsLegendary(slot.ItemId) || !Covers(slot.ItemId))
                continue;
            double? basis = BasisByDeadline(slot.ItemId, deadlineFor(slot.ItemId));
            if (basis == null)
                continue;
            int stack = AskBands.Roll(basis.Value, profile, rng);
            if (slot.Quality >= QualityGold)
                stack = AskBands.ForGold(stack);
            if (stack == slot.Stack)
                continue;
            banded ??= spec.Slots.ToList();
            banded[i] = slot with { Stack = stack };
        }
        return banded == null ? spec : spec with { Slots = banded };
    }

    /// <summary>True when the item's ask is banded by this pass, so the stack multiplier must
    /// leave it alone.</summary>
    public static bool Covers(string? itemId)
        => FishAskBasis.Covers(itemId) || ForageAskBasis.Covers(itemId);

    private static double? BasisByDeadline(string itemId, Season? deadline)
        => FishAskBasis.BasisByDeadline(itemId, deadline) ?? ForageAskBasis.BasisByDeadline(itemId, deadline);
}
