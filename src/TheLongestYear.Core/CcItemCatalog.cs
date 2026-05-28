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

    /// <summary>
    /// Qualified item ids that are NOT realistically obtainable in weeks 1-2 of a run regardless
    /// of season, because they require significant in-run infrastructure investment that takes
    /// most of a season to set up. The <see cref="BonusItemSampler"/> excludes these from the
    /// bonus pool during early weeks.
    ///
    /// 2026-05-28 playtest feedback: "Got Cheese in Spring W1 — no way I can do that run one,
    /// won't be able to get a barn and raise cows far enough, plus get cheese presses."
    ///
    /// Categories (with notes on the infrastructure gate):
    ///   - Animal artisan goods (cow + barn + cheese press / sheep + loom / etc.)
    ///   - Keg/preserve goods (keg ~30 wood + copper bar + 8 days fermenting)
    ///   - Fruit tree fruits (28-day sapling growth + season-locked harvest window)
    ///   - Deep mine drops (floor 40+ for essences/iron, floor 80+ for fire quartz/iridium)
    ///   - Calico Desert items (bus repair quest gates Oasis/Skull Cavern access)
    ///
    /// Rarity weighting alone can't keep these out — Solar Essence is 40g (Common-priced)
    /// despite being a deep-mine drop, so it scores Common×8 weight in the sampler.
    /// </summary>
    public static readonly IReadOnlySet<string> EarlyGameAvoid = new HashSet<string>(StringComparer.Ordinal)
    {
        // ----- Animal artisan (cow + cheese press / sheep + loom / etc.) -----
        "(O)424", // Cheese          — cow + cheese press
        "(O)426", // Goat Cheese     — goat + cheese press
        "(O)428", // Cloth           — sheep wool + loom
        "(O)440", // Wool            — sheep + barn upgrade
        "(O)436", // Goat Milk       — goat + Big Barn
        "(O)438", // Large Goat Milk — goat + max hearts
        "(O)442", // Duck Egg        — Big Coop
        "(O)444", // Duck Feather    — Big Coop + duck max hearts
        "(O)446", // Rabbit's Foot   — Deluxe Coop + rabbit max hearts
        "(O)306", // Mayonnaise      — chicken + mayo machine (achievable but tight)
        "(O)307", // Duck Mayonnaise — Big Coop + duck + mayo machine
        "(O)308", // Void Mayonnaise — void chicken (witch quest)

        // ----- Keg / preserve goods -----
        "(O)348", // Wine            — keg + fruit (7 days fermenting)
        "(O)346", // Beer            — keg + wheat (1.75 days)
        "(O)459", // Mead            — keg + honey
        "(O)303", // Pale Ale        — keg + hops
        "(O)350", // Juice           — keg + vegetable
        "(O)344", // Jelly           — preserves jar
        "(O)342", // Pickles         — preserves jar

        // ----- Fruit tree fruits (28-day sapling + seasonal bloom) -----
        "(O)613", // Apple           — Fall fruit tree
        "(O)634", // Apricot         — Spring fruit tree (28-day sapling, then mature 28-day grow)
        "(O)635", // Orange          — Summer fruit tree
        "(O)636", // Peach           — Summer fruit tree
        "(O)637", // Pomegranate     — Fall fruit tree
        "(O)638", // Cherry          — Spring fruit tree

        // ----- Deep mine / late-game minerals -----
        "(O)768", // Solar Essence   — floor 40+ (rainbow / mummy / metal head drops)
        "(O)769", // Void Essence    — floor 80+ (shadow brutes / Witch's Swamp)
        "(O)337", // Iridium Bar     — floor 120+ iridium ore + furnace
        "(O)386", // Iridium Ore     — Skull Cavern / floor 120+
        "(O)82",  // Fire Quartz     — floor 80+ Mines
        "(O)422", // Purple Mushroom — Mines floor 80+ / Skull Cavern
        "(O)336", // Gold Bar        — floor 80+ gold ore + furnace (tight even by Fall)
        "(O)74",  // Prismatic Shard — extremely rare deep-mine drop

        // ----- Calico Desert / bus-locked -----
        "(O)164", // Sandfish        — Sandy's Oasis pond (bus required)
        "(O)165", // Scorpion Carp   — Calico Desert
        "(O)252", // Rhubarb         — Sandy's Oasis
        "(O)88",  // Coconut         — Calico Desert forage
        "(O)90",  // Cactus Fruit    — Calico Desert forage

        // ----- Animal-locked staples beyond the cheese/wine list (2026-05-28 playtest:
        //       Farming week 1 surfaced "2 Large Eggs + Honey" with no path to obtain them) -----
        "(O)174", // Large Egg       — chicken at 5 hearts (~12+ days)
        "(O)182", // Large Brown Egg — brown chicken at 5 hearts
        "(O)176", // Egg             — coop + chicken (~5 days at minimum, then daily lay)
        "(O)180", // Brown Egg       — coop + brown chicken
        "(O)340", // Honey           — bee house + adjacent flower (Farming 8 to craft, 4 days)
        "(O)432", // Truffle Oil     — pig + oil maker (Deluxe Barn → pig → outdoor truffle)
        "(O)167", // Truffle         — pig only (Deluxe Barn gate)

        // ----- Tap / hardwood-locked forage (2026-05-28 playtest: Foraging surfaced Morel
        //       and Cactus Fruit on week 1 with no way to reach either) -----
        "(O)257", // Morel           — Secret Woods (needs steel axe + hardwood logs cleared)
        "(O)281", // Chanterelle     — Secret Woods (fall) — same axe gate
        "(O)420", // Red Mushroom    — Secret Woods or mines floor 50+ (mines_closed week blocked)
        "(O)724", // Maple Syrup     // requires tapper (copper bar) + 9 days on a maple tree
        "(O)725", // Oak Resin       // requires tapper + 7 days on an oak tree
        "(O)726", // Pine Tar        // requires tapper + 5 days on a pine tree
    };

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
