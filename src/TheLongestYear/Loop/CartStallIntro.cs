using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// One-time in-fiction explanation for the Cart Stall cap (<see cref="CartSlotLimitPatch"/>):
    /// the first time the player opens the Traveling Cart on a TLY save while the cap is holding
    /// the stock to a single item, swap in a short merchant dialogue, then hand the shop menu
    /// back once it's dismissed. The "seen" flag lives in <c>MetaState.DismissedIndicators</c>
    /// ("tly.cart-intro") so it shows once per meta-profile, not once per loop.
    ///
    /// Skipped entirely when the cap is disabled in config, when a Cart Stall tier is already
    /// owned (nothing to explain), or when the cart happened to have a single item anyway.
    /// </summary>
    internal sealed class CartStallIntro
    {
        internal const string DismissKey = "tly.cart-intro";

        private readonly IMonitor _monitor;
        private readonly Func<MetaState> _meta;
        private readonly Func<GameplayConfig> _config;

        public CartStallIntro(IModHelper helper, IMonitor monitor, Func<MetaState> meta, Func<GameplayConfig> config)
        {
            _monitor = monitor;
            _meta = meta;
            _config = config;
            helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is not ShopMenu shop || shop.ShopId != CartSlotLimitPatch.TravelerShopId) return;
            if (!RunActivation.IsActive) return;
            if (!CartSlotLimitPatch.Enabled || _config()?.LimitTravelingCartStock != true) return;
            var meta = _meta();
            if (meta == null || meta.DismissedIndicators.Contains(DismissKey)) return;
            if (UpgradeChecker.GetTier("cart_slot", CartSlotRules.MaxSlots) > 0) return; // already unlocked stalls
            if (shop.forSale == null || shop.forSale.Count != CartSlotRules.MinSlots) return; // cap isn't what they're seeing

            meta.DismissedIndicators.Add(DismissKey);
            // Show the line on top of the freshly-opened shop, then restore the shop when the
            // player dismisses it — vanilla's own pattern for "say something, then open a menu".
            Game1.afterDialogues = () => Game1.activeClickableMenu = shop;
            Game1.activeClickableMenu = new DialogueBox(Strings.Get("dialog.cart.first-visit"));
            _monitor.Log("Cart Stall intro shown (first Traveling Cart visit on this profile).", LogLevel.Trace);
        }
    }
}
