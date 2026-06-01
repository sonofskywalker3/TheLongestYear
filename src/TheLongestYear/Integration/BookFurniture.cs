using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
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
                // Find every existing copy of this book: placed furniture in any location, plus
                // inventory slots. The invariant is "exactly ONE copy exists somewhere" — a book
                // the player placed in the world counts, so we must NOT sweep placed copies back
                // into the inventory on every load (that yanked deliberately-placed books and,
                // when the bag was full, silently destroyed the overflow).
                var placed = new System.Collections.Generic.List<(StardewValley.GameLocation loc, StardewValley.Objects.Furniture f)>();
                StardewValley.Utility.ForEachLocation(loc =>
                {
                    foreach (StardewValley.Objects.Furniture f in loc.furniture)
                        if (f.ItemId == id) placed.Add((loc, f));
                    return true;
                });
                var invSlots = new System.Collections.Generic.List<int>();
                for (int i = 0; i < p.Items.Count; i++)
                    if (p.Items[i] != null && p.Items[i].ItemId == id) invSlots.Add(i);

                int total = placed.Count + invSlots.Count;

                if (total == 0)
                {
                    // None exists (e.g. just after a loop reset wiped the farm + inventory): grant one.
                    GrantBookOrDrop(p, id);
                }
                else if (total > 1)
                {
                    // Duplicates: trim to exactly one. Prefer keeping a placed copy so a book the
                    // player deliberately set down stays put; remove all the rest.
                    bool keepPlaced = placed.Count > 0;
                    for (int i = keepPlaced ? 1 : 0; i < placed.Count; i++)
                        placed[i].loc.furniture.Remove(placed[i].f);
                    for (int i = keepPlaced ? 0 : 1; i < invSlots.Count; i++)
                        p.Items[invSlots[i]] = null;
                }
                // total == 1: exactly one exists (carried or placed) — leave it untouched.
            }

            _monitor.Log("BookFurniture: reconciled books to exactly one of each (placed copies left in place).", LogLevel.Info);
        }

        /// <summary>Grant one book to the inventory; if the bag is full, drop it at the player's
        /// feet so it can never be silently lost (the old addItemToInventory discarded overflow).</summary>
        private static void GrantBookOrDrop(StardewValley.Farmer p, string id)
        {
            StardewValley.Objects.Furniture book = StardewValley.Objects.Furniture.GetFurnitureInstance(id);
            if (!p.addItemToInventoryBool(book))
                StardewValley.Game1.createItemDebris(
                    book, new Vector2(p.StandingPixel.X, p.StandingPixel.Y), p.FacingDirection, p.currentLocation);
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

        /// <summary>Draw the carried books at full inventory-slot scale. Vanilla caps a 1x1
        /// furniture's menu icon at <c>getScaleSizeForMenu() == 2f</c> (a 16x16 sprite drawn at
        /// 32px in a 64px slot), so the books looked tiny in the toolbar. For our three book ids
        /// we render the sprite scaled to fill the slot like a normal item; everything else falls
        /// through to vanilla.</summary>
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.drawInMenu),
            new System.Type[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float),
                typeof(StardewValley.StackDrawType), typeof(Color), typeof(bool) })]
        internal static class DrawInMenuPatch
        {
            private static bool Prefix(StardewValley.Objects.Furniture __instance, SpriteBatch spriteBatch,
                Vector2 location, float scaleSize, float transparency, float layerDepth, Color color)
            {
                switch (__instance.ItemId)
                {
                    case BookKit.CookbookId:
                    case BookKit.CraftbookId:
                    case BookKit.BundleLogId:
                        break;
                    default:
                        return true;
                }

                StardewValley.ItemTypeDefinitions.ParsedItemData data =
                    StardewValley.ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
                Texture2D tex = data.GetTexture();
                Rectangle src = data.GetSourceRect();
                // Scale the larger sprite dimension to the 64px slot (16x16 book -> 4x), matching
                // how a normal Object icon fills the slot.
                float scale = (64f / System.Math.Max(src.Width, src.Height)) * scaleSize;
                spriteBatch.Draw(tex, location + new Vector2(32f, 32f), src, color * transparency, 0f,
                    new Vector2(src.Width / 2f, src.Height / 2f), scale, SpriteEffects.None, layerDepth);
                return false;
            }
        }
    }
}
