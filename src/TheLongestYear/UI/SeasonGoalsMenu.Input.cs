using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    internal sealed partial class SeasonGoalsMenu
    {
        // ---------- input ----------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            int visibleCount = Math.Min(_rowsPerPage, Math.Max(0, _entries.Count - _scrollIndex));
            for (int i = 0; i < visibleCount; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                BundleEntry e = _entries[_scrollIndex + i];

                // Vault row: no item ingredients, so surface the "how do I satisfy this" hint here
                // (the old pinned banner spelled it out inline; on a row it lives on hover like the
                // bundle rows' item tooltips).
                if (e.IsVault)
                {
                    _hoverText = e.IsMet
                        ? Strings.Get("menu.goals.vault-hover-met")
                        : Strings.Get("menu.goals.vault-hover-unmet");
                    return;
                }

                // Hover over an ingredient icon → show its name + a "missing" tag.
                int iconRowX = _rowSlots[i].bounds.X + 16;
                int iconRowY = _rowSlots[i].bounds.Y + IngredientIconY;
                for (int k = 0; k < e.MissingItems.Count; k++)
                {
                    var iconRect = new Rectangle(
                        iconRowX + k * (IngredientIconSize + IngredientIconGap),
                        iconRowY, IngredientIconSize, IngredientIconSize);
                    if (iconRect.Contains(x, y))
                    {
                        string itemId = e.MissingItems[k];
                        int stack = StackFor(e.Bundle, itemId);
                        int quality = e.Bundle.IngredientQualities.TryGetValue(itemId, out int q) ? q : 0;
                        Item probe = ResolveItem(itemId, stack, quality);
                        string qty = stack > 1
                            ? Strings.Get("menu.goals.qty-suffix", new Dictionary<string, string> { ["count"] = stack.ToString() })
                            : "";
                        string qStr = QualityTags.For(quality);
                        _hoverText = (probe?.DisplayName ?? itemId) + qty + qStr;
                        return;
                    }
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
            }
            base.receiveGamePadButton(b);
        }

        private void Scroll(int delta)
        {
            int before = _scrollIndex;
            _scrollIndex += delta;
            ClampScroll();
            if (_scrollIndex != before)
                Game1.playSound("shwip");
        }

        private void ClampScroll()
        {
            int maxStart = Math.Max(0, _entries.Count - _rowsPerPage);
            if (_scrollIndex < 0) _scrollIndex = 0;
            if (_scrollIndex > maxStart) _scrollIndex = maxStart;
        }
    }
}
