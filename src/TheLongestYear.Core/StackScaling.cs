using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The one definition of what the stack-size difficulty modifier does to a slot.
///
/// It exists because the modifier used to reach only half the board. The Engine path re-rolls the
/// slots of bundles whose theme it recognises and keeps every other bundle exactly as vanilla
/// wrote it; scaling the generator's tuning block therefore moved the re-rolled bundles and left
/// the kept ones untouched, while the Vanilla path (which has no re-rolling at all, so it scales
/// the finished board directly) moved everything. Same dial, two different reaches, and Engine is
/// the default. Measured on a real Hard board: three bundles scaled, six did not
/// (Sticky still asked Sap x500).
///
/// Jeff's ruling 2026-08-27: one dial, one meaning, everywhere. Both paths now scale every slot
/// through this class, and the generator's tuning block no longer carries stack scaling at all.
///
/// Spec 2026-08-26 difficulty-modifiers section 3.1, amended.</summary>
public static class StackScaling
{
    /// <summary>No slot may ask for less than one of an item.</summary>
    public const int MinStack = 1;

    /// <summary>A slot asking for more than one inventory stack of a 99-cap item reads as a bug
    /// rather than as difficulty. Kept deliberately (Jeff, 2026-08-27): it does mean a big vanilla
    /// ask like x100 is already at the ceiling and stops responding to the dial.</summary>
    public const int MaxStack = 99;

    private const string MoneySlotId = "-1";
    private const string VaultRoom = "Vault";

    /// <summary>The scalar rule, shared by the Engine path and the Vanilla post-pass so the two
    /// can never drift. Rounds away from zero, floors at 1, caps at 99.</summary>
    public static int ScaleStack(int stack, double factor)
    {
        if (factor == 1.0)
            return stack;
        int baseStack = stack > 0 ? stack : MinStack;
        return Math.Clamp(
            (int)Math.Round(baseStack * factor, MidpointRounding.AwayFromZero), MinStack, MaxStack);
    }

    /// <summary>Scales every non-money slot of a generated bundle. Returns the SAME reference when
    /// the factor is 1.0 or nothing changed, so the default path allocates nothing.
    ///
    /// Money bundles are skipped for the same reason <see cref="RequiredSlots"/> skips them: a
    /// Vault ask is a sum of gold, not a quantity of an item, and
    /// <c>PoolTuning.VaultAmountMultiplier</c> owns that number.</summary>
    public static BundleSpec Apply(BundleSpec spec, DifficultyProfile profile)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        if (profile.StackFactor == 1.0 || spec.Slots == null || spec.Slots.Count == 0)
            return spec;
        if (IsMoneyBundle(spec))
            return spec;

        List<BundleSlotSpec> scaled = null;
        for (int i = 0; i < spec.Slots.Count; i++)
        {
            BundleSlotSpec slot = spec.Slots[i];
            if (slot.ItemId == MoneySlotId)
                continue;

            int next = ScaleStack(slot.Stack, profile.StackFactor);
            if (next == slot.Stack)
                continue;

            scaled ??= spec.Slots.ToList();
            scaled[i] = slot with { Stack = next };
        }

        return scaled == null ? spec : spec with { Slots = scaled };
    }

    private static bool IsMoneyBundle(BundleSpec spec)
        => string.Equals(spec.Room, VaultRoom, StringComparison.OrdinalIgnoreCase)
           || spec.Slots.Any(s => s.ItemId == MoneySlotId);
}
