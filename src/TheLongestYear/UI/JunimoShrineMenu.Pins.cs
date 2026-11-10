using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Pinned keeps (Nijah, Nexus posts 2026-11-10): Y or right-click on a keep row pins its
    /// chain, and pinned rows sit at the top of their tab in every state, above the focus and banish
    /// rows. The pinned row keeps the controller focus after the re-sort. Host only.</summary>
    internal sealed partial class JunimoShrineMenu
    {
        private const int StarInset = 12;

        private readonly KeepPinInput _pins = new();

        private IReadOnlyList<ShrineRow> PinnedFirst(List<ShrineRow> rows)
            => PinnedKeeps.PinnedFirst(rows, r => r.Def?.Id, _store.State);

        private bool IsPinned(ShrineRow row) => PinnedKeeps.IsPinned(_store.State, row.Def?.Id);

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            base.receiveRightClick(x, y, playSound);
            if (!KeepPinInput.IsFreshRightClick())
                return;
            IReadOnlyList<ShrineRow> rows = VisibleRows(out _, out _);
            for (int i = 0; i < rows.Count; i++)
                if (_rowSlots[i].containsPoint(x, y)) { TogglePin(rows[i], moveSnap: false); return; }
        }

        /// <summary>Controller Y on a snapped row. True when the press was used.</summary>
        private bool TryPinSnappedRow()
        {
            if (currentlySnappedComponent == null) return false;
            int slot = currentlySnappedComponent.myID - RowIdBase;
            if (slot < 0 || slot >= _rowsPerPage) return false;
            IReadOnlyList<ShrineRow> rows = VisibleRows(out _, out _);
            if (slot >= rows.Count) return false;
            TogglePin(rows[slot], moveSnap: true);
            return true;
        }

        /// <summary>Banish rows have no keep behind them, so they never pin.</summary>
        private void TogglePin(ShrineRow row, bool moveSnap)
        {
            if (row.Def == null || !_pins.TryToggle(_store.State, row.Def.Id))
                return;
            _monitor.Log($"Upgrade menu: {(IsPinned(row) ? "pinned" : "unpinned")} '{PinnedKeeps.ChainRoot(row.Def.Id)}'.", LogLevel.Trace);
            FollowRow(row.Def.Id, moveSnap);
        }

        /// <summary>After the re-sort, scroll so the toggled row is still on screen and, on a
        /// controller, keep the focus on it rather than on whatever slid into its old slot.</summary>
        private void FollowRow(string upgradeId, bool moveSnap)
        {
            IReadOnlyList<ShrineRow> all = VisibleCatalogForActiveCategory();
            int index = -1;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Def?.Id == upgradeId) { index = i; break; }
            if (index < 0) return;
            if (index < _scrollIndex) _scrollIndex = index;
            else if (index >= _scrollIndex + _rowsPerPage) _scrollIndex = index - _rowsPerPage + 1;
            ClampScroll();
            if (!moveSnap) return;
            currentlySnappedComponent = getComponentWithID(RowIdBase + index - _scrollIndex);
            snapCursorToCurrentSnappedComponent();
        }

        private string PinnedFooter(ShrineRow row)
            => IsPinned(row) ? "\n" + Strings.Get("menu.shrine.pinned") : "";

        private void DrawPinStar(SpriteBatch b, ClickableComponent slot, ShrineRow row)
        {
            if (!IsPinned(row)) return;
            int size = KeepPinBadge.SizeFor(KeepPinBadge.ShopScale);
            KeepPinBadge.Draw(b, new Vector2(slot.bounds.Right - StarInset - size, slot.bounds.Bottom - StarInset - size),
                KeepPinBadge.ShopScale);
        }

        /// <summary>"Y / Right-click: pin to top", left of the title when it fits there. Host only.</summary>
        private void DrawPinHint(SpriteBatch b)
        {
            if (!KeepPinInput.CanPin) return;
            string hint = Strings.Get("menu.shrine.pin-hint");
            float titleLeft = xPositionOnScreen + width / 2f
                              - SpriteText.getWidthOfString(Strings.Get("menu.shrine.title")) / 2f;
            float hintX = xPositionOnScreen + PanelPadding;
            if (hintX + Game1.smallFont.MeasureString(hint).X + 16 > titleLeft) return;
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(hintX, yPositionOnScreen + 32),
                Game1.textColor * 0.7f);
        }

        /// <summary>Controller Y pins; the game also sends it as the menu key, which must not close
        /// the shrine (<see cref="KeepPinInput.IsFreshPadY"/>).</summary>
        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            if (KeepPinInput.IsFreshPadY() && Game1.options.doesInputListContain(Game1.options.menuButton, key))
                return;
            base.receiveKeyPress(key);
        }
    }
}
