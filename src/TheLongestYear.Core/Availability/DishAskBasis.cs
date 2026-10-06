using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>The generated per-season weekly basis for cooked dishes (spec
/// docs/superpowers/specs/2026-09-30-quantity-rules-design.md, section 5). Built once where the
/// availability model is built and read by <see cref="QuantityAskPass"/> after the hand rows in
/// <see cref="SeasonalAskBasis"/>, which win for any id they list.
///
/// For a dish and a season: 0 before the season of the dish's model week; otherwise the larger of
/// what can be cooked (the scarcest ingredient's basis per unit the recipe takes, times
/// <see cref="IngredientShare"/>, never below 1, capped by the recipe's effort without the kitchen)
/// and what can be bought (<see cref="ShopBudget"/> over the shelf price, capped at
/// <see cref="ShopCap"/>). A dish the model cannot place gets no row and stays a single ask.</summary>
public static class DishAskBasis
{
    /// <summary>The part of an ingredient's supply a dish may claim; the ingredient has other uses.</summary>
    public const double IngredientShare = 0.25;
    /// <summary>Cooked caps by effort without the kitchen: 3 or less, 4 to 6, 7 and up.</summary>
    public const int CapEasyDish = 12, CapMidDish = 8, CapHardDish = 4;
    private const int EasyEffortMax = 3;
    private const int MidEffortMax = 6;
    /// <summary>Gold a week a player spends on one shop dish; basis = budget / price.</summary>
    public const double ShopBudget = 6000;
    public const int ShopCap = 25;
    /// <summary>A cooked amount never falls below one dish.</summary>
    private const int CookedFloor = 1;
    /// <summary>An ingredient nothing can size counts as one a week.</summary>
    private const double MissingBasis = 1;
    private const int SeasonCount = 4;

    /// <summary>Year-round shop dishes and their shelf price (Data/Shops, price x2 markup): the
    /// Saloon's Salad, Bread, Spaghetti and Pizza at twice Data/Objects' price, Willy's Trout Soup at
    /// its listed 250. Festival stock and the Traveling Cart do not count.</summary>
    public static readonly IReadOnlyDictionary<string, int> ShopDishPrices = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)196"] = 220,   // Salad
        ["(O)216"] = 120,   // Bread
        ["(O)224"] = 240,   // Spaghetti
        ["(O)206"] = 600,   // Pizza
        ["(O)219"] = 250,   // Trout Soup
    };

    /// <param name="availabilityOf">The dish's placement, or null when nothing placed it.</param>
    /// <param name="ingredientBasis">A non-dish ingredient's basis by deadline season, or null.</param>
    /// <param name="effortOf">Effort for an ingredient, read without any kitchen term.</param>
    public static IReadOnlyDictionary<string, double[]> Build(
        EffortData data, Func<string, ItemAvailability?> availabilityOf,
        Func<string, Season, double?> ingredientBasis, Func<string, int?> effortOf)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (availabilityOf == null) throw new ArgumentNullException(nameof(availabilityOf));
        if (ingredientBasis == null) throw new ArgumentNullException(nameof(ingredientBasis));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));

        // Every recipe for a dish, in data order; the dish ids themselves are walked in ordinal
        // order so memoisation and the cycle guard resolve the same way every time.
        var recipesByDish = new Dictionary<string, List<RawCookingRecipe>>(StringComparer.Ordinal);
        foreach (RawCookingRecipe recipe in data.CookingRecipes)
        {
            if (!recipesByDish.TryGetValue(recipe.OutputItemId, out List<RawCookingRecipe>? list))
                recipesByDish[recipe.OutputItemId] = list = new List<RawCookingRecipe>();
            list.Add(recipe);
        }

        var memo = new Dictionary<string, double[]?>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        double[]? RowOf(string dishId)
        {
            if (memo.TryGetValue(dishId, out double[]? known)) return known;
            if (!visiting.Add(dishId)) return new double[SeasonCount];
            try
            {
                double[]? row = Compute(dishId);
                memo[dishId] = row;
                return row;
            }
            finally
            {
                visiting.Remove(dishId);
            }
        }

        double? BasisOf(string ingredient, Season season)
        {
            if (int.TryParse(ingredient, out int category) && category < 0)
            {
                double? best = null;
                foreach (KeyValuePair<string, RawObjectEntry> kv in data.Objects.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (kv.Value.Category != category) continue;
                    double? b = ingredientBasis(BundleParsing.NormalizeItemId(kv.Key), season);
                    if (b != null && (best == null || b > best)) best = b;
                }
                return best;
            }
            string id = BundleParsing.NormalizeItemId(ingredient);
            if (recipesByDish.ContainsKey(id))
                return RowOf(id)?[(int)season];
            return ingredientBasis(id, season);
        }

        double[]? Compute(string dishId)
        {
            ItemAvailability? placed = availabilityOf(dishId);
            if (placed == null) return null;
            int gate = (int)AvailabilityWeeks.SeasonOf(placed.PacingWeek);
            double shop = ShopDishPrices.TryGetValue(dishId, out int price) ? Math.Min(ShopCap, ShopBudget / price) : 0;
            var row = new double[SeasonCount];
            for (int s = gate; s < SeasonCount; s++)
            {
                double cooked = 0;
                foreach (RawCookingRecipe recipe in recipesByDish[dishId])
                    cooked = Math.Max(cooked, Cooked(recipe, (Season)s));
                row[s] = Math.Max(cooked, shop);
            }
            return row;
        }

        double Cooked(RawCookingRecipe recipe, Season season)
        {
            double? scarcest = null;
            for (int i = 0; i < recipe.IngredientIds.Count; i++)
            {
                double b = (BasisOf(recipe.IngredientIds[i], season) ?? MissingBasis) / recipe.CountOf(i);
                if (scarcest == null || b < scarcest) scarcest = b;
            }
            double share = Math.Round((scarcest ?? MissingBasis) * IngredientShare, MidpointRounding.AwayFromZero);
            return Math.Min(Math.Max(CookedFloor, share), CapFor(CookedDishAvailability.EffortWithoutKitchen(recipe, data, effortOf)));
        }

        var table = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (string dishId in recipesByDish.Keys.OrderBy(k => k, StringComparer.Ordinal))
            if (RowOf(dishId) is double[] row)
                table[dishId] = row;
        return table;
    }

    private static int CapFor(int effort)
        => effort <= EasyEffortMax ? CapEasyDish : effort <= MidEffortMax ? CapMidDish : CapHardDish;
}
