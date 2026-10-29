using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Integration
{
    /// <summary>Registers the carried "book" furniture as custom furniture: edits Data/Furniture to
    /// add the rows and provides the tilesheet, opens the matching menu when a book is used, and
    /// keeps the per-loop count exact. Two books are handed out: the Bundle Log and the combined
    /// book (<see cref="OneBook"/>: Cookbook, Craftbook and Herd Book as tabs, 2026-10-28). The
    /// three retired books keep their rows so a copy the sweep missed still works.</summary>
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

        /// <summary>Per-loop invariant: exactly one Bundle Log, and exactly one of the combined book
        /// (<see cref="OneBook"/>) when any of its sections is owned. The Cookbook, Craftbook and
        /// Herd Book items are retired: every copy is removed, and the first one found takes the
        /// combined book's place (see <see cref="ReconcileOneBook"/>).</summary>
        public void ReconcileInventory(TheLongestYear.Core.MetaState meta)
        {
            StardewValley.Farmer p = StardewValley.Game1.player;
            if (p == null) return;

            ReconcileOneBook(p, meta);

            foreach (string id in SingleCopyIds)
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

            _monitor.Log("BookFurniture: reconciled the Bundle Log to exactly one (placed copies left in place).", LogLevel.Info);
        }

        /// <summary>Books kept at exactly one copy by the loop above. The combined book has its own
        /// reconcile, which also reaches chests and tables.</summary>
        private static readonly string[] SingleCopyIds = { BookKit.BundleLogId };

        /// <summary>One copy of the combined book or a retired book, and how to swap or remove it.</summary>
        private readonly struct FoundCopy
        {
            public readonly BookCopy Copy;
            public readonly string Where;
            /// <summary>Put the given item where the copy was, or remove it with null.</summary>
            public readonly System.Action<StardewValley.Item> Set;
            public FoundCopy(BookCopy copy, string where, System.Action<StardewValley.Item> set)
            { Copy = copy; Where = where; Set = set; }
        }

        /// <summary>The count-exact reconcile of the one book, mirroring the Circle of Warding's
        /// (placed anywhere, on a table, carried, or in a chest or fridge all count). The rules
        /// are <see cref="OneBook.Plan"/>; this finds the copies and carries the plan out.</summary>
        private void ReconcileOneBook(StardewValley.Farmer p, TheLongestYear.Core.MetaState meta)
        {
            bool owned = meta != null && OneBook.TabsFor(meta).Count > 0;
            List<FoundCopy> found = FindBookCopies(p);
            BookReconcilePlan plan = OneBook.Plan(found.Select(f => f.Copy).ToList(), owned);
            if (plan.IsNoOp) return;

            if (plan.ReplaceAt is int at)
            {
                found[at].Set(StardewValley.Objects.Furniture.GetFurnitureInstance(OneBook.BookId));
                _monitor.Log($"Book: the combined book took the place of a retired book ({found[at].Where}).", LogLevel.Info);
            }
            foreach (int i in plan.Remove)
            {
                found[i].Set(null);
                _monitor.Log($"Book: removed {(found[i].Copy.IsRetired ? "a retired book" : "an extra book")} ({found[i].Where}).", LogLevel.Info);
            }
            if (plan.Grant)
            {
                GrantBookOrDrop(p, OneBook.BookId);
                _monitor.Log("Book: granted the book.", LogLevel.Info);
            }
        }

        /// <summary>Every copy of the combined book and the retired books, in the same places the
        /// reconcile looks (for <c>tly_herdbook</c>).</summary>
        internal static List<(BookCopy Copy, string Where)> ListBookCopies(StardewValley.Farmer p)
            => FindBookCopies(p).Select(f => (f.Copy, f.Where)).ToList();

        private static List<FoundCopy> FindBookCopies(StardewValley.Farmer p)
        {
            var found = new List<FoundCopy>();
            // A Junimo Chest shows the same shared inventory from every chest, so each inventory is
            // walked once, however many chests show it.
            var seenInventories = new HashSet<object>(ReferenceEqualityComparer.Instance);

            StardewValley.Utility.ForEachLocation(loc =>
            {
                string where = loc.NameOrUniqueName;
                foreach (StardewValley.Objects.Furniture f in loc.furniture.ToList())
                {
                    if (IsBook(f.ItemId))
                    {
                        StardewValley.Objects.Furniture placed = f;
                        StardewValley.GameLocation at = loc;
                        found.Add(new FoundCopy(new BookCopy(OneBook.IsRetired(f.ItemId), BookSpot.Placed),
                            $"placed in {where} at {f.TileLocation}",
                            item =>
                            {
                                at.furniture.Remove(placed);
                                if (item != null)
                                    at.furniture.Add(StardewValley.Objects.Furniture.GetFurnitureInstance(item.ItemId, placed.TileLocation));
                            }));
                    }
                    // A book set on a table is the table's held object, not part of loc.furniture.
                    if (f.heldObject.Value is StardewValley.Objects.Furniture held && IsBook(held.ItemId))
                    {
                        StardewValley.Objects.Furniture table = f;
                        StardewValley.GameLocation at2 = loc;
                        found.Add(new FoundCopy(new BookCopy(OneBook.IsRetired(held.ItemId), BookSpot.Placed),
                            $"on a table in {where} at {f.TileLocation}",
                            item =>
                            {
                                // The same fields vanilla sets when a book is dropped on a table
                                // (Furniture.performObjectDropInAction).
                                if (item is StardewValley.Object book)
                                {
                                    book.Location = at2;
                                    book.TileLocation = table.TileLocation;
                                    book.boundingBox.X = table.boundingBox.X;
                                    book.boundingBox.Y = table.boundingBox.Y;
                                }
                                table.heldObject.Value = item as StardewValley.Object;
                            }));
                    }
                }
                foreach (StardewValley.Object obj in loc.objects.Values)
                    if (obj is StardewValley.Objects.Chest chest)
                        AddStored(found, seenInventories, chest, $"in a chest in {where} at {chest.TileLocation}");
                if (loc is StardewValley.Locations.FarmHouse house && house.fridge.Value is { } fridge)
                    AddStored(found, seenInventories, fridge, $"in the fridge in {where}");
                if (loc is StardewValley.Locations.IslandFarmHouse islandHouse && islandHouse.fridge.Value is { } islandFridge)
                    AddStored(found, seenInventories, islandFridge, $"in the fridge in {where}");
                return true;
            });

            for (int i = 0; i < p.Items.Count; i++)
            {
                if (p.Items[i] == null || !IsBook(p.Items[i].ItemId)) continue;
                int slot = i;
                found.Add(new FoundCopy(new BookCopy(OneBook.IsRetired(p.Items[i].ItemId), BookSpot.Carried),
                    $"inventory slot {slot}", item => p.Items[slot] = item));
            }
            return found;
        }

        private static void AddStored(List<FoundCopy> found, HashSet<object> seenInventories,
            StardewValley.Objects.Chest chest, string where)
        {
            StardewValley.Inventories.IInventory items = chest.Items;
            if (items == null || !seenInventories.Add(items)) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || !IsBook(items[i].ItemId)) continue;
                int slot = i;
                found.Add(new FoundCopy(new BookCopy(OneBook.IsRetired(items[i].ItemId), BookSpot.Stored),
                    $"{where}, slot {slot}", item => items[slot] = item));
            }
        }

        private static bool IsBook(string itemId) => itemId == OneBook.BookId || OneBook.IsRetired(itemId);

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
                    // Same reason the display-name translations below must never contain '/'.
                    string tex = BookTextureAsset.Replace('/', '\\');
                    data[BookKit.CookbookId]  = $"Cookbook/decor/1 1/1 1/1/0/-1/{TheLongestYear.Core.Strings.Get("furniture.cookbook")}/0/{tex}";
                    data[BookKit.CraftbookId] = $"Craftbook/decor/1 1/1 1/1/0/-1/{TheLongestYear.Core.Strings.Get("furniture.craftbook")}/1/{tex}";
                    data[BookKit.BundleLogId] = $"BundleLog/decor/1 1/1 1/1/0/-1/{TheLongestYear.Core.Strings.Get("furniture.bundle-log")}/2/{tex}";
                    // Sprite index 3: the fourth 16x16 cover in assets/books.png (64x16 sheet).
                    data[BookKit.HerdBookId]  = $"HerdBook/decor/1 1/1 1/1/0/-1/{TheLongestYear.Core.Strings.Get("furniture.herdbook")}/3/{tex}";
                    // The combined book reuses the Cookbook's cover (sprite 0) until it has its own.
                    data[OneBook.BookId]      = $"Book/decor/1 1/1 1/1/0/-1/{TheLongestYear.Core.Strings.Get("furniture.book")}/0/{tex}";
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
                    // A retired book the sweep could not reach opens the one book on its own tab.
                    case BookKit.CookbookId:  launcher.OpenCookbook();    __result = true; return false;
                    case BookKit.CraftbookId: launcher.OpenCraftbook();   __result = true; return false;
                    case BookKit.BundleLogId: launcher.OpenSeasonGoals(); __result = true; return false;
                    case BookKit.HerdBookId:  launcher.OpenHerdBook();    __result = true; return false;
                    case OneBook.BookId:      launcher.OpenBook(BookTab.Cookbook); __result = true; return false;
                    default: return true;
                }
            }
        }

        /// <summary>Draw the carried books at full inventory-slot scale. Vanilla caps a 1x1
        /// furniture's menu icon at <c>getScaleSizeForMenu() == 2f</c> (a 16x16 sprite drawn at
        /// 32px in a 64px slot), so the books looked tiny in the toolbar. For our book ids
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
                    case BookKit.HerdBookId:
                    case OneBook.BookId:
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

        /// <summary>The combined book's tooltip. Data/Furniture has no description field, so the
        /// game would show its generic "Can be placed inside your house." (see
        /// TheLongestYear.Loop.CircleDescriptionPatch, which does the same for the circle).</summary>
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), "loadDescription")]
        internal static class DescriptionPatch
        {
            private static void Postfix(StardewValley.Objects.Furniture __instance, ref string __result)
            {
                if (__instance?.ItemId == OneBook.BookId)
                    __result = TheLongestYear.Core.Strings.Get("furniture.book.desc");
            }
        }
    }
}
