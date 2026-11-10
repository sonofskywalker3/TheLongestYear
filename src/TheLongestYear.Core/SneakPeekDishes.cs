using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;

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
/// appears there and nothing is excluded here; the Placeable filter keeps them out on Easy, unless
/// the recipe is banked (Jeff, 2026-11-10): then the board's model carries the lexicon's stored
/// Normal answer for the dish (<see cref="Bankable"/>, <see cref="ItemAvailabilityModel.WithBankedDishes"/>),
/// so it is placed at the year-2 episode's week. That week is never earlier than the recipe can be
/// known, since a banked recipe is known from day 1. No week is worked out at board time.
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

    /// <summary>The stored answers a banked recipe places on a step that leaves the dish unplaced
    /// (Easy, Jeff 2026-11-10): every dish <paramref name="normal"/> places with the Sneak Peek marker
    /// that <paramref name="own"/> does not place. Both must be the lexicon's stored answers for the
    /// same inputs and kitchen state; this only picks rows, it computes nothing. Empty when either is
    /// missing, and on Normal, Hard and Extreme, whose own answers already place these dishes.</summary>
    public static IReadOnlyDictionary<string, BankableDish> Bankable(AvailabilityAnswers? own, AvailabilityAnswers? normal)
    {
        var bankable = new Dictionary<string, BankableDish>(StringComparer.Ordinal);
        if (own == null || normal == null || ReferenceEquals(own, normal)) return bankable;
        foreach (var (id, answer) in normal.EffortDerived)
        {
            if (answer.EarliestWeek == null || !IsSneakPeekOnly(answer.Basis)) continue;
            if (own.Derived.ContainsKey(id)) continue;
            if (own.EffortDerived.TryGetValue(id, out ItemEffort? mine) && mine.EarliestWeek != null) continue;
            bankable[id] = new BankableDish(answer, normal.DishBases.TryGetValue(id, out double[]? basis) ? basis : null);
        }
        return bankable;
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

/// <summary>A stored answer for a Sneak Peek dish that a banked recipe places
/// (<see cref="SneakPeekDishes.Bankable"/>): its effort row and its dish quantity basis, as the
/// lexicon's Normal section holds them.</summary>
public sealed record BankableDish(ItemEffort Answer, double[]? DishBasis);
