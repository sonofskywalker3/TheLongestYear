using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/> by partitioning the CC ground truth into
/// (season, theme) contracts. Spec change 2026-05-26 (round 2): placement is now a HARD
/// per-item assignment (every CC item has one assigned season, decided here), and the gate
/// requires every item assigned to a season to be donated by that season's end.
///
/// Algorithm:
///   1. Per-item override (config.SeasonOverrides): if a pinned season is in the item's
///      obtainable set, use it unconditionally. Otherwise:
///   2. Sort items deterministically — single-season first (force-placed in their only season),
///      then multi-season items in COMMON-first rarity order, with id alpha as the final tie.
///      Common-first means easy items claim earlier seasons; rarer items get pushed later
///      by the cap.
///   3. Place each item in its LEAST-LOADED under-cap obtainable season; on a tie go to the
///      EARLIEST candidate. This spreads year-round items across the year (so "copper bar
///      Spring, iron Summer, gold Fall" falls out for free given the rarity-ASC sort and
///      enough Mining items already present).
///   4. If every candidate is at cap, place in the EARLIEST obtainable season anyway. We never
///      drop items: every CC item MUST land somewhere or Win becomes unreachable.
///
/// Determinism: the seed parameter is unused for placement (kept in the API so the existing
/// reroll plumbing still compiles). Two callers with the same catalog + overrides get the
/// same plan. The user reviews the assignment via the dump-plan log emitted by RunController.
/// </summary>
public sealed class ContractGenerator
{
    private static readonly int[] DefaultCapBySeason = { 4, 5, 6, 9 };
    private static readonly IReadOnlyDictionary<string, Season> EmptyOverrides =
        new Dictionary<string, Season>();

    private readonly int[] _capBySeason;
    private readonly IReadOnlyDictionary<string, Season> _overrides;

    public ContractGenerator() : this(DefaultCapBySeason, EmptyOverrides) { }

    public ContractGenerator(int[] capBySeason) : this(capBySeason, EmptyOverrides) { }

    public ContractGenerator(int[] capBySeason, IReadOnlyDictionary<string, Season> overrides)
    {
        if (capBySeason == null || capBySeason.Length != Calendar.MonthsPerYear)
            throw new ArgumentException(
                $"capBySeason must be length {Calendar.MonthsPerYear}.", nameof(capBySeason));
        _capBySeason = capBySeason;
        _overrides = overrides ?? EmptyOverrides;
    }

    public YearPlan Generate(IReadOnlyList<CcItem> ccItems, int seed)
    {
        if (ccItems is null)
            throw new ArgumentNullException(nameof(ccItems));

        // Empty (season, theme) bins.
        var grouped = new Dictionary<(Season, Theme), List<string>>();
        foreach (Season s in Enum.GetValues(typeof(Season)))
            foreach (Theme t in Enum.GetValues(typeof(Theme)))
                grouped[(s, t)] = new List<string>();

        // Deterministic order: single-season-first (no flexibility), then COMMON-first by rarity
        // (easy items earlier), with id alpha as the final tie. No seed-driven shuffle — every
        // run with the same catalog + overrides gets the same partition (spec 2026-05-26).
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
    /// Pick the season for this item: pinned override wins; otherwise least-loaded under-cap
    /// candidate (earliest on tie); fallback to earliest obtainable if all over cap. Single-
    /// season items collapse to their only candidate naturally.
    /// </summary>
    private Season ChooseSeason(
        CcItem item,
        IReadOnlyDictionary<(Season, Theme), List<string>> grouped)
    {
        // Stable candidate order (earliest → latest). Using `<` in the load comparison keeps
        // the FIRST (earliest) match on ties.
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();

        // Pinned override?
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
            if (load < bestLoad)         // strict < so the EARLIEST tied load wins
            {
                best = s;
                bestLoad = load;
            }
        }

        if (best.HasValue)
            return best.Value;

        // Every candidate is at cap — never drop. Force-place in the earliest obtainable season.
        return candidates[0];
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
