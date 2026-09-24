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
    private const int TrapFishEffort = 2;

    public static ItemAvailabilityModel Build(
        ItemPools pools,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null,
        EffortData? effortData = null,
        bool hasKitchen = false,
        IReadOnlyDictionary<string, int>? weekOverrides = null,
        WeekMode mode = WeekMode.Pacing,
        DifficultyStep step = DifficultyStep.Normal)
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

        // Trap fish the crab-pot pool left out (the pool keeps a few per board) are still a fact
        // from Data/Fish: any of them from the week the crab pot recipe arrives.
        var trapDerivedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string trapId in pools.TrapFishIds ?? new HashSet<string>(StringComparer.Ordinal))
        {
            if (derived.ContainsKey(trapId)) continue;
            derived[trapId] = new ItemAvailability(Season.Spring, TrapFishEffort,
                $"crab pot catch (Data/Fish trap row), week {AvailabilityWeeks.TrapFishWeek}, effort {TrapFishEffort}",
                EffortSource.Derived, AvailabilityWeeks.TrapFishWeek, Season.Spring);
            trapDerivedIds.Add(trapId);
        }

        EffortComposer? composer = effortData != null
            ? new EffortComposer(effortData, derived, hasKitchen, pools.Saplings, pools.Artifacts, pools.Books, step)
            : null;
        IReadOnlyDictionary<string, ItemEffort>? effortDerived = composer?.DeriveAll();

        // A trap row is a lower bound, not the only route: Clam ((O)372) is also a Beach forage
        // spawn from day 1, which the trap-only entry above (week 2) can't see. Ask the composer
        // for every id that came solely from the trap loop and take the earlier week.
        if (composer != null)
        {
            foreach (string trapId in trapDerivedIds)
            {
                ItemAvailability current = derived[trapId];
                ItemEffort? composed = composer.Derive(trapId);
                if (composed?.EarliestWeek == null || composed.EarliestWeek >= current.EarliestWeek) continue;
                derived[trapId] = current with
                {
                    EarliestWeek = composed.EarliestWeek.Value,
                    HardWeek = composed.HardWeek ?? composed.EarliestWeek.Value,
                    Basis = $"{current.Basis}; forage route week {composed.EarliestWeek.Value}",
                };
            }
        }

        return new ItemAvailabilityModel(derived, seasonOverrides, effortOverrides, effortDerived, weekOverrides, mode, step);
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
