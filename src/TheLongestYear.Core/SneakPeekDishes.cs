using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>The year-2 Queen of Sauce dishes a CC board may not ask for (Jeff, 2026-11-10, Nijah's
/// report). A dish whose availability basis carries
/// <see cref="CookedDishAvailability.SneakPeekBasisMarker"/> can only be learned in year 1 by
/// buying the Sneak Peek boost mid-run and catching the right Wednesday rerun, so a Normal board
/// asking for Pink Cake from Summer asked for something most players had no way to make. The
/// marker is the definition: other ways to come by the dish (Chef's reward is 3 Pink Cakes) do not
/// count, because the rule is about learning the recipe.
///
/// Allowed anyway when the step is Hard or Extreme ("hopefully they have an idea of what's
/// available"), or when the dish's recipe was banked in the Cookbook when the board was built (a
/// banked recipe is known from day 1). Easy never places these dishes at all, so the marker never
/// appears there and nothing is excluded here; the Placeable filter keeps them out on Easy.
///
/// Only the CC board's ask pools take this set. Weekly goals keep the dishes with their
/// "(Boost: Sneak Peek)" tag, and the darkness tamper draws from its own pools, so the board can
/// still change to one.</summary>
public static class SneakPeekDishes
{
    /// <summary>The dish ids to leave out of a board's ask pools. Empty on Hard and Extreme and
    /// with no model. <paramref name="bankedDishIds"/> is the board's stamp
    /// (<see cref="MetaState.BoardBankedDishes"/>), never the live Cookbook.</summary>
    public static IReadOnlySet<string> ExcludedFor(
        DifficultyStep step, ItemAvailabilityModel? model, IEnumerable<string>? bankedDishIds)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (model == null || AllowsSneakPeekDishes(step)) return excluded;
        var banked = new HashSet<string>(bankedDishIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (string id in model.KnownIds)
        {
            if (banked.Contains(id)) continue;
            if (IsSneakPeekOnly(model.For(id).Basis)) excluded.Add(id);
        }
        return excluded;
    }

    /// <summary>Hard and Extreme boards may ask for a Sneak Peek dish without a banked recipe.</summary>
    public static bool AllowsSneakPeekDishes(DifficultyStep step)
        => step >= DifficultyStep.Hard;

    /// <summary>True when an availability basis says the Sneak Peek boost is the dish's only year-1
    /// recipe route. The same test SlotPoolBuilder uses for the weekly-goal Boost tag.</summary>
    public static bool IsSneakPeekOnly(string? basis)
        => basis != null && basis.Contains(CookedDishAvailability.SneakPeekBasisMarker, StringComparison.Ordinal);

    /// <summary><paramref name="baseExcluded"/> plus <paramref name="extra"/>. Returns the base set
    /// itself when there is nothing to add, so a board with no exclusion builds exactly as before.</summary>
    public static IReadOnlySet<string>? Union(IReadOnlySet<string>? baseExcluded, IReadOnlySet<string> extra)
    {
        if (extra == null || extra.Count == 0) return baseExcluded;
        var set = baseExcluded != null ? new HashSet<string>(baseExcluded, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        set.UnionWith(extra);
        return set;
    }

    private const char FieldSeparator = '/';
    private const char TokenSeparator = ' ';
    private const int OutputField = 2;

    /// <summary>The dish ids the banked cooking recipes make. <paramref name="cookingRecipes"/> is
    /// Data/CookingRecipes (recipe name to "ingredients/unused/output/unlock..."); a recipe it does
    /// not hold (another mod's, since removed) is skipped. Sorted and distinct, so the stamp is
    /// stable whatever order the Cookbook lists its recipes in.</summary>
    public static List<string> DishIdsFor(
        IEnumerable<string>? recipeNames, IReadOnlyDictionary<string, string>? cookingRecipes)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        if (recipeNames == null || cookingRecipes == null) return ids.ToList();
        foreach (string name in recipeNames)
        {
            if (name == null || !cookingRecipes.TryGetValue(name, out string? data) || data == null) continue;
            string[] fields = data.Split(FieldSeparator);
            if (fields.Length <= OutputField) continue;
            string? output = fields[OutputField].Split(TokenSeparator, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(output)) ids.Add(BundleParsing.NormalizeItemId(output));
        }
        return ids.ToList();
    }
}
