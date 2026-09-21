using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which fruit, mushroom or fish a flavored bundle slot names, and how many of the
/// dried or smoked good it may ask for. Plan 2026-09-21-flavored-bundle-slots.
///
/// A Dried Fruit slot used to accept any dried fruit, because a bundle's ingredients are
/// (id, stack, quality) triples with nowhere to put a flavor, so the menu showed the bare word
/// "Dried" and the match fell through to the base id (see <see cref="FlavorlessBundleSlots"/>).
/// Jeff, 2026-09-21: "any is WAY too easy, what's the point?" The slot names one fruit now.
///
/// Nothing here is persisted. The flavor is a pure function of the board seed and the slot's
/// position, recomputed identically at generation and at every load, exactly as Mr. Raccoon
/// re-derives his own bundles (Raccoon.cs:136). That is what keeps the bundle data string
/// untouched and the byte-for-byte EngineManifestCheck intact.
///
/// Core-only: the caller supplies the pools and a week lookup, so no rule here reads game state.</summary>
public static class FlavoredSlotRules
{
    public const string DriedFruit = "(O)DriedFruit";
    public const string DriedMushrooms = "(O)DriedMushrooms";
    public const string SmokedFish = "(O)SmokedFish";

    /// <summary>Data/Machines: the Dehydrator takes five fruit (or five mushrooms) for one dried
    /// good. The Fish Smoker is one for one.</summary>
    public const int DehydratorInputRatio = 5;
    public const int SmokerInputRatio = 1;

    /// <summary>What five dehydrators make in a week, one a day each: the basis these goods
    /// carried for every flavor before the flavor was pinned
    /// (<see cref="QuantityBasisTables.Stations"/>). It stays as the ceiling, because however
    /// plentiful the input is, the machines only run so often.</summary>
    public const double StationThroughput = 35;

    /// <summary>Data/Objects categories the Dehydrator's and Smoker's own triggers name
    /// (<c>category_fruits</c>, <c>category_fish</c>).</summary>
    public const int FruitCategory = -79;
    public const int FishCategory = -4;

    /// <summary>Spring tree fruit. A sapling takes 28 days and a tree bears only in its own
    /// season, so one planted in week 1 matures in Summer and would not fruit until a Spring the
    /// run never reaches: <see cref="AvailabilityWeeks.FruitTreeFruitWeeks"/> puts both at week 13,
    /// "second year or the cart". Cart stock is a coin flip, which is no way to gate a mandatory
    /// slot, so these are barred outright rather than merely gated late (Jeff raised the tree
    /// timing, 2026-09-21; the bar is the plan's own rule).</summary>
    private static readonly IReadOnlySet<string> CartOnlyFruit =
        new HashSet<string>(StringComparer.Ordinal) { "(O)634", "(O)638" };

    /// <summary>The salt for the flavor's own rng stream. Distinct from
    /// <c>BoardRepairService</c>'s so a repair draw and a flavor draw can never shadow one
    /// another.</summary>
    private const int FlavorSaltPrime = 0x5C2F;

    private static readonly IReadOnlyDictionary<string, int> Ratios =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [DriedFruit] = DehydratorInputRatio,
            [DriedMushrooms] = DehydratorInputRatio,
            [SmokedFish] = SmokerInputRatio,
        };

    /// <summary>True when this id is a good whose slot should name its input.</summary>
    public static bool IsFlavored(string? baseItemId)
        => baseItemId != null && Ratios.ContainsKey(BundleParsing.NormalizeItemId(baseItemId));

    /// <summary>How many inputs one of these goods costs.</summary>
    public static int InputRatioFor(string baseItemId)
        => Ratios.TryGetValue(BundleParsing.NormalizeItemId(baseItemId), out int r) ? r : 1;

    /// <summary>The inputs this slot may name: what the machine accepts, kept to what a rule
    /// places at or before the bundle's own deadline. <paramref name="weekOf"/> returns null for
    /// an id nothing placed, and such an id is left out rather than guessed at.</summary>
    public static IReadOnlyList<string> CandidatesFor(
        string baseItemId, ItemPools pools, Func<string, int?> weekOf, int deadlineWeek)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        if (weekOf == null) throw new ArgumentNullException(nameof(weekOf));

        string id = BundleParsing.NormalizeItemId(baseItemId ?? "");
        IEnumerable<string> raw = id switch
        {
            DriedFruit => FruitIds(pools),
            DriedMushrooms => MushroomIds(pools),
            SmokedFish => FishIds(pools),
            _ => Array.Empty<string>(),
        };

        var result = new List<string>();
        foreach (string candidate in raw.Distinct(StringComparer.Ordinal))
        {
            if (pools.ExcludedIds.Contains(candidate)) continue;
            int? week = weekOf(candidate);
            if (week == null || week > deadlineWeek) continue;
            result.Add(candidate);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Every fruit the Dehydrator would take: the fruit category wherever it appears in
    /// the pools, plus what a Data/FruitTrees tree grows (tree fruit has no crop row, so the
    /// category walk alone would never offer an Apple), minus the cart-only Spring fruit.</summary>
    private static IEnumerable<string> FruitIds(ItemPools pools)
        => pools.Crops.Concat(pools.Forage).Concat(pools.ArtisanGoods)
            .Where(p => p.Category == FruitCategory).Select(p => p.ItemId)
            .Concat(pools.FruitTreeFruitIds)
            .Where(i => !CartOnlyFruit.Contains(i));

    /// <summary>The edible mushrooms, the same list the Wild Medicine recipe draws on.</summary>
    private static IEnumerable<string> MushroomIds(ItemPools pools)
        => BundlePoolRecipes.EdibleMushroomIds;

    /// <summary>Real fish only: a legendary is a once-a-run catch and must never be the thing a
    /// slot demands five of.</summary>
    private static IEnumerable<string> FishIds(ItemPools pools)
        => pools.Fish.Where(p => p.Category == FishCategory && !LegendaryFishRules.IsLegendary(p.ItemId))
            .Select(p => p.ItemId);

    /// <summary>The input this slot names, or null when nothing is reachable and the slot has to
    /// stay flavorless. Its own rng stream, salted by the slot's position, so a board composes the
    /// same way however many flavored slots it has.</summary>
    public static string? Pick(int seed, int bundleIndex, int ingredientIndex, IReadOnlyList<string> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        var rng = new Random(seed ^ (bundleIndex * FlavorSaltPrime) ^ (ingredientIndex + 1));
        return candidates[rng.Next(candidates.Count)];
    }

    /// <summary>The weekly basis a flavored ask rolls off: what the named input itself yields,
    /// divided by what one of the good costs, and never more than the machines can make. The ask
    /// follows the fruit, not the machine, because the fruit is what the player has to find.</summary>
    public static double BasisFor(string baseItemId, double inputBasis)
    {
        double scaled = inputBasis / InputRatioFor(baseItemId);
        return Math.Max(1, Math.Min(scaled, StationThroughput));
    }
}
