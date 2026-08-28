using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Composes the per-domain rules into one <see cref="ItemAvailabilityModel"/>.
///
/// Phase 1 covers Fish, CrabPot and Metals, which are the pools the re-rolled PerItem bundles
/// draw from and therefore the largest part of the season-gate leak; those rules set a season
/// floor. Phase 2 (the <see cref="EffortComposer"/> rules, activity-themes spec 2026-08-28)
/// adds EFFORT ONLY for gems, geodes, monster drops, artifacts, animal products, artisan goods,
/// fish ponds, dishes, crops and forage: their floor stays the unrecognised Winter default, so no
/// gate moves, and the number feeds goal weighting and the review document.</summary>
public static class ItemAvailabilityBuilder
{
    public static ItemAvailabilityModel Build(
        ItemPools pools,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null,
        EffortData? effortData = null,
        bool hasKitchen = false)
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

        IReadOnlyDictionary<string, ItemEffort>? effortDerived = effortData != null
            ? new EffortComposer(effortData, derived, hasKitchen).DeriveAll()
            : null;

        return new ItemAvailabilityModel(derived, seasonOverrides, effortOverrides, effortDerived);
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
