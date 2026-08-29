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
/// so the filler's rng draws in a fixed order and a seed always composes the same board.</summary>
public sealed record PoolRecipe(string Name, IReadOnlyList<PoolPart> Parts);

/// <summary>The recipe for every non-money bundle: a named recipe where the spec rules one
/// (Dye asks for one item per colour, Field Research for one of each of four things), else the
/// pool of the majority <see cref="ItemKind"/> of the bundle's own vanilla items, else Other.
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

    private const string OtherLabel = "Other";
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

    /// <summary>Named recipes, keyed by the bundle's stable name. The value takes the bundle's
    /// vanilla ids because Chef's sizes its cooked half against the bundle's slot count.</summary>
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
                new PoolPart((p, _) => p.Cooking, Math.Max(1, ids.Count / 2), "Cooking"),
                new PoolPart((p, _) => Union(p.Crops, p.Forage, Bucket(p, ItemKind.Egg), Bucket(p, ItemKind.Milk),
                    Bucket(p, ItemKind.AnimalProduct)), RestOfTheSlots, "Ingredient"),
            },
            ["Winter Star"] = _ => One((p, _) => p.WinterOnly, "Winter"),
            ["The Missing"] = _ => One(MissingSource, "Extreme"),
            ["Children's"] = _ => One((p, _) => Fixed(p, SweetDishes.Concat(Berries).Concat(Dolls)), "Sweets and toys"),
            ["Enchanter's"] = _ => One(EnchanterSource, "Totem or essence"),
            ["Fish Farmer's"] = _ => One((p, _) => Fixed(p, PondGoods), "Pond goods"),
            ["Animal"] = _ => One((p, _) => Union(
                Bucket(p, ItemKind.Egg), Bucket(p, ItemKind.Milk), Bucket(p, ItemKind.AnimalProduct)), "Animal product"),
            ["Artisan"] = _ => One((p, _) => Union(p.ArtisanGoods, Bucket(p, ItemKind.ArtisanGood)), "Artisan good"),
            ["Adventurer's"] = _ => One((p, _) => Union(p.MonsterDrops, Bucket(p, ItemKind.MonsterLoot)), "Monster loot"),
            ["Forager's"] = _ => One((p, _) => p.Forage, "Forage"),
            ["Gil's Trophies"] = _ => One(Kind(ItemKind.Trophy), "Trophy"),
            ["Recycler's"] = _ => One((p, _) => Fixed(p, Trash), "Trash"),
            ["Book"] = _ => One((p, _) => Union(p.Books, Bucket(p, ItemKind.Book)), "Book"),
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

        IReadOnlyList<PoolPart> parts = Named.TryGetValue(name, out Func<IReadOnlyList<string>, IReadOnlyList<PoolPart>>? build)
            ? build(ids)
            : MajorityKindParts(ids, pools);

        IReadOnlyList<PoolItem> vanilla = VanillaItems(pools, ids);
        var widened = new List<PoolPart>(parts.Count);
        foreach (PoolPart part in parts)
        {
            Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> inner = part.Source;
            widened.Add(part with { Source = (p, m) => Union(inner(p, m), vanilla) });
        }
        return new PoolRecipe(name, widened);
    }

    /// <summary>The pool of the commonest ItemKind among the bundle's own items, ties broken by
    /// enum order so the choice is deterministic. Other never wins on its own: a bundle whose
    /// items are all uncategorized (or unknown to the pools) gets the Other pool by fallback,
    /// widened with its vanilla items like every other part.</summary>
    private static IReadOnlyList<PoolPart> MajorityKindParts(IReadOnlyList<string> ids, ItemPools pools)
    {
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
            return One(Kind(ItemKind.Other), OtherLabel);

        ItemKind winner = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => (int)kv.Key).First().Key;
        return One(Kind(winner), winner.ToString());
    }

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

    private static IReadOnlyList<PoolItem> FodderSource(ItemPools pools, ItemAvailabilityModel? model)
        => Union(
            Fixed(pools, FodderGrains.Concat(new[] { Hay })),
            pools.Crops.Where(p => p.Category == FruitCategory).ToList());

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
    /// asked for in vanilla, so it is obtainable even when no pool carries it.</summary>
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
