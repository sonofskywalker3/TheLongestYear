using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>The recipes a new save starts with, read the same way vanilla's
    /// <c>Farmer.LearnDefaultRecipes</c> reads them: the unlock field of Data/CraftingRecipes
    /// (index 4) and Data/CookingRecipes (index 3) equals "default". The reset re-seeds exactly
    /// these, so banking one in a book keeps nothing.</summary>
    internal static class RecipeDefaults
    {
        private const int CraftingUnlockField = 4;
        private const int CookingUnlockField = 3;
        private const string DefaultUnlock = "default";

        public static bool IsDefaultCrafting(string recipeId)
            => CraftingRecipe.craftingRecipes.TryGetValue(recipeId, out string data)
               && ArgUtility.Get(data.Split('/'), CraftingUnlockField) == DefaultUnlock;

        public static bool IsDefaultCooking(string recipeId)
            => CraftingRecipe.cookingRecipes.TryGetValue(recipeId, out string data)
               && ArgUtility.Get(data.Split('/'), CookingUnlockField) == DefaultUnlock;
    }
}
