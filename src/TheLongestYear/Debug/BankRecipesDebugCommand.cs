using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug: fill a recipe book to an exact count so a headless run can stage a book
    /// that is over its cap (0.18.17 lowered tier 3 from 20 slots to 16 and grandfathers the
    /// overflow). Takes the first N ids from the game's recipe data, known or not. In memory
    /// only; persists on the next save like every other MetaState edit.</summary>
    internal static class BankRecipesDebugCommand
    {
        public const string Name = "tly_bankrecipes";
        public const string Description =
            "Debug: set a recipe book to hold exactly N recipes (first N ids from the game data). Usage: tly_bankrecipes <cook|craft> <count>";

        public static void Run(IMonitor monitor, MetaState meta, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (meta == null || args.Length < 2 || !int.TryParse(args[1], out int count) || count < 0)
            {
                monitor.Log(Description, LogLevel.Warn);
                return;
            }

            bool cooking;
            switch (args[0].ToLowerInvariant())
            {
                case "cook": cooking = true; break;
                case "craft": cooking = false; break;
                default: monitor.Log(Description, LogLevel.Warn); return;
            }

            List<string> book = cooking ? meta.CookbookRecipes : meta.CraftbookRecipes;
            IEnumerable<string> ids = cooking ? CraftingRecipe.cookingRecipes.Keys : CraftingRecipe.craftingRecipes.Keys;
            book.Clear();
            book.AddRange(ids.Take(count));
            int slots = cooking
                ? UpgradeCatalog.CookbookSlotCount(meta.HighestKeptTier("cookbook_", UpgradeCatalog.BookMaxTier))
                : UpgradeCatalog.CraftbookSlotCount(meta.HighestKeptTier("craftbook_", UpgradeCatalog.BookMaxTier));
            monitor.Log(
                $"tly_bankrecipes: {(cooking ? "Cookbook" : "Craftbook")} now holds {book.Count} " +
                $"(asked {count}), slots={slots}, overCap={RecipeBanking.IsOverCap(slots, book.Count)}.",
                LogLevel.Info);
        }
    }
}
