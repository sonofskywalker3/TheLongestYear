using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The banish rows at the top of the Foresight tab (Nexus request, RosemaryBagels
    /// 2026-10-27): a "Banish" buy row while fewer than <see cref="BanishLadder.MaxPerLoop"/> are
    /// bought, then one row per banish bought for the next board, which takes it back on click (the
    /// loop has not reset yet, so nothing is lost). Buying opens <see cref="BanishPickMenu"/> as a
    /// child of this menu. Offered only where the next board is the mod's own and is not being held
    /// tonight (<see cref="BanishRules.IsOfferable"/>). Host only.</summary>
    internal sealed partial class JunimoShrineMenu
    {
        private const UpgradeCategory BanishTab = UpgradeCategory.Foresight;

        /// <summary>Show the banish rows. Only the rewind shrine does, like the focus rows: at the win
        /// shrine, a banish bought before "Keep playing" would hold its JP for the rest of the run.</summary>
        private readonly bool _banishRows;

        private IEnumerable<ShrineRow> BanishRows()
        {
            if (!_banishRows || _activeCategory != BanishTab || !Context.IsMainPlayer || !BanishRules.IsOfferable(_store.State))
                yield break;
            List<BanishPick> pending = _store.State.PendingBanishes;
            for (int i = 0; i < pending.Count; i++)
                yield return new ShrineRow(i);
            if (pending.Count < BanishLadder.MaxPerLoop)
                yield return new ShrineRow(ShrineRow.BanishBuySlot);
        }

        private string BanishHover(ShrineRow row)
        {
            if (row.BanishSlot >= 0)
            {
                BanishPick pick = _store.State.PendingBanishes[row.BanishSlot];
                return Strings.Get("menu.shrine.hover", new Dictionary<string, string>
                {
                    ["name"] = Strings.Get("banish.row.pending", new Dictionary<string, string> { ["name"] = pick.Label }),
                    ["description"] = Strings.Get("banish.row.pending-desc"),
                    ["footer"] = Strings.Get("banish.row.pending-status", new Dictionary<string, string> { ["jp"] = pick.Paid.ToString() }),
                });
            }
            return Strings.Get("menu.shrine.hover", new Dictionary<string, string>
            {
                ["name"] = Strings.Get("banish.row.name"),
                ["description"] = Strings.Get("banish.row.desc"),
                ["footer"] = Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = _purchases.BanishCost().ToString() }),
            });
        }

        private void ClickBanish(ShrineRow row)
        {
            if (row.BanishSlot >= 0)
            {
                _purchases.CancelBanish(row.BanishSlot);
                ClampScroll();
                return;
            }
            long cost = _purchases.BanishCost();
            if (_store.State.JunimoPoints < cost)
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage(Strings.Get("focus.cant-afford",
                    new Dictionary<string, string> { ["jp"] = cost.ToString() })));
                return;
            }
            OpenBanishPick();
        }

        /// <summary>Opens the pick over this menu. Lists THIS loop's board, read live.</summary>
        internal BanishPickMenu OpenBanishPick()
        {
            IReadOnlyDictionary<string, string> board = Game1.netWorldState.Value.BundleData;
            // A bundle that is the only candidate at a position is on every board, so it is not offered.
            IReadOnlySet<string> sole = BanishCandidates.SoleCandidates(TheLongestYear.Loop.BundleEngine.CandidateNames(_monitor));
            var menu = new BanishPickMenu(
                BanishCandidates.Bundles(board, sole), BanishCandidates.Items(board),
                _store.State.PendingBanishes,
                (kind, key, label) => _purchases.TryBanish(kind, key, label) == BanishRules.BuyResult.Bought,
                () => _purchases.BanishCost(),
                () => _store.State.JunimoPoints);
            SetChildMenu(menu);
            Game1.playSound("bigSelect");
            _monitor.Log("Upgrade menu: opened the banish pick.", LogLevel.Trace);
            return menu;
        }

        private void DrawBanishRow(SpriteBatch b, ClickableComponent slot, ShrineRow row)
        {
            if (row.BanishSlot >= 0)
            {
                BanishPick pick = _store.State.PendingBanishes[row.BanishSlot];
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, Color.White, 1f, false);
                Utility.drawTextWithShadow(b,
                    Strings.Get("banish.row.pending", new Dictionary<string, string> { ["name"] = pick.Label }),
                    Game1.dialogueFont, new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), OwnedGreen);
                Utility.drawTextWithShadow(b,
                    Strings.Get("banish.row.pending-status", new Dictionary<string, string> { ["jp"] = pick.Paid.ToString() }),
                    Game1.smallFont, new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56), OwnedGreen);
                return;
            }

            long cost = _purchases.BanishCost();
            bool affordable = _store.State.JunimoPoints >= cost;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height,
                affordable ? Color.White : Color.White * 0.55f, 1f, false);
            Utility.drawTextWithShadow(b, Strings.Get("banish.row.name"), Game1.dialogueFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), Game1.textColor);
            string tag = Strings.Get("banish.row.limit",
                new Dictionary<string, string> { ["count"] = BanishLadder.MaxPerLoop.ToString() });
            Vector2 tagSize = Game1.smallFont.MeasureString(tag);
            Utility.drawTextWithShadow(b, tag, Game1.smallFont,
                new Vector2(slot.bounds.Right - 16 - tagSize.X, slot.bounds.Y + 16), Game1.textColor * 0.7f);
            string status = Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = cost.ToString() })
                + (!affordable ? Strings.Get("menu.shrine.insufficient") : "");
            Utility.drawTextWithShadow(b, status, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56), affordable ? Game1.textColor : Color.Red);
        }
    }
}
