using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/>. Two-stage process:
///
///   1. <b>Per-item pinning (deterministic).</b> Sort items single-season-first then common-first
///      by rarity, place each into its least-loaded under-cap obtainable season; ties go to the
///      earliest candidate; force-place in earliest when all candidates are at cap. Seed is
///      ignored for placement. Overrides win over the algorithm if the pinned season is
///      obtainable for the item.
///
///   2. <b>Per-contract bonus sample (seed-driven).</b> For each (season, theme) pool, randomly
///      sample <c>BonusListSizeBySeason[season]</c> items as the per-run bonus list. The sample
///      re-rolls every reset (RunController calls Generate with a fresh seed), which is the
///      "bundle shuffle" the user asked for.
///
/// Gate requirement (how many pool items the player needs to donate to clear the contract) is
/// set from <see cref="GameplayConfig.GateRequirementBySeason"/> and capped at pool size.
/// </summary>
public sealed class ContractGenerator
{
    private static readonly int[] DefaultCapBySeason = { 15, 15, 15, 15 };
    private static readonly int[] DefaultGateBySeason = { 2, 3, 4, 4 };
    private static readonly int[] DefaultBonusBySeason = { 4, 5, 6, 7 };
    private static readonly IReadOnlyDictionary<string, Season> EmptyOverrides =
        new Dictionary<string, Season>();

    private readonly int[] _capBySeason;
    private readonly int[] _gateBySeason;
    private readonly int[] _bonusBySeason;
    private readonly IReadOnlyDictionary<string, Season> _overrides;

    public ContractGenerator()
        : this(DefaultCapBySeason, DefaultGateBySeason, DefaultBonusBySeason, EmptyOverrides) { }

    public ContractGenerator(int[] capBySeason)
        : this(capBySeason, DefaultGateBySeason, DefaultBonusBySeason, EmptyOverrides) { }

    public ContractGenerator(int[] capBySeason, IReadOnlyDictionary<string, Season> overrides)
        : this(capBySeason, DefaultGateBySeason, DefaultBonusBySeason, overrides) { }

    public ContractGenerator(
        int[] capBySeason,
        int[] gateBySeason,
        int[] bonusBySeason,
        IReadOnlyDictionary<string, Season> overrides)
    {
        if (capBySeason == null || capBySeason.Length != Calendar.MonthsPerYear)
            throw new ArgumentException($"capBySeason must be length {Calendar.MonthsPerYear}.", nameof(capBySeason));
        if (gateBySeason == null || gateBySeason.Length != Calendar.MonthsPerYear)
            throw new ArgumentException($"gateBySeason must be length {Calendar.MonthsPerYear}.", nameof(gateBySeason));
        if (bonusBySeason == null || bonusBySeason.Length != Calendar.MonthsPerYear)
            throw new ArgumentException($"bonusBySeason must be length {Calendar.MonthsPerYear}.", nameof(bonusBySeason));
        _capBySeason = capBySeason;
        _gateBySeason = gateBySeason;
        _bonusBySeason = bonusBySeason;
        _overrides = overrides ?? EmptyOverrides;
    }

    public YearPlan Generate(IReadOnlyList<CcItem> ccItems, int seed)
    {
        if (ccItems is null)
            throw new ArgumentNullException(nameof(ccItems));

        var grouped = new Dictionary<(Season, Theme), List<string>>();
        foreach (Season s in Enum.GetValues(typeof(Season)))
            foreach (Theme t in Enum.GetValues(typeof(Theme)))
                grouped[(s, t)] = new List<string>();

        var ordered = ccItems
            .OrderBy(i => i.ObtainableSeasons.Count)
            .ThenBy(i => RaritySortKey(i.Rarity))
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        foreach (CcItem item in ordered)
        {
            Season chosen = ChooseSeason(item, grouped);
            grouped[(chosen, item.Theme)].Add(item.Id);
        }

        // Bonus sample is seed-driven so each reset re-rolls the bonus visibility.
        var rng = new Random(seed);
        var contracts = new List<Contract>(grouped.Count);
        foreach (var kvp in grouped)
        {
            var (season, theme) = kvp.Key;
            var (bonusModId, liabilityModId) = ThemeModifiers.For(theme);
            int seasonIdx = (int)season;
            int gate = _gateBySeason[seasonIdx];
            int bonusSize = _bonusBySeason[seasonIdx];
            var bonusSample = SampleWithoutReplacement(kvp.Value, bonusSize, rng);
            contracts.Add(new Contract(season, theme, kvp.Value, gate, bonusSample, bonusModId, liabilityModId));
        }

        return new YearPlan(contracts);
    }

    private Season ChooseSeason(
        CcItem item,
        IReadOnlyDictionary<(Season, Theme), List<string>> grouped)
    {
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();

        if (_overrides.TryGetValue(item.Id, out Season pinned) && candidates.Contains(pinned))
            return pinned;

        Season? best = null;
        int bestLoad = int.MaxValue;
        foreach (Season s in candidates)
        {
            int load = grouped[(s, item.Theme)].Count;
            int cap = _capBySeason[(int)s];
            if (load >= cap)
                continue;
            if (load < bestLoad)
            {
                best = s;
                bestLoad = load;
            }
        }

        if (best.HasValue)
            return best.Value;

        return candidates[0];
    }

    /// <summary>Fisher-Yates partial shuffle: returns up to <paramref name="n"/> random items
    /// from <paramref name="source"/> without replacement.</summary>
    private static List<string> SampleWithoutReplacement(List<string> source, int n, Random rng)
    {
        if (n <= 0 || source.Count == 0)
            return new List<string>();
        n = Math.Min(n, source.Count);
        var pool = new List<string>(source);
        for (int i = 0; i < n; i++)
        {
            int j = i + rng.Next(pool.Count - i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, n);
    }

    private static int RaritySortKey(Rarity rarity) => rarity switch
    {
        Rarity.Common   => 0,
        Rarity.Uncommon => 1,
        Rarity.Rare     => 2,
        Rarity.VeryRare => 3,
        _ => 4
    };
}
