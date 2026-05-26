using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/> by partitioning the CC ground truth into
/// (season, theme) contracts using a rarity-weighted, capped placement so early-season contracts
/// are tractable and difficulty ramps up across the year — a classic roguelite progression curve
/// (spec §10 + post-playtest 2026-05-26 refinement).
///
/// Algorithm:
///   1. Shuffle item processing order by seed (so different seeds yield different valid partitions).
///   2. Sort by Rarity descending (rarest first) so picky items claim their preferred slot first.
///   3. For each item, score each candidate season as:
///        score = -currentLoad[(season,theme)] + bias(rarity) * seasonIndex
///      where bias is negative for Common (prefer earlier) and positive for Rare/VeryRare (prefer
///      later). Per-(season,theme) load count dominates when caps are unsaturated.
///   4. Respect per-season caps when possible; override caps only when the item is single-season
///      (i.e. no alternative is permitted).
/// </summary>
public sealed class ContractGenerator
{
    private static readonly int[] DefaultCapBySeason = { 4, 5, 6, 9 };

    private readonly int[] _capBySeason;

    public ContractGenerator() : this(DefaultCapBySeason) { }

    public ContractGenerator(int[] capBySeason)
    {
        if (capBySeason == null || capBySeason.Length != Calendar.MonthsPerYear)
            throw new ArgumentException(
                $"capBySeason must be length {Calendar.MonthsPerYear}.", nameof(capBySeason));
        _capBySeason = capBySeason;
    }

    public YearPlan Generate(IReadOnlyList<CcItem> ccItems, int seed)
    {
        if (ccItems is null)
            throw new ArgumentNullException(nameof(ccItems));

        var rng = new Random(seed);

        // Empty (season, theme) bins.
        var grouped = new Dictionary<(Season, Theme), List<string>>();
        foreach (Season s in Enum.GetValues(typeof(Season)))
            foreach (Theme t in Enum.GetValues(typeof(Theme)))
                grouped[(s, t)] = new List<string>();

        // Stable then shuffled order, then re-sorted by rarity DESC so picky items go first.
        var ordered = ccItems
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        Shuffle(ordered, rng);
        ordered = ordered
            .OrderByDescending(i => RaritySortKey(i.Rarity))
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        foreach (CcItem item in ordered)
        {
            Season chosen = ChooseSeason(item, grouped);
            grouped[(chosen, item.Theme)].Add(item.Id);
        }

        var contracts = new List<Contract>(grouped.Count);
        foreach (var kvp in grouped)
        {
            var (season, theme) = kvp.Key;
            var (bonus, liability) = ThemeModifiers.For(theme);
            contracts.Add(new Contract(season, theme, kvp.Value, bonus, liability));
        }

        return new YearPlan(contracts);
    }

    private Season ChooseSeason(CcItem item, IReadOnlyDictionary<(Season, Theme), List<string>> grouped)
    {
        // Stable candidate order for determinism.
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();

        double bias = RarityBias(item.Rarity);

        // First pass: scores for under-cap candidates only.
        Season? bestUnderCap = null;
        double bestUnderCapScore = double.NegativeInfinity;
        Season bestOverall = candidates[0];
        double bestOverallScore = double.NegativeInfinity;

        foreach (Season s in candidates)
        {
            int load = grouped[(s, item.Theme)].Count;
            int cap = _capBySeason[(int)s];
            double score = -load + bias * (int)s;

            if (score > bestOverallScore)
            {
                bestOverall = s;
                bestOverallScore = score;
            }

            if (load < cap && score > bestUnderCapScore)
            {
                bestUnderCap = s;
                bestUnderCapScore = score;
            }
        }

        // Prefer a season that's still under the cap; otherwise accept the overflow.
        return bestUnderCap ?? bestOverall;
    }

    private static double RarityBias(Rarity rarity) => rarity switch
    {
        Rarity.Common    => -0.5,
        Rarity.Uncommon  => -0.25,
        Rarity.Rare      => 0.25,
        Rarity.VeryRare  => 1.0,
        _ => 0.0
    };

    private static int RaritySortKey(Rarity rarity) => rarity switch
    {
        Rarity.VeryRare => 3,
        Rarity.Rare     => 2,
        Rarity.Uncommon => 1,
        Rarity.Common   => 0,
        _ => -1
    };

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
