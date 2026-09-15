using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The line-format parsers behind <see cref="GameObtainabilityData"/>: the slash-separated
    /// Data/Fish, Data/Monsters, Data/CookingRecipes and Data/CraftingRecipes rows, the machine output
    /// method names, and the small shape conversions the model's records need. Blind like the rest of
    /// the model: it reads Data assets only and never consults the mod's own item tables.</summary>
    internal static class GameObtainabilityParsing
    {
        private const int RecipeIngredientsField = 0;
        private const int RecipeOutputField = 2;
        private const int CookingUnlockField = 3;
        private const int CraftingBigCraftableField = 3;
        private const int CraftingUnlockField = 4;
        private const string SeedMakerSuffix = "OutputSeedMaker";
        private const string MushroomLogSuffix = "OutputMushroomLog";
        private const string CaskSuffix = "OutputCask";

        internal static OutputMethodKind MethodKind(string? method)
        {
            if (string.IsNullOrEmpty(method)) return OutputMethodKind.None;
            if (method.EndsWith(SeedMakerSuffix, StringComparison.Ordinal)) return OutputMethodKind.SeedMaker;
            if (method.EndsWith(MushroomLogSuffix, StringComparison.Ordinal)) return OutputMethodKind.MushroomLog;
            if (method.EndsWith(CaskSuffix, StringComparison.Ordinal)) return OutputMethodKind.Cask;
            return OutputMethodKind.Unknown;
        }

        internal static RecipeRow Recipe(string name, string row, bool cooking)
        {
            string[] fields = (row ?? "").Split('/');
            int unlockField = cooking ? CookingUnlockField : CraftingUnlockField;
            if (fields.Length <= unlockField) return null;
            string[] ingredientPairs = fields[RecipeIngredientsField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ingredients = new List<string>();
            for (int i = 0; i + 1 < ingredientPairs.Length; i += 2)
                ingredients.Add(int.TryParse(ingredientPairs[i], out int n) && n < 0 ? ingredientPairs[i] : BundleParsing.NormalizeItemId(ingredientPairs[i]));
            // The output field is "id count id count ..."; with several ids the game picks one at random
            // each craft (CraftingRecipe.cs 127-131, 192).
            string[] outputPairs = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool bigCraftable = !cooking && string.Equals(fields[CraftingBigCraftableField].Trim(), "true", StringComparison.OrdinalIgnoreCase);
            var outputIds = new List<string>();
            for (int i = 0; i < outputPairs.Length; i += 2)
                outputIds.Add(bigCraftable && !outputPairs[i].StartsWith("(", StringComparison.Ordinal) ? "(BC)" + outputPairs[i] : BundleParsing.NormalizeItemId(outputPairs[i]));
            if (outputIds.Count == 0) return null;
            return new RecipeRow(name, ingredients, outputIds[0], fields[unlockField].Trim(), cooking,
                outputIds.Count > 1 ? outputIds.Skip(1).ToList() : null);
        }

        internal static IReadOnlyList<AnimalProduce> Produce(List<FarmAnimalProduce> produce)
            => (produce ?? new List<FarmAnimalProduce>())
                .Where(p => !string.IsNullOrEmpty(p?.ItemId))
                .Select(p => new AnimalProduce(BundleParsing.NormalizeItemId(p.ItemId), p.Condition, p.MinimumFriendship)).ToList();

        /// <summary>What a spawn entry can give, ids and item queries alike. A non-empty RandomItemId replaces
        /// ItemId (ItemQueryResolver.cs 804-817) and one entry is picked, so each is random when there are
        /// several; otherwise the ItemId is the one fixed result.</summary>
        internal static IEnumerable<(string Id, bool IsRandom)> Entries(string itemId, List<string> randomItemIds)
        {
            List<string> random = (randomItemIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
            if (random.Count > 0)
            {
                foreach (string id in random) yield return (id, random.Count > 1);
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(itemId)) yield return (itemId.Trim(), false);
        }

        internal static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

        internal static CoreSeason? MapSeason(StardewValley.Season? season)
            => season is StardewValley.Season s ? (CoreSeason)(int)s : null;

        internal static IReadOnlyList<CoreSeason> MapSeasons(List<StardewValley.Season> seasons)
            => (seasons ?? new List<StardewValley.Season>()).Select(s => (CoreSeason)(int)s).Distinct().ToList();
    }
}
