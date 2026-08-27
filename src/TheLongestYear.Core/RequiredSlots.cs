using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Applies the required-slots modifier: how many of a bundle's SHOWN slots must actually
/// be donated. The slot list itself is never touched, so the board looks the same and only the
/// pick-X count moves.
///
/// This is the only ask-side modifier that raises the real total amount of work. The season gate
/// cannot do it: a bundle's quota ramp is capped at X, so raising an early season's quota only
/// steals from a later one (Jeff's ruling, spec section 1.1.1). Raising X itself is what actually
/// adds work.
///
/// Extreme (X == shown count) is safe with the rest of the system: BundleClassifier already
/// routes an X &gt;= Y bundle to KIND 2 PerItem, which requires every distinct ingredient, so
/// nothing has to special-case it.
///
/// Spec 2026-08-26 difficulty-modifiers, section 3.3.</summary>
public static class RequiredSlots
{
    private const string VaultRoom = "Vault";
    private const string MoneySlotId = "-1";

    /// <summary>Returns a spec with its pick-X count adjusted, clamped to
    /// <c>[1, Slots.Count]</c>. Returns the SAME reference when the modifier is Normal, when the
    /// bundle is a money bundle, or when the clamp lands on the value it already had.</summary>
    public static BundleSpec Apply(BundleSpec spec, DifficultyProfile profile)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        if (!profile.RequireAllSlots && profile.RequiredSlotsDelta == 0)
            return spec;

        // A Vault money bundle's slot count is structural, not a difficulty ask: the player pays
        // a sum, he does not donate 1-of-N items. VaultAmountMultiplier owns that number.
        if (IsMoneyBundle(spec))
            return spec;

        int shown = spec.Slots?.Count ?? 0;
        if (shown <= 0)
            return spec;

        int target = profile.RequireAllSlots
            ? shown
            : spec.NumberOfSlots + profile.RequiredSlotsDelta;

        target = Math.Clamp(target, 1, shown);
        return target == spec.NumberOfSlots ? spec : spec with { NumberOfSlots = target };
    }

    private static bool IsMoneyBundle(BundleSpec spec)
        => string.Equals(spec.Room, VaultRoom, StringComparison.OrdinalIgnoreCase)
           || (spec.Slots != null && spec.Slots.Any(s => s.ItemId == MoneySlotId));
}
