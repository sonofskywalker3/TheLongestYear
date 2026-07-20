using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>Scales money bundle amounts and display names by a multiplier knob
/// (engine-owned Vault at +25%).
/// </summary>
public static class VaultAmountScaler
{
    private static readonly Regex MoneyNamePattern = new(@"^[\d,]+g$", RegexOptions.Compiled);

    /// <summary>Scale a bundle's money slots (ItemId "-1") by a multiplier.
    /// For each money slot, Stack becomes (int)Math.Round(stack * multiplier);
    /// Name and DisplayName become "N0" + "g" formatted with invariant culture
    /// when the ORIGINAL name matches ^[\d,]+g$, else unchanged.
    /// Non-money slots and multiplier 1.0 (same reference) are pass-through.
    /// </summary>
    public static BundleSpec Scale(BundleSpec spec, double multiplier)
    {
        if (multiplier == 1.0)
        {
            return spec;
        }

        var scaledSlots = new List<BundleSlotSpec>();
        foreach (var slot in spec.Slots)
        {
            if (slot.ItemId == "-1")
            {
                var scaledStack = (int)Math.Round(slot.Stack * multiplier);
                scaledSlots.Add(slot with { Stack = scaledStack });
            }
            else
            {
                scaledSlots.Add(slot);
            }
        }

        var newName = spec.Name;
        var newDisplayName = spec.DisplayName;

        if (MoneyNamePattern.IsMatch(spec.Name))
        {
            var scaledAmount = (int)Math.Round(spec.Slots[0].Stack * multiplier);
            var formattedAmount = scaledAmount.ToString("N0", CultureInfo.InvariantCulture) + "g";
            newName = formattedAmount;
            newDisplayName = formattedAmount;
        }

        return spec with { Slots = scaledSlots, Name = newName, DisplayName = newDisplayName };
    }
}
