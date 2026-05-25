using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/> by partitioning the CC ground truth into
/// (season, theme) contracts. Each item is placed in one season it is obtainable in
/// (seeded-random among candidates), so the union of all contracts always completes the
/// whole CC and nothing is scheduled out of season — the solvability guarantee (spec §10).
/// </summary>
public sealed class ContractGenerator
{
    public YearPlan Generate(IReadOnlyList<CcItem> ccItems, int seed)
    {
        if (ccItems is null)
            throw new ArgumentNullException(nameof(ccItems));

        var rng = new Random(seed);

        // Group required item ids by the (season, theme) slot we assign them to.
        var grouped = new Dictionary<(Season, Theme), List<string>>();
        foreach (Season s in Enum.GetValues(typeof(Season)))
            foreach (Theme t in Enum.GetValues(typeof(Theme)))
                grouped[(s, t)] = new List<string>();

        // Deterministic iteration order so a given seed always yields the same plan.
        foreach (CcItem item in ccItems.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            Season chosen = ChooseSeason(item, rng);
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

    private static Season ChooseSeason(CcItem item, Random rng)
    {
        // Stable candidate order, then a seeded pick — keeps generation deterministic.
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();
        return candidates[rng.Next(candidates.Count)];
    }
}
