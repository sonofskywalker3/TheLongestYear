using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Interactables
{
    /// <summary>A section of the one book (tanky24u, Nexus, 2026-10-28): the Cookbook, the
    /// Craftbook and the Herd Book used to be three carried items and are now tabs of one.</summary>
    public enum BookTab
    {
        Cookbook,
        Craftbook,
        HerdBook,
    }

    /// <summary>Where one copy of a book item sits in the world.</summary>
    public enum BookSpot
    {
        /// <summary>Placed furniture, on the floor or on a table.</summary>
        Placed,
        /// <summary>In a chest or fridge.</summary>
        Stored,
        /// <summary>In the player's inventory.</summary>
        Carried,
    }

    /// <summary>One copy found by the reconcile sweep: the combined book, or one of the three
    /// retired books (<see cref="OneBook.RetiredIds"/>).</summary>
    public readonly record struct BookCopy(bool IsRetired, BookSpot Spot);

    /// <summary>What the reconcile does: remove the copies at <see cref="Remove"/> (indexes into the
    /// copy list), put the combined book where the retired copy at <see cref="ReplaceAt"/> was,
    /// and grant one to the inventory when <see cref="Grant"/>. ReplaceAt is never in Remove.</summary>
    public sealed record BookReconcilePlan(IReadOnlyList<int> Remove, int? ReplaceAt, bool Grant)
    {
        public bool IsNoOp => Remove.Count == 0 && ReplaceAt == null && !Grant;
    }

    /// <summary>Pure rules for the combined book: which tabs a save gets, and the count-exact
    /// reconcile (exactly one book when any section is owned, none of the retired three).</summary>
    public static class OneBook
    {
        /// <summary>The combined book's furniture id.</summary>
        public const string BookId = "sonofskywalker3.TheLongestYear_Book";

        /// <summary>The three books the combined one replaced. Their Data/Furniture rows stay so a
        /// copy the sweep cannot reach is never an Error Item, and still opens its tab.</summary>
        public static readonly string[] RetiredIds = { BookKit.CookbookId, BookKit.CraftbookId, BookKit.HerdBookId };

        public static bool IsRetired(string itemId) => System.Array.IndexOf(RetiredIds, itemId) >= 0;

        /// <summary>The tab a retired book stands for.</summary>
        public static BookTab? TabForRetired(string itemId) => itemId switch
        {
            BookKit.CookbookId => BookTab.Cookbook,
            BookKit.CraftbookId => BookTab.Craftbook,
            BookKit.HerdBookId => BookTab.HerdBook,
            _ => null,
        };

        /// <summary>A section is owned when it gives the save at least one slot (the free slots
        /// count: every book starts with some, Jeff 2026-09-16) or still holds something.</summary>
        public static bool Owns(int slots, int held) => slots > 0 || held > 0;

        /// <summary>The tabs to show, in book order, for whether each section is owned.</summary>
        public static IReadOnlyList<BookTab> Tabs(bool cookbook, bool craftbook, bool herdBook)
        {
            var tabs = new List<BookTab>();
            if (cookbook) tabs.Add(BookTab.Cookbook);
            if (craftbook) tabs.Add(BookTab.Craftbook);
            if (herdBook) tabs.Add(BookTab.HerdBook);
            return tabs;
        }

        /// <summary>The tabs <paramref name="meta"/> owns.</summary>
        public static IReadOnlyList<BookTab> TabsFor(MetaState meta)
        {
            int cookSlots = UpgradeCatalog.CookbookSlotCount(meta.HighestKeptTier("cookbook_", UpgradeCatalog.BookMaxTier));
            int craftSlots = UpgradeCatalog.CraftbookSlotCount(meta.HighestKeptTier("craftbook_", UpgradeCatalog.BookMaxTier));
            int herdSlots = HerdBookRules.SlotsFor(meta).Count;
            return Tabs(
                Owns(cookSlots, meta.CookbookRecipes.Count),
                Owns(craftSlots, meta.CraftbookRecipes.Count),
                Owns(herdSlots, meta.HerdBook.Count));
        }

        /// <summary>The tab to open: <paramref name="wanted"/> when shown, else the first tab,
        /// else null (no tabs: the book is not owned).</summary>
        public static BookTab? Resolve(IReadOnlyList<BookTab> tabs, BookTab wanted)
        {
            if (tabs.Count == 0) return null;
            return tabs.Contains(wanted) ? wanted : tabs[0];
        }

        /// <summary>The tab <paramref name="step"/> places from <paramref name="current"/>,
        /// wrapping at both ends (shoulder buttons).</summary>
        public static BookTab Step(IReadOnlyList<BookTab> tabs, BookTab current, int step)
        {
            if (tabs.Count == 0) return current;
            int at = Math.Max(0, IndexOf(tabs, current));
            int next = ((at + step) % tabs.Count + tabs.Count) % tabs.Count;
            return tabs[next];
        }

        private static int IndexOf(IReadOnlyList<BookTab> tabs, BookTab tab)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i] == tab) return i;
            return -1;
        }

        /// <summary>Keep order for copies of one item: a placed copy stays where the player set
        /// it, then one in a chest, then one in the bag.</summary>
        private static int KeepRank(BookSpot spot) => spot switch
        {
            BookSpot.Placed => 0,
            BookSpot.Stored => 1,
            _ => 2,
        };

        /// <summary>The count-exact reconcile, mirroring the Circle of Warding's: when
        /// <paramref name="owned"/>, exactly one combined book exists afterwards, otherwise none.
        /// Every retired book goes. When no combined book exists yet, it takes the place of the
        /// best-placed retired copy (placed, then stored, then carried), so a save's old Cookbook
        /// on a shelf becomes the new book on the same shelf; with no copy at all, one is granted.</summary>
        public static BookReconcilePlan Plan(IReadOnlyList<BookCopy> copies, bool owned)
        {
            var remove = new List<int>();
            int? keep = null;
            int? replaceAt = null;

            if (owned)
                keep = Best(copies, retired: false);

            if (owned && keep == null)
                replaceAt = Best(copies, retired: true);

            for (int i = 0; i < copies.Count; i++)
            {
                if (i == keep || i == replaceAt) continue;
                remove.Add(i);
            }
            bool grant = owned && keep == null && replaceAt == null;
            return new BookReconcilePlan(remove, replaceAt, grant);
        }

        private static int? Best(IReadOnlyList<BookCopy> copies, bool retired)
        {
            int? best = null;
            for (int i = 0; i < copies.Count; i++)
            {
                if (copies[i].IsRetired != retired) continue;
                if (best == null || KeepRank(copies[i].Spot) < KeepRank(copies[best.Value].Spot))
                    best = i;
            }
            return best;
        }
    }
}
