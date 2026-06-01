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

        /// <summary>Per-loop invariant: the player holds exactly one of each book. Remove every
        /// instance from all locations + the inventory, then grant one back. Keeps the books from
        /// multiplying or being lost wherever they were left.</summary>
        public void ReconcileInventory()
        {
            StardewValley.Farmer p = StardewValley.Game1.player;
            if (p == null) return;

            foreach (string id in BookKit.AllBookQualifiedIds)
            {
                // 1. Remove placed copies in every location (collect-then-remove by reference).
                StardewValley.Utility.ForEachLocation(loc =>
                {
                    var placed = new System.Collections.Generic.List<StardewValley.Objects.Furniture>();
                    foreach (StardewValley.Objects.Furniture f in loc.furniture)
                        if (f.ItemId == id) placed.Add(f);
                    foreach (StardewValley.Objects.Furniture f in placed)
                        loc.furniture.Remove(f);
                    return true;
                });

                // 2. Keep exactly one inventory copy; null out any extras.
                int kept = 0;
                for (int i = 0; i < p.Items.Count; i++)
                {
                    if (p.Items[i] != null && p.Items[i].ItemId == id)
                    {
                        if (kept == 0) kept = 1;          // keep the first
                        else p.Items[i] = null;           // drop duplicates
                    }
                }

                // 3. Grant one if none was held (GrantCountToReachOne(0) == 1, (1) == 0).
                if (BookKit.GrantCountToReachOne(kept) > 0)
                    p.addItemToInventory(StardewValley.Objects.Furniture.GetFurnitureInstance(id));
            }

            _monitor.Log("BookFurniture: reconciled the three books to exactly one each in inventory.", LogLevel.Info);
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
                    // The texture asset path MUST use backslashes here: Data/Furniture is split on '/'
                    // (FurnitureDataDefinition.GetRawData), so a "Mods/.../Books" path would be truncated
                    // to "Mods" in field 9. SMAPI normalizes '\' vs '/', so it still matches registration.
                    string tex = BookTextureAsset.Replace('/', '\\');
                    data[BookKit.CookbookId]  = $"Cookbook/decor/1 1/1 1/1/0/-1/The Longest Year Cookbook/0/{tex}";
                    data[BookKit.CraftbookId] = $"Craftbook/decor/1 1/1 1/1/0/-1/The Longest Year Craftbook/1/{tex}";
                    data[BookKit.BundleLogId] = $"BundleLog/decor/1 1/1 1/1/0/-1/The Longest Year Bundle Log/2/{tex}";
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
