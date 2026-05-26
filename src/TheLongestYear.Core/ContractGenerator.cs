using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/> by partitioning the CC ground truth into
/// (season, theme) contracts. Difficulty ramps UP across the year (classic roguelite curve):
/// early-season contracts get the few items that ONLY exist in early seasons, multi-season
/// items get pushed to later seasons where the player has had time to invest in coops/barns/
/// preserves-jars/etc.
///
/// Algorithm (post-playtest 2026-05-26, revised same-day):
///   1. Shuffle item order by seed so different seeds yield different partitions.
///   2. Sort by ObtainableSeasons.Count ASC, then Rarity DESC. Single-season items go first
///      (no choice where they land), then multi-season items rare-first (so the rare ones
///      claim their preferred slot before the commons fill).
///   3. For each item, pick the LEAST-LOADED under-cap candidate season; ties go to the LATER
///      season. This spreads year-round items across the year (so Spring Mining isn't empty
///      while Winter Mining stacks up) while preserving the gentle bias toward later seasons.
///   4. If no candidate is under cap:
///        - Single-season item → force-place anyway (we can't drop CC items with no alternative).
///        - Multi-season item → DROP it. The user said this is OK ("It's ok to leave out some
///          of the items for the early season as long as they are available in later seasons").
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

        // Stable, shuffled by seed, then re-sorted: single-season-FIRST (they have no choice),
        // then rare-DESC inside each season-count tier so picky items claim their preferred slot
        // before the commons.
        var ordered = ccItems
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        Shuffle(ordered, rng);
        ordered = ordered
            .OrderBy(i => i.ObtainableSeasons.Count)
            .ThenByDescending(i => RaritySortKey(i.Rarity))
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        foreach (CcItem item in ordered)
        {
            if (TryChooseSeason(item, grouped, out Season chosen))
                grouped[(chosen, item.Theme)].Add(item.Id);
            // else: multi-season item, all candidates over cap → silently dropped
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

    /// <summary>
    /// Choose the season this item should go in. Picks the LEAST-LOADED under-cap candidate;
    /// ties resolve to the LATER season. Result: year-round items spread across seasons
    /// (post-playtest 2026-05-26: previously they all stacked into Winter, leaving early
    /// seasons of low-spread themes like Mining empty). Single-season items are force-placed
    /// when all candidates are at cap. Multi-season items with no room anywhere get dropped.
    /// </summary>
    private bool TryChooseSeason(
        CcItem item,
        IReadOnlyDictionary<(Season, Theme), List<string>> grouped,
        out Season chosen)
    {
        // Stable candidate order (earliest → latest) for determinism. Iterating in this
        // order and using `<=` on the load comparison gives latest-wins on ties for free.
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();

        Season? best = null;
        int bestLoad = int.MaxValue;
        foreach (Season s in candidates)
        {
            int load = grouped[(s, item.Theme)].Count;
            int cap = _capBySeason[(int)s];
            if (load >= cap)
                continue;
            if (load <= bestLoad)   // <= so later seasons win ties
            {
                best = s;
                bestLoad = load;
            }
        }

        if (best.HasValue)
        {
            chosen = best.Value;
            return true;
        }

        // Every candidate season is at cap.
        if (candidates.Count == 1)
        {
            // Single-season item — accept overflow (we can't drop it; the season is its only home).
            chosen = candidates[0];
            return true;
        }

        // Multi-season item with no room anywhere → drop it.
        chosen = default;
        return false;
    }

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
