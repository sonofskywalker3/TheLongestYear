using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// One Community Center required item — the "ground truth" unit the contracts schedule.
/// Tagged with the theme it belongs to, its rarity (JP value), and the seasons it can be obtained.
/// </summary>
public sealed class CcItem
{
    public string Id { get; }
    public Theme Theme { get; }
    public Rarity Rarity { get; }
    public IReadOnlySet<Season> ObtainableSeasons { get; }

    public CcItem(string id, Theme theme, Rarity rarity, IReadOnlySet<Season> obtainableSeasons)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must be non-empty.", nameof(id));
        if (obtainableSeasons is null || obtainableSeasons.Count == 0)
            throw new ArgumentException("An item must be obtainable in at least one season.", nameof(obtainableSeasons));

        Id = id;
        Theme = theme;
        Rarity = rarity;
        ObtainableSeasons = obtainableSeasons;
    }

    public bool IsObtainableIn(Season season) => ObtainableSeasons.Contains(season);
}
