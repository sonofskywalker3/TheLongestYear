using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Seeded re-roll of a picked bundle's slot contents from its domain's item
/// pool (spec "expanded-pool remix"): weighted sample without replacement (no duplicate
/// items per bundle), season filtering for seasonal domains, habitat filtering for fish,
/// stack/quality rolls from the BundleGenerationTuning block, and the large-quantity
/// forage ask. Returns the input spec UNCHANGED (reference-equal) when the domain is
/// None or the filtered pool cannot fill every slot with distinct items — the safe
/// fallback the caller logs.</summary>
public static class BundleSlotFiller
{
    private const int QualityGold = 2;
    private const int QualitySilver = 1;

    public static BundleSpec Fill(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        BundleGenerationTuning tuning, Random rng)
    {
        if (match.Domain == PoolDomain.None)
            return spec;

        IReadOnlyList<PoolItem> candidates = Candidates(spec, match, pools);
        int targetCount = spec.PickCount > 0
            ? Math.Min(spec.PickCount, spec.Slots.Count)
            : spec.Slots.Count;
        if (candidates.Count < targetCount)
            return spec;

        List<PoolItem> chosen = WeightedSampler.Sample(candidates, targetCount, rng);
        var slots = chosen.Select(item => new BundleSlotSpec(
            item.ItemId,
            RollStack(match.Domain, item, tuning, rng),
            RollQuality(match.Domain, tuning, rng))).ToList();

        if (match.Domain == PoolDomain.SeasonalForage
            && rng.NextDouble() < tuning.LargeQuantityForageChance)
        {
            int slotIndex = rng.Next(slots.Count);
            int stack = rng.Next(tuning.LargeQuantityMinStack, tuning.LargeQuantityMaxStack + 1);
            slots[slotIndex] = slots[slotIndex] with { Stack = stack, Quality = 0 };
        }

        return spec with
        {
            Slots = slots,
            NumberOfSlots = Math.Min(spec.NumberOfSlots, slots.Count),
        };
    }

    private static IReadOnlyList<PoolItem> Candidates(
        BundleSpec spec, DomainMatch match, ItemPools pools)
    {
        switch (match.Domain)
        {
            case PoolDomain.SeasonalCrops:
            case PoolDomain.QualityCrops:
                return FilterSeason(pools.Crops, match.Season);
            case PoolDomain.SeasonalForage:
                return FilterSeason(pools.Forage, match.Season);
            case PoolDomain.Fish:
                return FilterFishByHabitat(spec, pools.Fish);
            case PoolDomain.CrabPot:
                return pools.CrabPot;
            case PoolDomain.MonsterDrops:
                return pools.MonsterDrops;
            case PoolDomain.Metals:
                return pools.Metals;
            case PoolDomain.ArtisanGoods:
                return pools.ArtisanGoods;
            default:
                return Array.Empty<PoolItem>();
        }
    }

    private static IReadOnlyList<PoolItem> FilterSeason(IReadOnlyList<PoolItem> pool, Season? season)
        => season == null
            ? pool
            : pool.Where(p => p.Seasons.Count == 0 || p.Seasons.Contains(season.Value)).ToList();

    /// <summary>A fish bundle keeps its habitat identity: candidates are fish sharing at
    /// least one spawn location with the bundle's ORIGINAL fish (union empty — e.g. all
    /// originals unknown to the pool — falls back to the whole fish pool).</summary>
    private static IReadOnlyList<PoolItem> FilterFishByHabitat(
        BundleSpec spec, IReadOnlyList<PoolItem> fishPool)
    {
        var byId = fishPool.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var habitat = new HashSet<string>(StringComparer.Ordinal);
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            string normalizedId = BundleParsing.NormalizeItemId(slot.ItemId);
            if (!string.IsNullOrEmpty(normalizedId) && byId.TryGetValue(normalizedId, out var original))
                foreach (string location in original.Locations)
                    habitat.Add(location);
        }
        if (habitat.Count == 0)
            return fishPool;
        return fishPool.Where(p => p.Locations.Any(habitat.Contains)).ToList();
    }

    private static int RollStack(
        PoolDomain domain, PoolItem item, BundleGenerationTuning tuning, Random rng)
    {
        switch (domain)
        {
            case PoolDomain.QualityCrops:
                return tuning.QualityCropStack;
            case PoolDomain.MonsterDrops:
                if (item.Price < tuning.CheapPriceCeiling)
                    return rng.Next(tuning.CheapMinStack, tuning.CheapMaxStack + 1);
                if (item.Price < tuning.MidPriceCeiling)
                    return rng.Next(tuning.MidMinStack, tuning.MidMaxStack + 1);
                return rng.Next(tuning.DearMinStack, tuning.DearMaxStack + 1);
            default:
                return 1;
        }
    }

    private static int RollQuality(PoolDomain domain, BundleGenerationTuning tuning, Random rng)
    {
        switch (domain)
        {
            case PoolDomain.QualityCrops:
                return QualityGold;
            case PoolDomain.SeasonalCrops:
            case PoolDomain.SeasonalForage:
            case PoolDomain.Fish:
                if (rng.NextDouble() < tuning.GoldQualityChance) return QualityGold;
                if (rng.NextDouble() < tuning.SilverQualityChance) return QualitySilver;
                return 0;
            default:
                return 0;
        }
    }
}
