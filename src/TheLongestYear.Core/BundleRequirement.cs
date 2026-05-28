using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// One vanilla CC bundle with its kind-specific gate metadata. Used by <see cref="BundleGate"/>
/// to evaluate "is this bundle's contribution to season N satisfied?" and by the win check.
/// Constructors are kind-specific so invalid combinations are unrepresentable.
/// </summary>
public sealed class BundleRequirement
{
    public string Name { get; }
    public Theme Theme { get; }
    public BundleKind Kind { get; }

    /// <summary>The Y ingredient ids (qualified id form, e.g. "(O)24"). Player donates from these.</summary>
    public IReadOnlyList<string> Ingredients { get; }

    /// <summary>The X slots — number of distinct ingredients required for full completion.</summary>
    public int NumberOfSlots { get; }

    // ---------- KIND 1 (Seasonal) ----------
    public Season? SeasonalSeason { get; }

    // ---------- KIND 2 (PerItem) ----------
    public IReadOnlyDictionary<string, Season>? ItemSeasonPins { get; }

    // ---------- KIND 3 (Percentage) ----------
    /// <summary>Cumulative donations required by [Spring, Summer, Fall, Winter] day 28.
    /// Length 4. Each entry must be ≤ <see cref="NumberOfSlots"/>.</summary>
    public IReadOnlyList<int>? CumulativeRequiredBySeason { get; }

    // ---------- Per-ingredient display data (optional) ----------
    /// <summary>Stack required for each ingredient id (e.g. (O)388 Wood → 99). Empty if the
    /// caller didn't supply per-ingredient details — the value 1 is the safe default the UI
    /// uses on lookup miss. Per-bundle (different bundles may require different stacks for
    /// the same id).</summary>
    public IReadOnlyDictionary<string, int> IngredientStacks { get; }

    /// <summary>Minimum quality required for each ingredient id (0=basic, 1=silver, 2=gold,
    /// 4=iridium per Stardew's quality scale). Empty if not supplied — default 0. Same
    /// per-bundle scope as <see cref="IngredientStacks"/>; Quality Crops needs gold-star,
    /// Pantry's Fall Crops needs basic, both are "(O)24 Parsnip" but with different qualities.</summary>
    public IReadOnlyDictionary<string, int> IngredientQualities { get; }

    private BundleRequirement(
        string name, Theme theme, BundleKind kind,
        IReadOnlyList<string> ingredients, int numberOfSlots,
        Season? seasonalSeason,
        IReadOnlyDictionary<string, Season>? itemSeasonPins,
        IReadOnlyList<int>? cumulativeRequiredBySeason,
        IReadOnlyDictionary<string, int>? ingredientStacks,
        IReadOnlyDictionary<string, int>? ingredientQualities)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Theme = theme;
        Kind = kind;
        Ingredients = ingredients;
        NumberOfSlots = numberOfSlots;
        SeasonalSeason = seasonalSeason;
        ItemSeasonPins = itemSeasonPins;
        CumulativeRequiredBySeason = cumulativeRequiredBySeason;
        IngredientStacks = ingredientStacks ?? new Dictionary<string, int>();
        IngredientQualities = ingredientQualities ?? new Dictionary<string, int>();
    }

    public static BundleRequirement CreateSeasonal(
        string name, Theme theme, IReadOnlyList<string> ingredients, Season season,
        IReadOnlyDictionary<string, int>? ingredientStacks = null,
        IReadOnlyDictionary<string, int>? ingredientQualities = null)
    {
        if (ingredients == null || ingredients.Count == 0)
            throw new ArgumentException("Seasonal bundle needs at least one ingredient.", nameof(ingredients));
        return new BundleRequirement(
            name, theme, BundleKind.Seasonal,
            ingredients, ingredients.Count,
            seasonalSeason: season,
            itemSeasonPins: null,
            cumulativeRequiredBySeason: null,
            ingredientStacks: ingredientStacks,
            ingredientQualities: ingredientQualities);
    }

    /// <summary>Convenience: pins become the ingredient list. Use when every ingredient is pinned.</summary>
    public static BundleRequirement CreatePerItem(
        string name, Theme theme, IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int>? ingredientStacks = null,
        IReadOnlyDictionary<string, int>? ingredientQualities = null)
    {
        if (itemSeasonPins == null || itemSeasonPins.Count == 0)
            throw new ArgumentException("PerItem bundle needs at least one pinned ingredient.", nameof(itemSeasonPins));
        return CreatePerItem(name, theme, itemSeasonPins.Keys.ToList(), itemSeasonPins,
            ingredientStacks, ingredientQualities);
    }

    /// <summary>Full form: ingredients listed explicitly; pins are an optional subset. Unpinned
    /// ingredients don't gate any season but still count toward <see cref="IsFullyComplete"/>.</summary>
    public static BundleRequirement CreatePerItem(
        string name, Theme theme,
        IReadOnlyList<string> ingredients,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int>? ingredientStacks = null,
        IReadOnlyDictionary<string, int>? ingredientQualities = null)
    {
        if (ingredients == null || ingredients.Count == 0)
            throw new ArgumentException("PerItem bundle needs at least one ingredient.", nameof(ingredients));
        if (itemSeasonPins == null)
            throw new ArgumentNullException(nameof(itemSeasonPins));
        return new BundleRequirement(
            name, theme, BundleKind.PerItem,
            ingredients, ingredients.Count,
            seasonalSeason: null,
            itemSeasonPins: itemSeasonPins,
            cumulativeRequiredBySeason: null,
            ingredientStacks: ingredientStacks,
            ingredientQualities: ingredientQualities);
    }

    public static BundleRequirement CreatePercentage(
        string name, Theme theme,
        IReadOnlyList<string> ingredients, int numberOfSlots,
        IReadOnlyList<int> cumulativeRequiredBySeason,
        IReadOnlyDictionary<string, int>? ingredientStacks = null,
        IReadOnlyDictionary<string, int>? ingredientQualities = null)
    {
        if (ingredients == null || ingredients.Count <= numberOfSlots)
            throw new ArgumentException(
                $"Percentage bundle needs Y > X; got Y={ingredients?.Count}, X={numberOfSlots}.",
                nameof(ingredients));
        if (cumulativeRequiredBySeason == null || cumulativeRequiredBySeason.Count != Calendar.MonthsPerYear)
            throw new ArgumentException(
                $"cumulativeRequiredBySeason must be length {Calendar.MonthsPerYear}.",
                nameof(cumulativeRequiredBySeason));
        foreach (int n in cumulativeRequiredBySeason)
            if (n < 0 || n > numberOfSlots)
                throw new ArgumentOutOfRangeException(nameof(cumulativeRequiredBySeason),
                    $"Each cumulative requirement must be in [0..{numberOfSlots}]; got {n}.");
        return new BundleRequirement(
            name, theme, BundleKind.Percentage,
            ingredients, numberOfSlots,
            seasonalSeason: null,
            itemSeasonPins: null,
            cumulativeRequiredBySeason: cumulativeRequiredBySeason,
            ingredientStacks: ingredientStacks,
            ingredientQualities: ingredientQualities);
    }

    /// <summary>True if this bundle's contribution to <paramref name="currentSeason"/>'s
    /// day-28 gate is satisfied by the run's donation ledger.</summary>
    public bool IsSatisfiedAtSeasonEnd(Season currentSeason, ISet<string> donated)
    {
        switch (Kind)
        {
            case BundleKind.Seasonal:
                // Not yet due if its named season is later than current.
                if ((int)SeasonalSeason!.Value > (int)currentSeason) return true;
                return Ingredients.All(donated.Contains);

            case BundleKind.PerItem:
                foreach (KeyValuePair<string, Season> kv in ItemSeasonPins!)
                {
                    if ((int)kv.Value <= (int)currentSeason && !donated.Contains(kv.Key))
                        return false;
                }
                return true;

            case BundleKind.Percentage:
                int required = CumulativeRequiredBySeason![(int)currentSeason];
                int count = 0;
                foreach (string id in Ingredients)
                    if (donated.Contains(id) && ++count >= required)
                        return true;
                return required == 0;   // an explicit-zero quota is trivially met

            default:
                throw new InvalidOperationException($"Unknown bundle kind: {Kind}");
        }
    }

    /// <summary>True if this bundle is fully complete (≥ X ingredients donated).</summary>
    public bool IsFullyComplete(ISet<string> donated)
        => Ingredients.Count(donated.Contains) >= NumberOfSlots;

    /// <summary>Ingredients that are "in play" for the given season — these become the candidate
    /// pool for the planning-hub bonus list.
    /// <list type="bullet">
    ///   <item>Seasonal: all ingredients, but only when its season is the current season.</item>
    ///   <item>PerItem: ingredients pinned to the current season.</item>
    ///   <item>Percentage: ingredients that pass the <paramref name="obtainablePredicate"/>,
    ///         but ONLY when the bundle's cumulative quota for this season is non-zero. A
    ///         zero-quota bundle isn't urgent this season — its items shouldn't pollute the
    ///         bonus pool. 2026-05-28 playtest: Adventurer's Spring quota = 0 was leaking
    ///         Solar/Void Essence into Spring Mining bonus picks because the prior rule only
    ///         filtered by per-item obtainability. The earlier rationale ("rarity weighting
    ///         keeps essences sparse") doesn't hold when both essences are priced as Common
    ///         (40g) / Uncommon (50g) — their rarity matches Quartz so weighting can't keep
    ///         them out.</item>
    /// </list>
    /// </summary>
    public IEnumerable<string> InPlayItemsFor(Season season, Func<string, bool> obtainablePredicate)
    {
        switch (Kind)
        {
            case BundleKind.Seasonal:
                return SeasonalSeason!.Value == season
                    ? Ingredients
                    : Enumerable.Empty<string>();

            case BundleKind.PerItem:
                return ItemSeasonPins!.Where(kv => kv.Value == season).Select(kv => kv.Key);

            case BundleKind.Percentage:
                if (CumulativeRequiredBySeason![(int)season] == 0)
                    return Enumerable.Empty<string>();
                return Ingredients.Where(obtainablePredicate);

            default:
                return Enumerable.Empty<string>();
        }
    }
}
