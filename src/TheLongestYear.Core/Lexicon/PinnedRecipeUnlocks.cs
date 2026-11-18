using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Recipe unlocks a supported mod's layer names (<see cref="LayerInputs.RecipeUnlocks"/>).
///
/// A mod can teach a recipe by a letter or a scene and write its data unlock as "null" or "mail",
/// which the rules read as "not in year 1" (a dish) or "the special-order mail week" (a machine).
/// Vanilla's own such recipes are a hand table (<see cref="Obtainability.MadeSources"/>' event-taught
/// unlocks); a layer names its mod's the same way, in the game's own unlock words ("s Fishing 2",
/// "f Wizard 4", "default"), and every reader then sees that unlock in place of the data's. The
/// game's own data is never changed: a pinned asset is read from a copy.</summary>
public static class PinnedRecipeUnlocks
{
    public const string CraftingAsset = "Data/CraftingRecipes";
    public const string CookingAsset = "Data/CookingRecipes";

    /// <summary>The slash field that holds the unlock, by asset: a crafting row is
    /// "ingredients/unused/yield/big craftable/unlock/name", a cooking row
    /// "ingredients/unused/yield/unlock/name" (CraftingRecipe.cs 112-140).</summary>
    public static readonly IReadOnlyDictionary<string, int> UnlockFields = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [CraftingAsset] = 4,
        [CookingAsset] = 3,
    };

    /// <summary>A copy of the recipe rows with each named recipe's unlock field replaced. A recipe
    /// the game does not hold, or a row too short to have the field, changes nothing. Returns
    /// <paramref name="live"/> itself when nothing applies.</summary>
    public static Dictionary<string, string> Apply(string asset, Dictionary<string, string> live, IReadOnlyDictionary<string, string>? unlocks)
    {
        if (live == null) throw new ArgumentNullException(nameof(live));
        if (unlocks == null || unlocks.Count == 0 || !UnlockFields.TryGetValue(asset ?? "", out int field)) return live;
        Dictionary<string, string>? read = null;
        foreach (var (key, unlock) in unlocks)
        {
            if (!live.TryGetValue(key, out string? row) || row == null) continue;
            string[] fields = row.Split('/');
            if (fields.Length <= field || fields[field] == unlock) continue;
            fields[field] = unlock;
            read ??= new Dictionary<string, string>(live, live.Comparer);
            read[key] = string.Join("/", fields);
        }
        return read ?? live;
    }
}
