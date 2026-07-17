using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>Maps a picked bundle to the item pool its slots re-roll from. Name
/// fast-paths keep the seasonal bundles' identity (spec: seasonal bundles KEEP their
/// season); everything else is claimed by unambiguous ingredient-majority membership
/// in exactly one pool. None (composite/money/category/ambiguous bundles) = keep
/// vanilla slots — the safe default.</summary>
public static class PoolDomainClassifier
{
    private const string MoneySlotId = "-1";
    // Majority threshold: at least 2/3 of slots must belong to the claiming pool.
    private const int MajorityNumerator = 2;
    private const int MajorityDenominator = 3;

    private static readonly Regex SeasonalNamePattern = new(
        @"^(?<season>Spring|Summer|Fall|Winter)\s+(?<kind>Foraging|Crops)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static DomainMatch Classify(BundleSpec spec, ItemPools pools)
    {
        Match seasonal = SeasonalNamePattern.Match(spec.Name ?? "");
        if (seasonal.Success)
        {
            Season season = (Season)Enum.Parse(typeof(Season), seasonal.Groups["season"].Value, ignoreCase: true);
            PoolDomain domain = seasonal.Groups["kind"].Value.Equals("Crops", StringComparison.OrdinalIgnoreCase)
                ? PoolDomain.SeasonalCrops
                : PoolDomain.SeasonalForage;
            return new DomainMatch(domain, season);
        }
        if (string.Equals(spec.Name, "Quality Crops", StringComparison.OrdinalIgnoreCase))
            return new DomainMatch(PoolDomain.QualityCrops, null);

        var ids = new List<string>(spec.Slots.Count);
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            if (slot.ItemId == MoneySlotId || BundleParsing.IsCategoryRef(slot.ItemId))
                return new DomainMatch(PoolDomain.None, null);
            ids.Add(BundleParsing.NormalizeItemId(slot.ItemId));
        }
        if (ids.Count == 0)
            return new DomainMatch(PoolDomain.None, null);

        var candidates = new (PoolDomain Domain, IReadOnlyList<PoolItem> Pool)[]
        {
            (PoolDomain.Fish, pools.Fish),
            (PoolDomain.CrabPot, pools.CrabPot),
            (PoolDomain.MonsterDrops, pools.MonsterDrops),
            (PoolDomain.Metals, pools.Metals),
            (PoolDomain.ArtisanGoods, pools.ArtisanGoods),
            (PoolDomain.SeasonalCrops, pools.Crops), // generic crop bundle: any-season re-roll
        };

        PoolDomain claimed = PoolDomain.None;
        foreach ((PoolDomain domain, IReadOnlyList<PoolItem> pool) in candidates)
        {
            var member = new HashSet<string>(pool.Select(p => p.ItemId), StringComparer.Ordinal);
            int hits = ids.Count(member.Contains);
            if (hits * MajorityDenominator >= ids.Count * MajorityNumerator)
            {
                if (claimed != PoolDomain.None)
                    return new DomainMatch(PoolDomain.None, null); // ambiguous — keep vanilla
                claimed = domain;
            }
        }
        return new DomainMatch(claimed, null);
    }
}
