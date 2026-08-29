using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for a cooked dish (Data/CookingRecipes): its hardest
/// ingredient (a category ref such as "any milk" uses the cheapest member), plus the recipe
/// unlock, plus the kitchen (house upgrade 1, dropped when keep_kitchen is owned). A dish with an
/// ingredient no rule can place is Extreme and, for the week, unplaced. The week is the later of
/// the kitchen week (AvailabilityWeeks.KitchenWeek, whatever keep_kitchen says, so boards stay
/// deterministic across that upgrade) and the latest ingredient's week.</summary>
public static class CookedDishAvailability
{
    public const int ExtremeEffort = 12;
    public const int KitchenCost = 1;
    private const int TvUnlockEffort = 1;
    private const int LowSkillMax = 5;
    private const int SkillUnlockLow = 1;
    private const int SkillUnlockHigh = 2;
    private const int FriendshipUnlockEffort = 2;
    private const int SpecialUnlockEffort = 3;
    private const string SkillPrefix = "s";
    private const string FriendshipPrefix = "f";
    private const string TvUnlock = "l";

    public static int UnlockEffort(string? unlock)
    {
        string text = (unlock ?? "").Trim();
        if (text.Equals("default", StringComparison.OrdinalIgnoreCase)) return 0;
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase)) return TvUnlockEffort;
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase)) return FriendshipUnlockEffort;
        if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3
            && int.TryParse(tokens[^1], out int level))
            return level <= LowSkillMax ? SkillUnlockLow : SkillUnlockHigh;
        return SpecialUnlockEffort;
    }

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf, bool hasKitchen,
        Func<string, int?>? weekOf = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (RawCookingRecipe recipe in data.CookingRecipes)
        {
            if (recipe.OutputItemId != qualifiedId) continue;
            int hardest = 0;
            string hardestId = "";
            string? missing = null;
            // The week ignores keep_kitchen on purpose: the engine's Spring foothold reads the
            // gate season, and the board must regenerate identically before and after that
            // upgrade (it is compared byte for byte at save load). keep_kitchen still drops effort.
            int? latestWeek = AvailabilityWeeks.KitchenWeek;
            foreach (string ingredient in recipe.IngredientIds)
            {
                int? e = IngredientEffort(ingredient, data, effortOf);
                if (e == null) { missing = ingredient; break; }
                if (e >= hardest) { hardest = e.Value; hardestId = ingredient; }
                int? w = IngredientWeek(ingredient, data, weekOf);
                if (w == null) latestWeek = null;
                else if (latestWeek != null && w > latestWeek) latestWeek = w;
            }
            int? recipeWeek = RecipeWeek(recipe, data);
            if (recipeWeek == null) latestWeek = null;
            else if (latestWeek != null) latestWeek = Math.Max(latestWeek.Value, recipeWeek.Value);
            ItemEffort candidate = missing != null
                ? new ItemEffort(ExtremeEffort, $"dish {recipe.Name}: ingredient {missing} unrecognised, extreme")
                : new ItemEffort(
                    hardest + UnlockEffort(recipe.UnlockCondition) + (hasKitchen ? 0 : KitchenCost),
                    $"dish {recipe.Name}: hardest ingredient {hardestId} ({hardest}) + unlock '{recipe.UnlockCondition}' "
                    + $"({UnlockEffort(recipe.UnlockCondition)}) + kitchen {(hasKitchen ? 0 : KitchenCost)}"
                    + (recipeWeek == null ? ", recipe not in year 1" : $", recipe week {recipeWeek}"),
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

    /// <summary>Week the recipe itself is learned, or null when it is not in year 1 (a year-2
    /// episode, Kent, Leo).
    ///
    /// Every "l N" row is a Queen of Sauce recipe and is read the same way, whatever N is: the
    /// EARLIER of its year-1 episode week (episodes 1 to 16; a later episode contributes nothing)
    /// and the week the player can afford it in a shop, if any shop sells it. The number is not
    /// branched on. "l 100" simply has no shop row, so the episode week is all that is left. "f
    /// NPC N" takes the hearts table, again against the price week. "s Skill N" the level week.
    /// "default" is week 1. "null" is Cookies (event 19).</summary>
    public static int? RecipeWeek(RawCookingRecipe recipe, EffortData data)
    {
        string text = (recipe.UnlockCondition ?? "").Trim();
        if (text.Equals("default", StringComparison.OrdinalIgnoreCase)) return 1;
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase)) return AvailabilityWeeks.CookiesWeek;
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int? priceWeek = data.RecipePrices.TryGetValue(recipe.OutputItemId, out int price) ? UnlockWeeks.ForCost(price) : null;
        if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3 && int.TryParse(tokens[^1], out int hearts))
            return Min(UnlockWeeks.ForFriendship(tokens[1], hearts), priceWeek);
        if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3 && int.TryParse(tokens[^1], out int level))
            return Min(AvailabilityWeeks.MachineLevelWeek(level), priceWeek);
        if (tokens[0].Equals(TvUnlock, StringComparison.OrdinalIgnoreCase))
        {
            int? episode = data.CookingChannel.TryGetValue(recipe.Name, out int e) ? e : null;
            int? tvWeek = episode != null && episode.Value <= AvailabilityWeeks.YearOneEpisodes ? episode : null;
            return Min(tvWeek, priceWeek);
        }
        return Min(null, priceWeek);
    }

    private static int? Min(int? a, int? b) => a == null ? b : b == null ? a : Math.Min(a.Value, b.Value);

    private static int? IngredientEffort(string ingredient, EffortData data, Func<string, int?> effortOf)
    {
        if (int.TryParse(ingredient, out int category) && category < 0)
        {
            int? cheapest = null;
            foreach (KeyValuePair<string, RawObjectEntry> kv in data.Objects.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (kv.Value.Category != category) continue;
                int? e = effortOf(BundleParsing.NormalizeItemId(kv.Key));
                if (e != null && (cheapest == null || e < cheapest)) cheapest = e;
            }
            return cheapest;
        }
        return effortOf(BundleParsing.NormalizeItemId(ingredient));
    }

    /// <summary>A category ref takes the earliest member with a week; a member without one is
    /// skipped, so "any fish" is as early as the earliest placed fish.</summary>
    private static int? IngredientWeek(string ingredient, EffortData data, Func<string, int?> weekOf)
    {
        if (int.TryParse(ingredient, out int category) && category < 0)
        {
            int? earliest = null;
            foreach (KeyValuePair<string, RawObjectEntry> kv in data.Objects.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (kv.Value.Category != category) continue;
                int? w = weekOf(BundleParsing.NormalizeItemId(kv.Key));
                if (w != null && (earliest == null || w < earliest)) earliest = w;
            }
            return earliest;
        }
        return weekOf(BundleParsing.NormalizeItemId(ingredient));
    }
}
