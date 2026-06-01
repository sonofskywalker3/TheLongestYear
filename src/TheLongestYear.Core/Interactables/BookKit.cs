namespace TheLongestYear.Core.Interactables
{
    /// <summary>Identity + reconcile rules for the three carried "book" furniture items. The
    /// per-loop invariant is "exactly ONE copy of each exists" — counting both the inventory and
    /// books placed in the world, so deliberately-placed books are left where they are. A copy is
    /// granted only when none exists (e.g. after a loop reset); duplicates are trimmed to one.</summary>
    public static class BookKit
    {
        public const string CookbookId  = "sonofskywalker3.TheLongestYear_Cookbook";
        public const string CraftbookId = "sonofskywalker3.TheLongestYear_Craftbook";
        public const string BundleLogId = "sonofskywalker3.TheLongestYear_BundleLog";

        public static readonly string[] AllBookQualifiedIds = { CookbookId, CraftbookId, BundleLogId };

        /// <summary>How many to add so the player holds exactly one, given how many they already
        /// hold AFTER any extras/placed copies have been removed.</summary>
        public static int GrantCountToReachOne(int heldCount) => heldCount >= 1 ? 0 : 1;
    }
}
