using System;
using System.Collections.Generic;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Live "can this item feed any CC bundle?" lookup for the Cart Whisperer shrine display. Builds,
    /// once per save (lazily), the set of bundle-relevant item ids from the current CC's bundle data,
    /// the crop data (seed→crop), and the crafting/cooking recipes (ingredient→product), via the pure
    /// <see cref="BundleRelevance"/>. Also tracks the category refs bundles use (e.g. "-5" any animal
    /// product) so an item is relevant when its Category matches. Cached; call <see cref="Invalidate"/>
    /// on save change.
    /// </summary>
    internal static class BundleRelevanceIndex
    {
        private static HashSet<string> _relevantIds;
        private static HashSet<int> _bundleCategories;

        public static void Invalidate()
        {
            _relevantIds = null;
            _bundleCategories = null;
        }

        /// <summary>True when the item (by qualified id) or its Category can contribute to any bundle.</summary>
        public static bool IsRelevant(Item item)
        {
            if (item == null) return false;
            EnsureBuilt();
            if (_relevantIds.Contains(item.QualifiedItemId)) return true;
            if (item.Category < 0 && _bundleCategories.Contains(item.Category)) return true;
            return false;
        }

        private static void EnsureBuilt()
        {
            if (_relevantIds != null) return;

            var bundleItems = new HashSet<string>(StringComparer.Ordinal);
            var categories = new HashSet<int>();
            foreach (KeyValuePair<string, string> kvp in Game1.netWorldState.Value.BundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                foreach (BundleIngredient ing in bundle.Ingredients)
                {
                    if (BundleParsing.IsCategoryRef(ing.ItemRef))
                    {
                        if (int.TryParse(ing.ItemRef, out int cat))
                            categories.Add(cat);
                    }
                    else
                    {
                        bundleItems.Add(BundleParsing.NormalizeItemId(ing.ItemRef));
                    }
                }
            }

            var seedToCrop = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, StardewValley.GameData.Crops.CropData> kvp in Game1.cropData)
            {
                if (kvp.Value?.HarvestItemId == null) continue;
                seedToCrop[BundleParsing.NormalizeItemId(kvp.Key)] = BundleParsing.NormalizeItemId(kvp.Value.HarvestItemId);
            }

            var productToIngredients = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            AddRecipes(productToIngredients, CraftingRecipe.craftingRecipes, isCooking: false);
            AddRecipes(productToIngredients, CraftingRecipe.cookingRecipes, isCooking: true);

            _relevantIds = BundleRelevance.BuildRelevantItemIds(bundleItems, seedToCrop, productToIngredients);
            _bundleCategories = categories;
        }

        private static void AddRecipes(
            Dictionary<string, IReadOnlyCollection<string>> map,
            Dictionary<string, string> recipes, bool isCooking)
        {
            if (recipes == null) return;
            foreach (string name in recipes.Keys)
            {
                CraftingRecipe r;
                try { r = new CraftingRecipe(name, isCooking); }
                catch (Exception) { continue; }

                if (r.itemToProduce == null || r.itemToProduce.Count == 0) continue;
                string productId = BundleParsing.NormalizeItemId(r.itemToProduce[0]);

                var ingredients = new List<string>();
                foreach (string ingId in r.recipeList.Keys)
                {
                    if (BundleParsing.IsCategoryRef(ingId)) continue; // recipe category ingredient — skip
                    ingredients.Add(BundleParsing.NormalizeItemId(ingId));
                }
                if (ingredients.Count > 0)
                    map[productId] = ingredients;
            }
        }
    }
}
