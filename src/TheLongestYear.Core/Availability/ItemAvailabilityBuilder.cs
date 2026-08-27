using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Composes the per-domain rules into one <see cref="ItemAvailabilityModel"/>.
///
/// Phase 1 covers Fish, CrabPot and Metals, which are the pools the re-rolled PerItem bundles
/// draw from and therefore the largest part of the season-gate leak. Items from pools not yet
/// covered are absent from the derived table and fall through to the model's unrecognised
/// default, which floors them at Winter. That is the safe direction while the remaining domains
/// land in later phases.</summary>
public static class ItemAvailabilityBuilder
{
    public static ItemAvailabilityModel Build(
        ItemPools pools,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));

        var derived = new Dictionary<string, ItemAvailability>(StringComparer.Ordinal);

        foreach (PoolItem item in pools.Fish ?? new List<PoolItem>())
            derived[item.ItemId] = FishAvailability.Derive(item, RowFor(pools, item.ItemId));

        foreach (PoolItem item in pools.CrabPot ?? new List<PoolItem>())
            derived[item.ItemId] = FishAvailability.Derive(item, RowFor(pools, item.ItemId));

        foreach (PoolItem item in pools.Metals ?? new List<PoolItem>())
        {
            ItemAvailability? metal = MetalsAvailability.Derive(item);
            if (metal != null)
                derived[item.ItemId] = metal;
        }

        return new ItemAvailabilityModel(derived, seasonOverrides, effortOverrides);
    }

    /// <summary>Pools carry qualified ids ("(O)128"); Data/Fish is keyed unqualified ("128").
    /// Strip the prefix to join them.</summary>
    private static RawFishEntry? RowFor(ItemPools pools, string qualifiedId)
    {
        string unqualified = BundleParsing.StripQualifier(qualifiedId);
        return pools.FishRows != null && pools.FishRows.TryGetValue(unqualified, out RawFishEntry? row)
            ? row
            : null;
    }
}
