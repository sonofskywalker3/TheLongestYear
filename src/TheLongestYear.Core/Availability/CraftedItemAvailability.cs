using System;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for an object made at the crafting menu (Data/CraftingRecipes,
/// big craftables excluded: those are machines). The dish rule without the kitchen: the hardest
/// ingredient plus the recipe unlock, and the week is the later of the recipe's week and the
/// latest ingredient's week. The unlock and the ingredients are read exactly as a dish's are
/// (<see cref="CookedDishAvailability"/>), except that no Queen of Sauce episode teaches a crafting
/// recipe ("l N" leans on the shop price alone). An empty unlock is "default". A "null" or "none"
/// unlock is taught by a shop, letter or event, so only a shop price places it, unless the recipe
/// is in <see cref="MadeSources.EventTaughtUnlocks"/> (Jeff's ruling 2026-09-16): then the event's
/// friendship condition (or, for a recipe the museum teaches, "donate &lt;id&gt;": the week that item can
/// first be had, at the special-unlock effort) stands in for the unlock, and the basis shows the
/// substitution.
/// A recipe that asks for "any wild seed" (-777, a code-only group the dish rule never meets) reads it
/// as the cheapest of the four packets (<see cref="MadeSources.WildSeedIds"/>): the earliest week, then
/// the lowest effort.</summary>
public static class CraftedItemAvailability
{
    private const string DefaultUnlock = "default";
    private const string TaughtElsewhereUnlock = "null";

    /// <summary>The unlock the rule dates the recipe from, and the text the basis shows for it. The
    /// words are MadeSources' own: a known-from-start word reads as "default", and every
    /// taught-elsewhere word ("none" too) reads as "null", so they cost the same.</summary>
    private static (string Unlock, string Label) EffectiveUnlock(CraftingRecipe recipe)
    {
        string text = (recipe.Unlock ?? "").Trim();
        string word = text.ToLowerInvariant();
        if (MadeSources.KnownFromStartWords.Contains(word)) return (DefaultUnlock, text);
        if (!MadeSources.TaughtElsewhereWords.Contains(word)) return (text, text);
        return MadeSources.EventTaughtUnlocks.TryGetValue(recipe.Name, out string? taughtBy)
            ? (taughtBy, $"{text} → {taughtBy}")
            : (TaughtElsewhereUnlock, text);
    }

    /// <summary>The wild seed packet an "any wild seed" ingredient is read as: of the packets with an
    /// effort, the one with the earliest week (an unplaced packet last), then the lowest effort, then
    /// the first in <see cref="MadeSources.WildSeedIds"/>. Null when no packet has an effort.</summary>
    private static string? CheapestWildSeed(Func<string, int?> effortOf, Func<string, int?> weekOf)
        => MadeSources.WildSeedIds
            .Select(id => (Id: id, Effort: effortOf(id), Week: weekOf(id)))
            .Where(p => p.Effort != null)
            .OrderBy(p => p.Week ?? int.MaxValue)
            .ThenBy(p => p.Effort)
            .Select(p => p.Id)
            .FirstOrDefault();

    /// <param name="step">Required for the same reason as the dish rule's: the recipe-week reading
    /// it shares takes the difficulty step.</param>
    /// <param name="shopWeeks">The shops the walk placed, which date a recipe's shop rows.</param>
    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf,
        Func<string, int?>? weekOf, DifficultyStep step, ShopWeeks? shopWeeks = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (CraftingRecipe recipe in data.CraftingRecipes)
        {
            if (recipe.OutputId != qualifiedId) continue;
            (string unlock, string unlockLabel) = EffectiveUnlock(recipe);
            int unlockEffort = CookedDishAvailability.UnlockEffort(unlock);
            int hardest = 0;
            string hardestId = "";
            string? missing = null;
            string? unplaced = null;
            int? latestWeek = 1;
            foreach (RecipeIngredient ingredient in recipe.Ingredients)
            {
                string id = ingredient.Id == MadeSources.AnyWildSeed ? CheapestWildSeed(effortOf, weekOf) ?? ingredient.Id : ingredient.Id;
                int? e = id == MadeSources.AnyWildSeed ? null : CookedDishAvailability.IngredientEffort(id, data, effortOf);
                if (e == null) { missing = ingredient.Id; break; }
                if (e >= hardest) { hardest = e.Value; hardestId = id; }
                int? w = CookedDishAvailability.IngredientWeek(id, data, weekOf);
                if (w == null) { latestWeek = null; unplaced ??= id; }
                else if (latestWeek != null && w > latestWeek) latestWeek = w;
            }
            int? recipeWeek = CookedDishAvailability.CraftingRecipeWeek(unlock, recipe.OutputId, data, step, shopWeeks);
            // A museum-taught recipe is learned the week the donated item can first be had.
            if (MadeSources.DonatedItem(unlock) is string donated && weekOf(donated) is int donatedWeek)
                recipeWeek = recipeWeek == null ? donatedWeek : Math.Min(recipeWeek.Value, donatedWeek);
            if (recipeWeek == null) latestWeek = null;
            else if (latestWeek != null) latestWeek = Math.Max(latestWeek.Value, recipeWeek.Value);
            ItemEffort candidate = missing != null
                ? new ItemEffort(CookedDishAvailability.ExtremeEffort, $"crafted {recipe.Name}: ingredient {missing} unrecognised, extreme")
                : new ItemEffort(
                    hardest + unlockEffort,
                    $"crafted {recipe.Name}: hardest ingredient {hardestId} ({hardest}) + unlock '{unlockLabel}' ({unlockEffort})"
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
