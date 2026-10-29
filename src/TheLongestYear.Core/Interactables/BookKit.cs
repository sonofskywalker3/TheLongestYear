namespace TheLongestYear.Core.Interactables
{
    /// <summary>Identity of the carried "book" furniture items. Two are handed out: the Bundle Log,
    /// kept at exactly ONE copy (counting the inventory and books placed in the world, so a
    /// deliberately placed book is left where it is), and the combined book (<see cref="OneBook"/>),
    /// whose Cookbook, Craftbook and Herd Book tabs replaced the three separate books on
    /// 2026-10-28. Those three ids are retired but stay registered (<see cref="OneBook.RetiredIds"/>).</summary>
    public static class BookKit
    {
        /// <summary>Retired: now the Cookbook tab of the combined book.</summary>
        public const string CookbookId  = "sonofskywalker3.TheLongestYear_Cookbook";
        /// <summary>Retired: now the Craftbook tab of the combined book.</summary>
        public const string CraftbookId = "sonofskywalker3.TheLongestYear_Craftbook";
        public const string BundleLogId = "sonofskywalker3.TheLongestYear_BundleLog";
        /// <summary>Retired: now the Herd Book tab of the combined book (spec 2026-09-25).</summary>
        public const string HerdBookId  = "sonofskywalker3.TheLongestYear_HerdBook";

        /// <summary>How many to add so the player holds exactly one, given how many they already
        /// hold AFTER any extras/placed copies have been removed.</summary>
        public static int GrantCountToReachOne(int heldCount) => heldCount >= 1 ? 0 : 1;
    }
}
