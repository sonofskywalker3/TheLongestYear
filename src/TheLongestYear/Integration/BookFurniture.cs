using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework.Graphics;
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Integration
{
    /// <summary>Registers the three carried "book" furniture (Cookbook/Craftbook/Bundle-log) as
    /// custom furniture: edits Data/Furniture to add the rows and provides the tilesheet. The
    /// checkForAction interception (open menus) and the per-loop sweep are added in later tasks.</summary>
    internal sealed class BookFurniture
    {
        private const string FurnitureAsset = "Data/Furniture";
        // The texture the Data/Furniture rows point at (mod-provided, loaded from assets/books.png).
        internal const string BookTextureAsset = "Mods/sonofskywalker3.TheLongestYear/Books";

        private readonly IMonitor _monitor;
        private static System.Func<TheLongestYear.UI.MenuLauncher> _launcher;

        public BookFurniture(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        /// <summary>Lazy launcher accessor (the launcher isn't built until OnSaveLoaded). Used by
        /// <see cref="CheckForActionPatch"/> to open the matching menu when a book is clicked.</summary>
        public void AttachLauncher(System.Func<TheLongestYear.UI.MenuLauncher> launcher) => _launcher = launcher;

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(BookTextureAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/books.png", AssetLoadPriority.Medium);
                return;
            }

            if (e.NameWithoutLocale.IsEquivalentTo(FurnitureAsset))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // Format: name/type/tilesheetSize/boundingBox/rotations/price/placementRestriction/DisplayName/spriteIndex/texture
                    // type "decor", 1x1 tile, 1 rotation, free, no placement restriction (-1).
                    data[BookKit.CookbookId]  = $"Cookbook/decor/1 1/1 1/1/0/-1/The Longest Year Cookbook/0/{BookTextureAsset}";
                    data[BookKit.CraftbookId] = $"Craftbook/decor/1 1/1 1/1/0/-1/The Longest Year Craftbook/1/{BookTextureAsset}";
                    data[BookKit.BundleLogId] = $"BundleLog/decor/1 1/1 1/1/0/-1/The Longest Year Bundle Log/2/{BookTextureAsset}";
                }, AssetEditPriority.Default);
            }
        }

        /// <summary>Open the matching menu when one of our book furniture items is acted on
        /// (place it, then press the action button). Returns false to swallow vanilla furniture
        /// behavior for our ids. Furniture's QualifiedItemId is "(F)" + ItemId.</summary>
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.checkForAction))]
        internal static class CheckForActionPatch
        {
            private static bool Prefix(StardewValley.Objects.Furniture __instance, bool justCheckingForActivity, ref bool __result)
            {
                if (justCheckingForActivity) return true;
                TheLongestYear.UI.MenuLauncher launcher = _launcher?.Invoke();
                if (launcher == null) return true;

                switch (__instance.ItemId)
                {
                    case BookKit.CookbookId:  launcher.OpenCookbook();    __result = true; return false;
                    case BookKit.CraftbookId: launcher.OpenCraftbook();   __result = true; return false;
                    case BookKit.BundleLogId: launcher.OpenSeasonGoals(); __result = true; return false;
                    default: return true;
                }
            }
        }
    }
}
