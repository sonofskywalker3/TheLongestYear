using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The Craftbook: banks crafting recipes in <see cref="MetaState.CraftbookRecipes"/>, with
    /// <see cref="UpgradeCatalog.CraftbookSlotCount"/> slots for the highest owned craftbook tier. Opened
    /// from the placeable Craftbook. Everything else lives in <see cref="RecipeBookMenu"/>.</summary>
    internal sealed class CraftbookMenu : RecipeBookMenu
    {
        public CraftbookMenu(IMonitor monitor, MetaState meta, string subtitle = null)
            : base(monitor, meta, subtitle, "craftbook_", UpgradeCatalog.CraftbookSlotCount, isCooking: false,
                IntroQuestIds.LegacyCraftbookDismissed, rowIdBase: 8200, scrollUpId: 8950, scrollDownId: 8951)
        {
        }

        protected override List<string> Banked => _meta.CraftbookRecipes;

        protected override string TitleText(int used, int total)
            => Strings.Get("menu.craftbook.title", new Dictionary<string, string>
            {
                ["used"] = used.ToString(),
                ["total"] = total.ToString(),
            });

        protected override string RemoveConfirmText(string recipeName)
            => Strings.Get("menu.craftbook.remove-confirm", new Dictionary<string, string> { ["recipe"] = recipeName });

        protected override List<string> AvailableRecipesToBank()
            => RecipeBanking.Bankable(Game1.player.craftingRecipes.Keys, _meta.CraftbookRecipes,
                TheLongestYear.Loop.RecipeDefaults.IsDefaultCrafting);
    }
}
