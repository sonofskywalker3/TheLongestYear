using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// The full set of contracts for one run: exactly one per (season, theme) — 20 in total.
/// </summary>
public sealed class YearPlan
{
    private readonly Dictionary<(Season, Theme), Contract> _bySlot;

    public IReadOnlyList<Contract> Contracts { get; }

    public YearPlan(IReadOnlyList<Contract> contracts)
    {
        if (contracts is null)
            throw new ArgumentNullException(nameof(contracts));

        _bySlot = new Dictionary<(Season, Theme), Contract>();
        foreach (Contract c in contracts)
        {
            var key = (c.Season, c.Theme);
            if (_bySlot.ContainsKey(key))
                throw new ArgumentException($"Duplicate contract for {c.Season}/{c.Theme}.", nameof(contracts));
            _bySlot[key] = c;
        }

        int expected = Calendar.MonthsPerYear * 5; // 5 themes per season
        if (_bySlot.Count != expected)
            throw new ArgumentException($"A year plan needs exactly {expected} contracts (one per season/theme); got {_bySlot.Count}.", nameof(contracts));

        Contracts = contracts.ToList();
    }

    public Contract Get(Season season, Theme theme) => _bySlot[(season, theme)];

    public IEnumerable<Contract> ForSeason(Season season)
        => Contracts.Where(c => c.Season == season);
}
