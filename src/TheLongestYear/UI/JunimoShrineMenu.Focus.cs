using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Focus;

namespace TheLongestYear.UI
{
    /// <summary>The "your focus" rows: each chosen, unbought focus card that can still be bought
    /// shows at the top of the tab that holds its target upgrade, at the card's live deal (same
    /// rung, same price, the unkept lower rungs included). Run-reach does not apply, and the plain
    /// catalog row for the same upgrade is left out so it is not listed twice. Buying one keeps the
    /// upgrade only: the loop is ending, so nothing is handed over in the world. Host only.</summary>
    internal sealed partial class JunimoShrineMenu
    {
        private static readonly Color FocusTagColor = new(40, 90, 160);

        private readonly FocusService _focus;

        /// <summary>The focus rows' cards, quoted when the menu opens and again after every purchase.
        /// Quoting a tool card scans every chest, so it is not done per frame.</summary>
        private List<(FocusHand Hand, FocusQuote Quote)> _focusCards = new();

        private void RefreshFocusCards()
        {
            _focusCards = new List<(FocusHand, FocusQuote)>();
            if (_focus == null || !Context.IsMainPlayer) return;
            foreach ((FocusHand hand, FocusQuote quote) in _focus.ChosenCards())
            {
                if (hand.Bought || quote.UpgradeId == null) continue;
                if (quote.Kind is not (FocusQuoteKind.Offer or FocusQuoteKind.KeepOnly)) continue;
                _focusCards.Add((hand, quote));
            }
        }

        /// <summary>The focus rows for the active tab, and the upgrade ids they stand in for.</summary>
        private List<ShrineRow> FocusRows(out HashSet<string> focusIds)
        {
            var rows = new List<ShrineRow>();
            focusIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach ((FocusHand hand, FocusQuote quote) in _focusCards)
            {
                UpgradeDefinition def = UpgradeCatalog.TryGet(quote.UpgradeId);
                if (def == null || def.Category != _activeCategory) continue;
                rows.Add(new ShrineRow(def, hand.Theme, quote));
                focusIds.Add(def.Id);
            }
            return rows;
        }

        private void BuyFocus(ShrineRow row)
        {
            FocusTheme theme = row.FocusTheme!.Value;
            if (_store.State.JunimoPoints < row.Quote.Price)
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage(Strings.Get("focus.cant-afford",
                    new Dictionary<string, string> { ["jp"] = row.Quote.Price.ToString() })));
                return;
            }
            FocusBuyResult result = _focus.BuyKeepOnly(theme);
            _monitor.Log($"Upgrade menu: your-focus row '{row.Quote.ChainId}' ({row.Def.Id}) bought as keep only: {result}.", LogLevel.Info);
        }

        private string FocusFooter(ShrineRow row)
            => Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = row.Quote.Price.ToString() });

        private void DrawFocusRow(SpriteBatch b, ClickableComponent slot, ShrineRow row)
        {
            long cost = row.Quote.Price;
            bool affordable = _store.State.JunimoPoints >= cost;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height,
                affordable ? Color.White : Color.White * 0.55f, 1f, false);

            Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.dialogueFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), Game1.textColor);

            string tag = Strings.Get("focus.row.your-focus");
            Vector2 tagSize = Game1.smallFont.MeasureString(tag);
            Utility.drawTextWithShadow(b, tag, Game1.smallFont,
                new Vector2(slot.bounds.Right - 16 - tagSize.X, slot.bounds.Y + 16), FocusTagColor);

            string statusLine = FocusFooter(row) + (!affordable ? Strings.Get("menu.shrine.insufficient") : "");
            Utility.drawTextWithShadow(b, statusLine, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56),
                affordable ? Game1.textColor : Color.Red);
        }
    }
}
