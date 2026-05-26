using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Hand-authored minimal Community Center ground truth for v1. Each <see cref="CcItem"/> is tagged
/// with the theme/room it belongs to, its rarity (JP value), and the seasons it can be obtained.
/// This is intentionally curated, not exhaustive: it is enough to generate a solvable year-plan and
/// drive the loop. A real Data/Bundles adapter (which supplies item+room but not rarity/season) is
/// deferred to Plan 04 and will feed this same CcItem shape.
/// </summary>
public static class CcItemCatalog
{
    private static readonly IReadOnlyList<CcItem> _items = Build();
    private static readonly IReadOnlyDictionary<string, Rarity> _rarityById =
        _items.ToDictionary(i => i.Id, i => i.Rarity);

    public static IReadOnlyList<CcItem> Items => _items;

    /// <summary>Rarity for a catalog item id; <see cref="Rarity.Common"/> for anything unknown.</summary>
    public static Rarity RarityOf(string itemId)
        => _rarityById.TryGetValue(itemId, out Rarity r) ? r : Rarity.Common;

    private static CcItem Item(string id, Theme theme, Rarity rarity, params Season[] seasons)
        => new CcItem(id, theme, rarity, new HashSet<Season>(seasons));

    private static IReadOnlyList<CcItem> Build() => new List<CcItem>
    {
        // Foraging (Crafts Room) — one seasonal forage each, plus a rare.
        Item("WildHorseradish", Theme.Foraging, Rarity.Common,   Season.Spring),
        Item("Spice Berry",     Theme.Foraging, Rarity.Common,   Season.Summer),
        Item("CommonMushroom",  Theme.Foraging, Rarity.Common,   Season.Fall),
        Item("Crocus",          Theme.Foraging, Rarity.Uncommon, Season.Winter),
        Item("Morel",           Theme.Foraging, Rarity.Rare,     Season.Spring, Season.Fall),

        // Farming (Pantry) — staple crops + a banked rarity (Red Cabbage, Summer).
        Item("Parsnip",         Theme.Farming,  Rarity.Common,   Season.Spring),
        Item("Melon",           Theme.Farming,  Rarity.Uncommon, Season.Summer),
        Item("Pumpkin",         Theme.Farming,  Rarity.Uncommon, Season.Fall),
        Item("RedCabbage",      Theme.Farming,  Rarity.VeryRare, Season.Summer),

        // Fishing (Fish Tank) — seasonal fish + a rare.
        Item("Sardine",         Theme.Fishing,  Rarity.Common,   Season.Spring, Season.Fall, Season.Winter),
        Item("Sunfish",         Theme.Fishing,  Rarity.Common,   Season.Spring, Season.Summer),
        Item("Salmon",          Theme.Fishing,  Rarity.Uncommon, Season.Fall),
        Item("Catfish",         Theme.Fishing,  Rarity.Rare,     Season.Spring, Season.Fall),

        // Mining (Boiler Room) — bars/minerals obtainable year-round, plus a gem.
        Item("CopperBar",       Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("IronBar",         Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Quartz",          Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("FrozenTear",      Theme.Mining,   Rarity.Uncommon, Season.Winter),
        Item("Diamond",         Theme.Mining,   Rarity.Rare,     Season.Spring, Season.Summer, Season.Fall, Season.Winter),

        // Mixed (Bulletin Board) — cross-cutting items, spread across seasons.
        Item("Egg",             Theme.Mixed,    Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Wool",            Theme.Mixed,    Rarity.Uncommon, Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Honey",           Theme.Mixed,    Rarity.Uncommon, Season.Spring, Season.Summer, Season.Fall),
        Item("Truffle",         Theme.Mixed,    Rarity.Rare,     Season.Fall, Season.Winter),
    };
}
