using System;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for an object made at the crafting menu (Data/CraftingRecipes,
/// big craftables excluded: those are machines). The dish rule without the kitchen: the hardest
/// ingredient plus the recipe unlock, and the week is the later of the recipe's week and the
/// latest ingredient's week. The unlock and the ingredients are read exactly as a dish's are
/// (<see cref="CookedDishAvailability"/>), except that no Queen of Sauce episode teaches a crafting
/// recipe and a "null" unlock is not the Cookies event, so both lean on the shop price alone.
/// A recipe that asks for "any wild seed" (-777, a code-only group the dish rule never meets) is
/// skipped rather than guessed at.</summary>
public static class CraftedItemAvailability
{
    private const string AnyWildSeed = "-777";

    /// <param name="step">Required for the same reason as the dish rule's: the recipe-week reading
    /// it shares takes the difficulty step.</param>
    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf,
        Func<string, int?>? weekOf, DifficultyStep step)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (CraftingRecipe recipe in data.CraftingRecipes)
        {
            if (recipe.OutputId != qualifiedId) continue;
            if (recipe.Ingredients.Any(i => i.Id == AnyWildSeed)) continue;
            int unlockEffort = CookedDishAvailability.UnlockEffort(recipe.Unlock);
            int hardest = 0;
            string hardestId = "";
            string? missing = null;
            string? unplaced = null;
            int? latestWeek = 1;
            foreach (RecipeIngredient ingredient in recipe.Ingredients)
            {
                int? e = CookedDishAvailability.IngredientEffort(ingredient.Id, data, effortOf);
                if (e == null) { missing = ingredient.Id; break; }
                if (e >= hardest) { hardest = e.Value; hardestId = ingredient.Id; }
                int? w = CookedDishAvailability.IngredientWeek(ingredient.Id, data, weekOf);
                if (w == null) { latestWeek = null; unplaced ??= ingredient.Id; }
                else if (latestWeek != null && w > latestWeek) latestWeek = w;
            }
            int? recipeWeek = CookedDishAvailability.CraftingRecipeWeek(recipe.Unlock, recipe.OutputId, data, step);
            if (recipeWeek == null) latestWeek = null;
            else if (latestWeek != null) latestWeek = Math.Max(latestWeek.Value, recipeWeek.Value);
            ItemEffort candidate = missing != null
                ? new ItemEffort(CookedDishAvailability.ExtremeEffort, $"crafted {recipe.Name}: ingredient {missing} unrecognised, extreme")
                : new ItemEffort(
                    hardest + unlockEffort,
                    $"crafted {recipe.Name}: hardest ingredient {hardestId} ({hardest}) + unlock '{recipe.Unlock}' ({unlockEffort})"
                    + (recipeWeek == null ? ", recipe not in year 1" : "")
                    + (unplaced == null ? "" : $", ingredient {unplaced} not in year 1"),
                    latestWeek, latestWeek == null ? null : AvailabilityWeeks.SeasonOf(latestWeek.Value));
            bool better = best == null
                || (candidate.EarliestWeek ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                || (candidate.EarliestWeek == best.EarliestWeek && candidate.Effort < best.Effort);
            if (better)
                best = candidate;
        }
        return best == null
            ? null
            : new ItemEffort(best.Effort, $"{best.Basis}, week {(best.EarliestWeek?.ToString() ?? "unknown")}, effort {best.Effort}",
                best.EarliestWeek, best.GateSeason);
    }
}
