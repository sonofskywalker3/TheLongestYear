using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One slice of a bundle's re-roll: where its candidates come from, and how many
/// slots it fills. <paramref name="Count"/> 0 means "the rest of the slots".</summary>
public sealed record PoolPart(
    Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> Source,
    int Count,
    string Label);

/// <summary>An ordered list of parts describing how one bundle re-rolls. Part order is fixed,
/// so the filler's rng draws in a fixed order and a seed always composes the same board.
/// <paramref name="IsVanillaOnly"/> = this bundle has no pool to roll: its single part offers the
/// bundle's own items and nothing else, so it re-rolls to (a subset of) what vanilla asked for.
/// Diagnostics tag those, because a board full of them is a board that is not rolling.</summary>
public sealed record PoolRecipe(string Name, IReadOnlyList<PoolPart> Parts, bool IsVanillaOnly = false);

/// <summary>The recipe for every non-money bundle: a named recipe where the spec rules one
/// (Dye asks for one item per colour, Field Research for one of each of four things), else the
/// pool of the majority <see cref="ItemKind"/> of the bundle's own vanilla items, else the
/// bundle's own items and nothing more.
///
/// The Other kind is NEVER a roll source (Jeff, 2026-08-29: 47 of 195 recipe rolls landed in it,
/// and it holds rings, fences, paths, Gravel Path, Tent Kit and Artifact Spot). A bundle whose
/// items name no kind rolls from its own vanilla list instead: a smaller ask, never a junk one.
///
/// Every part also keeps the bundle's own vanilla items as candidates, so a part whose pool is
/// empty on this save (a mod removed the items, a hand-built pool has no ByKind data) still has
/// something to draw and the bundle degrades to its vanilla identity rather than to nothing.
///
/// Core-only by construction: a part reads the pools and the availability model, never live game
/// state. Spec 2026-08-28-obtainable-board-3-pools.</summary>
public static class BundlePoolRecipes
{
    /// <summary>A part <see cref="PoolPart.Count"/> of 0: this part fills whatever slots the
    /// earlier parts left.</summary>
    public const int RestOfTheSlots = 0;

    /// <summary>The Missing asks for the extreme band only: effort at or above this.</summary>
    public const int MissingMinimumEffort = 9;

    /// <summary>Rare Crops asks for crops that take real work: effort at or above this.</summary>
    public const int RareCropMinimumEffort = 3;

    /// <summary>The weight a synthesized candidate carries: the lowest there is, so a bundle's own
    /// vanilla id never outdraws a real pool item.
    ///
    /// A synthesized PoolItem also carries price 0 and no seasons, because the pools are the only
    /// place that data lives and by definition no pool knows this id. <see cref="ItemHardness"/>
    /// therefore ranks it Common (price 0 is below every rarity threshold) and gives it no
    /// out-of-season bonus, so the pity trim drops it LAST. That is the intended direction: the
    /// item is what vanilla itself asked for, so it is the safest thing in the list.</summary>
    private const int SynthesizedWeight = 1;

    private const string Hay = "(O)178";
    private const int FruitCategory = -79;
    private const int GreensCategory = -81;

    /// <summary>Fodder's grains: the spec names Wheat and Corn by name, and every fruit by
    /// category. PoolItem carries no name, so the two grains are listed by id and the fruit half
    /// is the fruit category, which is what "any fruit" means in the game's own data.</summary>
    private static readonly string[] FodderGrains = { "(O)262", "(O)270" };

    /// <summary>Wild Medicine's mushrooms. The spec's rule is the edible_mushroom context tag or
    /// the greens category; PoolItem carries no context tags, so the vanilla mushroom ids stand in
    /// for the tag half and the greens category does the rest (a modded mushroom in that category
    /// still counts).</summary>
    private static readonly string[] EdibleMushrooms = { "(O)257", "(O)281", "(O)404", "(O)420", "(O)422" };

    /// <summary>Field Research's beach half: the shell and sea forage that sits beside artifacts
    /// in vanilla's own Field Research bundle.</summary>
    private static readonly string[] ShellForage =
    {
        "(O)372", "(O)392", "(O)393", "(O)394", "(O)397", "(O)718", "(O)719", "(O)723",
    };

    /// <summary>Children's sweets. The spec's rule is the food_sweet context tag; PoolItem carries
    /// no context tags, so this is the vanilla list of sweet cooked dishes.</summary>
    private static readonly string[] SweetDishes =
    {
        "(O)220", "(O)221", "(O)222", "(O)223", "(O)233", "(O)234",
        "(O)604", "(O)608", "(O)611", "(O)612", "(O)731",
    };

    /// <summary>Chef's pantry staples: Sugar, Wheat Flour, Oil, Vinegar, Rice. A kitchen asks for
    /// these as readily as for a crop, and none of them lands in a crop, forage or animal-product
    /// pool, so without this list the ingredient half could never offer one (final review,
    /// 2026-08-29).</summary>
    private static readonly string[] ChefStaples = { "(O)245", "(O)246", "(O)247", "(O)419", "(O)423" };

    private static readonly string[] Berries = { "(O)296", "(O)410" };
    private static readonly string[] Dolls = { "(O)103", "(O)126", "(O)127" };

    /// <summary>Solar and Void Essence. Both are monster loot by category, so the ByKind walk
    /// files them under MonsterLoot, not Essence; PoolItem carries no name, so the " Essence"
    /// name rule is spelled out as ids here and joined with whatever DID land in ByKind[Essence]
    /// (a modded essence with no loot category).</summary>
    private static readonly string[] Essences = { "(O)768", "(O)769" };

    /// <summary>Fish Farmer's pond goods: Roe, Aged Roe, Squid Ink, Caviar.</summary>
    private static readonly string[] PondGoods = { "(O)812", "(O)447", "(O)814", "(O)445" };

    /// <summary>Recycler's: the five trash items plus the two things a Recycling Machine makes
    /// from them.</summary>
    private static readonly string[] Trash =
    {
        "(O)168", "(O)169", "(O)170", "(O)171", "(O)172", "(O)338", "(O)428",
    };

    private static readonly string[] DyeColourTags =
    {
        "color_red", "color_purple", "color_yellow", "color_white", "color_blue", "color_green",
    };

    /// <summary>Bundles with no pool of their own: they re-roll from their own vanilla items, so
    /// the trim can shorten them but nothing new is ever asked for. Helper's and Home Cook's are
    /// hand-picked vanilla lists with no kind in common (Jeff, 2026-08-29).</summary>
    private static readonly IReadOnlySet<string> VanillaOnlyBundles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Helper's", "Home Cook's" };

    /// <summary>The label of the vanilla-only part, shown in the bundle guide and the audit.</summary>
    public const string VanillaOnlyLabel = "the bundle's own items";

    /// <summary>Named recipes, keyed by the bundle's stable name. The value takes the bundle's
    /// vanilla ids because Chef's sizes its cooked half against the bundle's slot count.
    ///
    /// Book, Gil's Trophies and Recycler's are authored bundles: BundleEngine short-circuits an
    /// authored pick to PoolDomain.None before it ever reaches the classifier, so those three rows
    /// are unreachable today. They stay for the spec's table and in case an authored def is ever
    /// dropped back into the rolled pool.</summary>
    private static readonly IReadOnlyDictionary<string, Func<IReadOnlyList<string>, IReadOnlyList<PoolPart>>> Named =
        new Dictionary<string, Func<IReadOnlyList<string>, IReadOnlyList<PoolPart>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Treasure Hunter's"] = _ => One(Kind(ItemKind.Gem), "Gem"),
            ["Construction"] = _ => One(Kind(ItemKind.Resource), "Resource"),
            ["Fodder"] = _ => One(FodderSource, "Fodder"),
            ["Dye"] = _ => DyeParts(),
            ["Field Research"] = _ => new[]
            {
                new PoolPart((p, _) => p.Forage, 1, "Forage"),
                new PoolPart((p, _) => Union(Bucket(p, ItemKind.Artifact), p.Artifacts, Fixed(p, ShellForage)), 1, "Artifact or shell"),
                new PoolPart((p, _) => p.Fish, 1, "Fish"),
                new PoolPart((p, _) => Union(Bucket(p, ItemKind.Mineral), p.GeodeMinerals), 1, "Mineral or geode"),
            },
            ["Wild Medicine"] = _ => One(MedicineSource, "Mushroom"),
            ["Chef's"] = ids => new[]
            {
                new PoolPart((p, m) => Placeable(p.Cooking, m), Math.Max(1, ids.Count / 2), "Cooking"),
                new PoolPart((p, _) => Union(p.Crops, p.Forage, Bucket(p, ItemKind.Egg), Bucket(p, ItemKind.Milk),
                    Bucket(p, ItemKind.AnimalProduct), Fixed(p, ChefStaples)), RestOfTheSlots, "Ingredient"),
            },
            ["Winter Star"] = _ => One((p, _) => p.WinterOnly, "Winter"),
            ["The Missing"] = _ => One(MissingSource, "Extreme"),
            ["Children's"] = _ => One((p, m) => Placeable(Fixed(p, SweetDishes.Concat(Berries).Concat(Dolls)), m), "Sweets and toys"),
            ["Enchanter's"] = _ => One(EnchanterSource, "Totem or essence"),
            ["Fish Farmer's"] = _ => One((p, _) => Fixed(p, PondGoods), "Pond goods"),
            ["Animal"] = _ => One((p, _) => Union(
                Bucket(p, ItemKind.Egg), Bucket(p, ItemKind.Milk), Bucket(p, ItemKind.AnimalProduct)), "Animal product"),
            ["Artisan"] = _ => One((p, _) => Union(p.ArtisanGoods, Bucket(p, ItemKind.ArtisanGood)), "Artisan good"),
            ["Adventurer's"] = _ => One((p, _) => Union(p.MonsterDrops, Bucket(p, ItemKind.MonsterLoot)), "Monster loot"),
            ["Forager's"] = _ => One((p, _) => p.Forage, "Forage"),
            ["Gil's Trophies"] = _ => One(Kind(ItemKind.Trophy), "Trophy"),
            ["Recycler's"] = _ => One((p, _) => Fixed(p, Trash), "Trash"),
            // Books only from the Books pool: it is already filtered to the books a player can
            // reach in year 1 (AvailabilityWeeks.BookWeeks). ByKind[Book] is not.
            ["Book"] = _ => One((p, _) => p.Books, "Book"),

            // Rows added after the 20-board run (Jeff, 2026-08-29): these four were falling
            // through to a kind pool that did not match what the bundle is about.
            ["Crab Pot"] = _ => One(CrabPotSource, "Crab pot"),
            ["Exotic Foraging"] = _ => One((p, _) => Union(p.Forage, p.TapperGoods), "Forage or tapper"),
            ["Rare Crops"] = _ => One(RareCropSource, "Rare crop"),
            ["Sticky"] = _ => One((p, _) => Union(Bucket(p, ItemKind.Resource), p.TapperGoods), "Sap or resource"),
        };

    /// <summary>The recipe this bundle re-rolls from. Named recipe first, else the majority
    /// ItemKind of its vanilla ids (read off the pools' own ByKind membership), else Other. Every
    /// part comes back widened with the bundle's own vanilla items.</summary>
    public static PoolRecipe For(
        string bundleName, IReadOnlyList<string> vanillaIds, ItemPools pools, ItemAvailabilityModel? model)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        IReadOnlyList<string> ids = vanillaIds ?? Array.Empty<string>();
        string name = (bundleName ?? "").Trim();

        bool vanillaOnly = VanillaOnlyBundles.Contains(name);
        IReadOnlyList<PoolPart> parts;
        if (vanillaOnly)
            parts = VanillaOnlyParts();
        else if (Named.TryGetValue(name, out Func<IReadOnlyList<string>, IReadOnlyList<PoolPart>>? build))
            parts = build(ids);
        else
        {
            parts = MajorityKindParts(ids, pools, out bool noKind);
            vanillaOnly = noKind;
        }

        IReadOnlyList<PoolItem> vanilla = VanillaItems(pools, ids);
        var widened = new List<PoolPart>(parts.Count);
        foreach (PoolPart part in parts)
        {
            Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> inner = part.Source;
            widened.Add(part with { Source = (p, m) => Union(inner(p, m), vanilla) });
        }
        return new PoolRecipe(name, widened, vanillaOnly);
    }

    /// <summary>The pool of the commonest ItemKind among the bundle's own items, ties broken by
    /// enum order so the choice is deterministic. The Other kind is not a candidate and is not a
    /// fallback: a bundle whose items name no kind (or that no pool knows) comes back
    /// <paramref name="noKind"/> and rolls from its own vanilla items only.</summary>
    private static IReadOnlyList<PoolPart> MajorityKindParts(
        IReadOnlyList<string> ids, ItemPools pools, out bool noKind)
    {
        noKind = false;
        var counts = new Dictionary<ItemKind, int>();
        var byId = new Dictionary<string, ItemKind>(StringComparer.Ordinal);
        foreach (KeyValuePair<ItemKind, IReadOnlyList<PoolItem>> bucket in pools.ByKind)
        {
            if (bucket.Key == ItemKind.Other) continue;
            foreach (PoolItem item in bucket.Value)
                byId[item.ItemId] = bucket.Key;
        }
        foreach (string raw in ids)
        {
            if (string.IsNullOrEmpty(raw) || BundleParsing.IsCategoryRef(raw)) continue;
            if (!byId.TryGetValue(BundleParsing.NormalizeItemId(raw), out ItemKind kind)) continue;
            counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
        }
        if (counts.Count == 0)
        {
            noKind = true;
            return VanillaOnlyParts();
        }

        ItemKind winner = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => (int)kv.Key).First().Key;
        return One(Kind(winner), winner.ToString());
    }

    /// <summary>The one part of a vanilla-only recipe: it offers nothing of its own, and
    /// <see cref="For"/> widens it with the bundle's own items, which is the whole point.</summary>
    /// <summary>Drops ids the availability model cannot place. The cooking pool comes from a walk
    /// of CookingRecipes, which includes recipes year 1 cannot reach: Crispy Bass needs Kent at 3
    /// hearts, and Kent is not in the valley in year 1. An unplaced id lands on the board as
    /// UNKNOWN and the gate then treats it as Winter, so a Cooking-sourced part filters them out
    /// here. With no model there is nothing to check and the list passes through unchanged.</summary>
    private static IReadOnlyList<PoolItem> Placeable(IReadOnlyList<PoolItem> items, ItemAvailabilityModel? model)
        => model == null ? items : items.Where(p => model.IsPlaced(p.ItemId)).ToList();

    private static IReadOnlyList<PoolPart> VanillaOnlyParts()
        => One((_, _) => Array.Empty<PoolItem>(), VanillaOnlyLabel);

    private static IReadOnlyList<PoolPart> One(
        Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> source, string label)
        => new[] { new PoolPart(source, RestOfTheSlots, label) };

    private static IReadOnlyList<PoolPart> DyeParts()
        => DyeColourTags.Select(tag => new PoolPart(
            (p, _) => p.ColourTags.TryGetValue(tag, out IReadOnlyList<PoolItem>? list)
                ? list
                : Array.Empty<PoolItem>(),
            1, tag)).ToList();

    private static Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> Kind(ItemKind kind)
        => (p, _) => Bucket(p, kind);

    /// <summary>Fodder: the grains and Hay, plus every fruit. The fruit half is the fruit category
    /// as it appears in the crop pool, joined with the fruit a Data/FruitTrees tree grows: tree
    /// fruit has no crop row, so the category walk alone would never offer an Apple (final review,
    /// 2026-08-29). Both halves resolve THROUGH the pools, so nothing unvetted can enter.</summary>
    private static IReadOnlyList<PoolItem> FodderSource(ItemPools pools, ItemAvailabilityModel? model)
        => Union(
            Fixed(pools, FodderGrains.Concat(new[] { Hay })),
            pools.Crops.Where(p => p.Category == FruitCategory).ToList(),
            Fixed(pools, pools.FruitTreeFruitIds));

    /// <summary>Crab Pot: the crab-pot pool plus every trap fish the data names, taken from the
    /// Fish or CrabPot pool. A trap id NEITHER pool knows is left out rather than synthesized
    /// (mirroring <see cref="RareCropSource"/>): an id no pool carries is one that failed vetting
    /// or was excluded on purpose, and Data/Fish naming it "trap" is no reason to put it back
    /// (final review, 2026-08-29). Only the bundle's own vanilla ids are ever synthesized.</summary>
    private static IReadOnlyList<PoolItem> CrabPotSource(ItemPools pools, ItemAvailabilityModel? model)
    {
        var byId = new Dictionary<string, PoolItem>(StringComparer.Ordinal);
        foreach (PoolItem item in pools.Fish)
            byId[item.ItemId] = item;
        foreach (PoolItem item in pools.CrabPot)
            if (!byId.ContainsKey(item.ItemId))
                byId[item.ItemId] = item;
        var traps = new List<PoolItem>();
        foreach (string id in pools.TrapFishIds)
            if (byId.TryGetValue(id, out PoolItem? known))
                traps.Add(known);
        return Union(pools.CrabPot, traps);
    }

    /// <summary>Rare Crops: crops that take real work (effort 3 or more) plus every fruit a tree
    /// grows. With no availability model there is no effort to read, so the crop half is empty and
    /// the bundle's own items carry it.</summary>
    private static IReadOnlyList<PoolItem> RareCropSource(ItemPools pools, ItemAvailabilityModel? model)
    {
        IReadOnlyList<PoolItem> rare = model == null
            ? Array.Empty<PoolItem>()
            : pools.Crops.Where(p => model.For(p.ItemId).Effort >= RareCropMinimumEffort).ToList();
        var byId = new Dictionary<string, PoolItem>(StringComparer.Ordinal);
        foreach (PoolItem item in AllVetted(pools))
            if (!byId.ContainsKey(item.ItemId))
                byId[item.ItemId] = item;
        var fruit = new List<PoolItem>();
        foreach (string id in pools.FruitTreeFruitIds)
            if (byId.TryGetValue(id, out PoolItem? known))
                fruit.Add(known);
        return Union(rare, fruit);
    }

    private static IReadOnlyList<PoolItem> MedicineSource(ItemPools pools, ItemAvailabilityModel? model)
        => Union(
            pools.Forage.Where(p => p.Category == GreensCategory).ToList(),
            Fixed(pools, EdibleMushrooms));

    private static IReadOnlyList<PoolItem> EnchanterSource(ItemPools pools, ItemAvailabilityModel? model)
        => Union(Bucket(pools, ItemKind.Totem), Bucket(pools, ItemKind.Essence), Fixed(pools, Essences));

    /// <summary>The Missing: every vetted item in the extreme effort band. With no availability
    /// model there is no band to read, so the part is empty and the filler falls back to the
    /// bundle's own vanilla items.</summary>
    private static IReadOnlyList<PoolItem> MissingSource(ItemPools pools, ItemAvailabilityModel? model)
    {
        if (model == null)
            return Array.Empty<PoolItem>();
        return AllVetted(pools).Where(p => model.For(p.ItemId).Effort >= MissingMinimumEffort).ToList();
    }

    private static IReadOnlyList<PoolItem> Bucket(ItemPools pools, ItemKind kind)
        => pools.ByKind.TryGetValue(kind, out IReadOnlyList<PoolItem>? list) ? list : Array.Empty<PoolItem>();

    /// <summary>The listed ids, as they exist in this save's pools. An id no pool knows is left
    /// out: it failed vetting, or a mod removed it.</summary>
    private static IReadOnlyList<PoolItem> Fixed(ItemPools pools, IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids.Select(BundleParsing.NormalizeItemId), StringComparer.Ordinal);
        return Distinct(AllVetted(pools).Where(p => wanted.Contains(p.ItemId)));
    }

    /// <summary>The bundle's own items, so every part can fall back to the vanilla identity.
    /// An id the pools do not know is synthesized at the lowest weight: it is what the bundle
    /// asked for in vanilla, so it is obtainable even when no pool carries it.
    ///
    /// An id in <see cref="ItemPools.ExcludedIds"/> is NOT synthesized, and not offered at all: the
    /// pools left it out on purpose (Easy's year-2 crops, the built-in exclude list, the config
    /// exclude list), and widening every part with the bundle's own vanilla ids would otherwise
    /// re-admit exactly the ids the exclusions exist to keep off the board (final review,
    /// 2026-08-29). Only ids that are merely unknown to the pools are synthesized.</summary>
    private static IReadOnlyList<PoolItem> VanillaItems(ItemPools pools, IReadOnlyList<string> ids)
    {
        var known = new Dictionary<string, PoolItem>(StringComparer.Ordinal);
        foreach (PoolItem item in AllVetted(pools))
            if (!known.ContainsKey(item.ItemId))
                known[item.ItemId] = item;

        var result = new List<PoolItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in ids)
        {
            if (string.IsNullOrEmpty(raw) || BundleParsing.IsCategoryRef(raw)) continue;
            string id = BundleParsing.NormalizeItemId(raw);
            if (!seen.Add(id)) continue;
            if (pools.ExcludedIds.Contains(id)) continue;
            result.Add(known.TryGetValue(id, out PoolItem? item)
                ? item
                : new PoolItem(id, 0, SynthesizedWeight, Array.Empty<Season>(), Array.Empty<string>()));
        }
        return result;
    }

    /// <summary>Every item any pool carries. ByKind covers all of vetted Data/Objects; the named
    /// pools are joined in for the hand-built pools of the tests and for Trophy, which is not a
    /// Data/Objects walk.</summary>
    private static IEnumerable<PoolItem> AllVetted(ItemPools pools)
    {
        foreach (IReadOnlyList<PoolItem> bucket in pools.ByKind.Values)
            foreach (PoolItem item in bucket)
                yield return item;
        foreach (PoolItem item in pools.Crops) yield return item;
        foreach (PoolItem item in pools.Fish) yield return item;
        foreach (PoolItem item in pools.CrabPot) yield return item;
        foreach (PoolItem item in pools.Forage) yield return item;
        foreach (PoolItem item in pools.MonsterDrops) yield return item;
        foreach (PoolItem item in pools.Metals) yield return item;
        foreach (PoolItem item in pools.ArtisanGoods) yield return item;
        foreach (PoolItem item in pools.Artifacts) yield return item;
        foreach (PoolItem item in pools.Books) yield return item;
        foreach (PoolItem item in pools.Saplings) yield return item;
        foreach (PoolItem item in pools.GeodeMinerals) yield return item;
        foreach (PoolItem item in pools.Cooking) yield return item;
        foreach (PoolItem item in pools.TapperGoods) yield return item;
    }

    /// <summary>Concatenates candidate lists, keeping the first sighting of each id and ordinal
    /// order, so a part's candidate list is the same on every run of the same seed.</summary>
    public static IReadOnlyList<PoolItem> Union(params IReadOnlyList<PoolItem>[] lists)
        => Distinct(lists.SelectMany(list => list ?? (IReadOnlyList<PoolItem>)Array.Empty<PoolItem>()));

    private static IReadOnlyList<PoolItem> Distinct(IEnumerable<PoolItem> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PoolItem>();
        foreach (PoolItem item in items)
            if (seen.Add(item.ItemId))
                result.Add(item);
        result.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
        return result;
    }
}
