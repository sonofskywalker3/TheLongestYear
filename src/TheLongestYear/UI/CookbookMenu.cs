using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The Cookbook: banks cooking recipes in <see cref="MetaState.CookbookRecipes"/>, with
    /// <see cref="UpgradeCatalog.CookbookSlotCount"/> slots for the highest owned cookbook tier. Opened
    /// from the placeable Cookbook. Everything else lives in <see cref="RecipeBookMenu"/>.</summary>
    internal sealed class CookbookMenu : RecipeBookMenu
    {
        public CookbookMenu(IMonitor monitor, MetaState meta, string subtitle = null)
            : base(monitor, meta, subtitle, "cookbook_", UpgradeCatalog.CookbookSlotCount, isCooking: true,
                IntroQuestIds.LegacyCookbookDismissed, rowIdBase: 8100, scrollUpId: 8900, scrollDownId: 8901)
        {
        }

        protected override List<string> Banked => _meta.CookbookRecipes;

        protected override string TitleText(int used, int total)
            => Strings.Get("menu.cookbook.title", new Dictionary<string, string>
            {
                ["used"] = used.ToString(),
                ["total"] = total.ToString(),
            });

        protected override string RemoveConfirmText(string recipeName)
            => Strings.Get("menu.cookbook.remove-confirm", new Dictionary<string, string> { ["recipe"] = recipeName });

        protected override List<string> AvailableRecipesToBank()
            => RecipeBanking.Bankable(Game1.player.cookingRecipes.Keys, _meta.CookbookRecipes,
                TheLongestYear.Loop.RecipeDefaults.IsDefaultCooking);
    }
}
