using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Per-(run, week, theme) random sample of "items in play this week" from the theme's bundles —
/// the bonus list that earns the 1.5× SelectionBonusMultiplier when donated. Pure and
/// seed-deterministic, so the planning hub's per-card preview and the selection-time commit
/// produce the same list for the same (seed, week, theme).
///
/// Sampling rule (per bundle, see <see cref="BundleRequirement.InPlayItemsFor"/>):
/// <list type="bullet">
///   <item>Seasonal: all ingredients when the season matches; nothing otherwise.</item>
///   <item>PerItem: ingredients pinned to this season.</item>
///   <item>Percentage: ingredients that pass the obtainability predicate, but only when the
///         bundle's quota is non-zero for this season (zero-quota bundles aren't urgent and
///         shouldn't pollute the sample — e.g. Adventurer's items on Spring).</item>
/// </list>
///
/// Rarity weighting (UX4 from the 2026-05-26 playtest): the planning-hub's bonus preview should
/// surface easier items more often. Each pool member is weighted inverse to its rarity (see
/// <see cref="WeightFor"/>) and picks are weighted-draws-without-replacement. Determinism is
/// preserved: same (seed, week, theme, pool, weights) → same picks.
/// </summary>
public static class BonusItemSampler
{
    private const int WeekSaltPrime = 7919;
    private const int ThemeSaltPrime = 1031;

    /// <summary>
    /// How many bonus items to show per (season, theme) preview card. Scales +1 per season so the
    /// player sees more bonus options later in the year. Indices match the <see cref="Season"/>
    /// enum (Spring=0..Winter=3). Spec round 6 lived in <c>GameplayConfig.BonusListSizeBySeason</c>;
    /// moved here in spec round 7 so the sampler owns its own default cap.
    /// </summary>
    public static readonly IReadOnlyList<int> DefaultMaxCountBySeason = new[] { 4, 5, 6, 7 };

    /// <summary>
    /// Inverse-rarity weight: Common shows up most often, VeryRare least. Tuned per the 2026-05-26
    /// playtest feedback (user expected Chub/Sardine to dominate Spring fishing samples, not Crab
    /// Pot items; expected to NOT see Solar/Void essences in Spring mining samples).
    /// </summary>
    public static int WeightFor(Rarity r) => r switch
    {
        Rarity.Common   => 8,
        Rarity.Uncommon => 4,
        Rarity.Rare     => 2,
        Rarity.VeryRare => 1,
        _ => 1
    };

    /// <summary>
    /// Sample up to <paramref name="maxCount"/> bonus items for <paramref name="theme"/> in
    /// <paramref name="currentSeason"/>. Returns a stable order for a given (seed, week, theme,
    /// pool, weights). If the pool is smaller than <paramref name="maxCount"/>, returns the
    /// whole pool (order is the weighted-draw order, not input order).
    /// </summary>
    public static IReadOnlyList<string> SampleForTheme(
        int runSeed, int weekOfYear,
        Theme theme,
        Season currentSeason,
        IReadOnlyList<BundleRequirement> bundles,
        Func<string, bool> isObtainableInSeason,
        Func<string, Rarity> rarityOf,
        int maxCount)
    {
        if (bundles is null) throw new ArgumentNullException(nameof(bundles));
        if (isObtainableInSeason is null) throw new ArgumentNullException(nameof(isObtainableInSeason));
        if (rarityOf is null) throw new ArgumentNullException(nameof(rarityOf));
        if (maxCount <= 0) return Array.Empty<string>();

        // Pool: distinct in-play items across the theme's bundles, tagged with rarity weight.
        Dictionary<string, int> poolWeights = new(StringComparer.Ordinal);
        foreach (BundleRequirement b in bundles)
        {
            if (b.Theme != theme) continue;
            foreach (string id in b.InPlayItemsFor(currentSeason, isObtainableInSeason))
                poolWeights[id] = WeightFor(rarityOf(id));
        }

        if (poolWeights.Count == 0) return Array.Empty<string>();

        // Stable input order keyed by id so the seeded weighted draws are reproducible.
        List<(string Id, int Weight)> remaining = poolWeights
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        Random rng = new Random(runSeed ^ (weekOfYear * WeekSaltPrime) ^ ((int)theme * ThemeSaltPrime));
        int take = Math.Min(maxCount, remaining.Count);
        List<string> result = new(take);
        for (int n = 0; n < take; n++)
        {
            int totalWeight = 0;
            for (int i = 0; i < remaining.Count; i++) totalWeight += remaining[i].Weight;

            int draw = rng.Next(totalWeight);
            int cum = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                cum += remaining[i].Weight;
                if (draw < cum)
                {
                    result.Add(remaining[i].Id);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return result;
    }
}
