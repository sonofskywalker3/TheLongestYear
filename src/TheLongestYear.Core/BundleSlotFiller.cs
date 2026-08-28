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

    /// <summary>Items that fish out at base quality only, whatever the roll says —
    /// see <see cref="RollQuality"/>. Public because <see cref="VanillaBoardDifficultyPass"/>
    /// must honour exactly the same set when the quality-asks modifier adds a star to a
    /// vanilla-authored board (Nexus 1122358: a quality ask on an item the game never stars is
    /// an impossible slot).</summary>
    public static readonly IReadOnlySet<string> BuiltInQualityIneligibleItemIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "(O)152", // Seaweed
            "(O)153", // Green Algae
            "(O)157", // White Algae
        };

    public static BundleSpec Fill(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        BundleGenerationTuning tuning, Random rng,
        PityTrim? trim = null, RarityThresholds? thresholds = null, Action<string>? log = null)
    {
        if (match.Domain == PoolDomain.None)
            return spec;

        IReadOnlyList<PoolItem> candidates = Candidates(spec, match, pools);
        int targetCount = spec.PickCount > 0
            ? Math.Min(spec.PickCount, spec.Slots.Count)
            : spec.Slots.Count;

        // Season pity, reshuffle path (spec 2026-08-25): quality-off costs one unit for the whole
        // bundle when the domain rolls quality; the rest remove the hardest candidates, never
        // below what this bundle needs to fill.
        bool qualityOff = false;
        if (TrimApplies(match, trim))
        {
            int before = candidates.Count;
            int units = trim!.Units;
            if (DomainRollsQuality(match.Domain) && units > 0)
            {
                qualityOff = true;
                units -= 1;
            }
            candidates = ItemHardness.Trim(candidates, units, targetCount, match.Domain, thresholds ?? new RarityThresholds());
            int after = candidates.Count;
            if (log != null)
            {
                int removed = before - after;
                string guardNote = after == targetCount && removed < units ? " (guard stopped early)" : "";
                log($"pity trim '{spec.Name}': {before} candidates -> {after} (units {trim.Units}, quality off {qualityOff}, need {targetCount}){guardNote}");
            }
        }

        if (candidates.Count < targetCount)
            return spec;

        List<PoolItem> chosen = WeightedSampler.Sample(candidates, targetCount, rng);
        var slots = chosen.Select(item => new BundleSlotSpec(
            item.ItemId,
            RollStack(match.Domain, item, tuning, rng),
            qualityOff ? 0 : RollQuality(match.Domain, item, pools, tuning, rng))).ToList();

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

    /// <summary>A trim applies to bundles feeding the trimmed season's gate: season-agnostic
    /// pools (Metals, ArtisanGoods, Fish, CrabPot, MonsterDrops, generic crops) feed every
    /// season, so they count; season-named bundles count only for their own season.</summary>
    public static bool TrimApplies(DomainMatch match, PityTrim? trim)
        => trim != null && trim.Units > 0 && match.Domain != PoolDomain.None
           && (match.Season == null || match.Season == trim.Season);

    /// <summary>Mirrors the domains <see cref="RollQuality"/> can give a silver/gold ask.</summary>
    public static bool DomainRollsQuality(PoolDomain domain)
        => domain is PoolDomain.QualityCrops or PoolDomain.SeasonalCrops or PoolDomain.SeasonalForage or PoolDomain.Fish;

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

    /// <summary>A season-named bundle asks only for items specific to that season, like
    /// vanilla's own Spring/Summer/Fall/Winter bundles. Any-season items (beach shellfish,
    /// desert fruit, an all-year modded crop) would otherwise sit in all four pools at full
    /// weight and crowd out the season's real forage (player report 2026-08-28, Mussel in four
    /// foraging bundles). A season-less bundle (null) still draws from the whole pool.</summary>
    private static IReadOnlyList<PoolItem> FilterSeason(IReadOnlyList<PoolItem> pool, Season? season)
        => season == null
            ? pool
            : pool.Where(p => p.Seasons.Count > 0 && p.Seasons.Contains(season.Value)).ToList();

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

    private static int RollQuality(
        PoolDomain domain, PoolItem item, ItemPools pools, BundleGenerationTuning tuning, Random rng)
    {
        // Items that can never carry a quality star (algae/seaweed) must not get a
        // silver/gold ask — the slot would be impossible to donate (Nexus 1122358).
        // Built-in set + config extension list (built-in because an existing config.json
        // overrides serialized list defaults wholesale — see ItemPoolBuilder.BuiltInExcludedItemIds).
        if (BuiltInQualityIneligibleItemIds.Contains(item.ItemId)
            || tuning.QualityIneligibleItemIds.Contains(item.ItemId))
            return 0;

        // Structural rule (2026-08-25): only items the game itself gives quality to may carry
        // a quality ask. Null = no eligibility data (hand-built pools), keep legacy behaviour.
        if (pools.QualityEligibleIds != null && !pools.QualityEligibleIds.Contains(item.ItemId))
            return 0;
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
