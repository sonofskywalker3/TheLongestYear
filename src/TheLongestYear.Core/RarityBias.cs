using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Applies the item-rarity modifier by reweighting the generation pools, rather than by
/// changing how the sampler works.
///
/// <see cref="WeightedSampler"/> already walks cumulative <see cref="PoolItem.Weight"/> values, so
/// biasing those weights up front shifts the sampled distribution with no edit to the sampler,
/// to <see cref="BundleSlotFiller"/>, or to <see cref="AuthoredBundleComposer"/>.
///
/// Hardness reuses <see cref="ItemHardness.Score"/>, the same 1-to-7 ranking the reshuffle-path
/// pity trim uses (rarity tier 1-4, +2 when the domain needs a station or recipe, +1 when the
/// item's earliest spawn season is Fall or Winter). A weight becomes
/// <c>round(weight * bias^(score - 1))</c>, so a score-1 item is never moved and everything
/// harder moves further the harder it is.
///
/// ENGINE BOARD ONLY. Changing which item a vanilla bundle asks for would be changing the bundle,
/// so this modifier has no vanilla equivalent and the GMCM row says so on its face.
///
/// Spec 2026-08-26 difficulty-modifiers, section 3.4.</summary>
public static class RarityBias
{
    private const int MinWeight = 1;

    /// <summary>Returns a reweighted copy of every pool. Returns the SAME reference when the bias
    /// is 1.0, so the default path allocates nothing.</summary>
    public static ItemPools Apply(ItemPools pools, double bias, RarityThresholds thresholds)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        if (thresholds == null) throw new ArgumentNullException(nameof(thresholds));

        if (bias == 1.0)
            return pools;

        // `with`, never a fresh ItemPools: every property this pass does NOT name carries over by
        // construction. Listing them by hand is how a property added later gets silently dropped
        // whenever the rarity modifier is on (final review, 2026-08-29).
        return pools with
        {
            Crops = Reweight(pools.Crops, bias, PoolDomain.SeasonalCrops, thresholds),
            Fish = Reweight(pools.Fish, bias, PoolDomain.Fish, thresholds),
            CrabPot = Reweight(pools.CrabPot, bias, PoolDomain.CrabPot, thresholds),
            Forage = Reweight(pools.Forage, bias, PoolDomain.SeasonalForage, thresholds),
            MonsterDrops = Reweight(pools.MonsterDrops, bias, PoolDomain.MonsterDrops, thresholds),
            Metals = Reweight(pools.Metals, bias, PoolDomain.Metals, thresholds),
            ArtisanGoods = Reweight(pools.ArtisanGoods, bias, PoolDomain.ArtisanGoods, thresholds),

            // These pools feed authored bundles rather than a slot domain, so they score with
            // PoolDomain.None: price and spawn season still count, the station bonus does not.
            Artifacts = Reweight(pools.Artifacts, bias, PoolDomain.None, thresholds),
            Books = Reweight(pools.Books, bias, PoolDomain.None, thresholds),
            Saplings = Reweight(pools.Saplings, bias, PoolDomain.None, thresholds),
            GeodeMinerals = Reweight(pools.GeodeMinerals, bias, PoolDomain.None, thresholds),
            Cooking = Reweight(pools.Cooking, bias, PoolDomain.None, thresholds),
            TapperGoods = Reweight(pools.TapperGoods, bias, PoolDomain.None, thresholds),

            // The recipe pools (Plan 3 Task 5). They feed every non-money bundle, so dropping
            // them here would silently empty every recipe whenever the rarity modifier is on.
            ByKind = pools.ByKind.ToDictionary(
                kv => kv.Key, kv => Reweight(kv.Value, bias, PoolDomain.None, thresholds)),
            ColourTags = pools.ColourTags.ToDictionary(
                kv => kv.Key, kv => Reweight(kv.Value, bias, PoolDomain.None, thresholds), StringComparer.Ordinal),
            WinterOnly = Reweight(pools.WinterOnly, bias, PoolDomain.None, thresholds),

            // Obtainability and quality-eligibility data (DerivedSeasonPins, QualityEligibleIds,
            // FishRows, TrapFishIds, FruitTreeFruitIds, ExcludedIds) are correctness, not
            // difficulty: they are rules, not weights, and the `with` above carries them unchanged.
        };
    }

    private static IReadOnlyList<PoolItem> Reweight(
        IReadOnlyList<PoolItem> pool, double bias, PoolDomain domain, RarityThresholds thresholds)
    {
        if (pool == null || pool.Count == 0)
            return pool ?? (IReadOnlyList<PoolItem>)Array.Empty<PoolItem>();

        var result = new List<PoolItem>(pool.Count);
        foreach (PoolItem item in pool)
        {
            int score = ItemHardness.Score(item, domain, thresholds);
            double scaled = item.Weight * Math.Pow(bias, score - 1);
            int weight = Math.Max(MinWeight, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
            result.Add(item with { Weight = weight });
        }
        return result;
    }
}
