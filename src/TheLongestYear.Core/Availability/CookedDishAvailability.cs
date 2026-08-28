using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for a cooked dish (Data/CookingRecipes): its hardest ingredient (a category
/// ref such as "any milk" uses the cheapest member), plus the recipe unlock, plus the kitchen
/// (house upgrade 1, dropped when keep_kitchen is owned). A dish with an ingredient no rule can
/// place is Extreme.</summary>
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

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf, bool hasKitchen)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        ItemEffort? best = null;
        foreach (RawCookingRecipe recipe in data.CookingRecipes)
        {
            if (recipe.OutputItemId != qualifiedId) continue;
            int hardest = 0;
            string hardestId = "";
            string? missing = null;
            foreach (string ingredient in recipe.IngredientIds)
            {
                int? e = IngredientEffort(ingredient, data, effortOf);
                if (e == null) { missing = ingredient; break; }
                if (e >= hardest) { hardest = e.Value; hardestId = ingredient; }
            }
            ItemEffort candidate = missing != null
                ? new ItemEffort(ExtremeEffort, $"dish {recipe.Name}: ingredient {missing} unrecognised, extreme")
                : new ItemEffort(
                    hardest + UnlockEffort(recipe.UnlockCondition) + (hasKitchen ? 0 : KitchenCost),
                    $"dish {recipe.Name}: hardest ingredient {hardestId} ({hardest}) + unlock '{recipe.UnlockCondition}' "
                    + $"({UnlockEffort(recipe.UnlockCondition)}) + kitchen {(hasKitchen ? 0 : KitchenCost)}");
            if (best == null || candidate.Effort < best.Effort)
                best = candidate;
        }
        return best == null ? null : new ItemEffort(best.Effort, $"{best.Basis}, effort {best.Effort}");
    }

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
}
