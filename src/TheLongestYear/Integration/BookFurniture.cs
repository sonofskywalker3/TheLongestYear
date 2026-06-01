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

        public BookFurniture(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

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
    }
}
