using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Detects the separate **Cart Catalog** mod (mail-order from the Traveling Cart). When it's
    /// merely **installed**, Cart Whisperer extends to **every day** (you can mail-order any day),
    /// not just cart-visit days. Ownership of the book is intentionally NOT required — seeing the
    /// bundle items you're missing on off-days nudges the player to buy the book next time the cart
    /// is in town. Degrades to off when the mod isn't installed.
    ///
    /// <see cref="ModLoaded"/> is set once at game launch from <c>Helper.ModRegistry.IsLoaded</c>.
    /// </summary>
    internal static class CartCatalogIntegration
    {
        /// <summary>Planned unique id of the standalone Cart Catalog mod (see workspace CartCatalog/).</summary>
        public const string ModId = "sonofskywalker3.CartCatalog";

        /// <summary>Set at game launch from the SMAPI mod registry.</summary>
        public static bool ModLoaded;

        /// <summary>True when the every-day Cart Whisperer mode applies (mod installed). The
        /// <paramref name="player"/> is unused by design — installation alone enables it.</summary>
        public static bool Available(Farmer player) => ModLoaded;
    }
}
