using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The band above the rows (spec 2026-11-10, sections 4 and 8): the pinned budget line
    /// on the left and the "Affordable only" toggle on the right. The toggle hides every row that
    /// cannot be bought right now (pins included; the budget line still tells about them), before
    /// the rows are paged; banish rows always stay. Mouse: click it. Controller: left trigger, or A
    /// on the toggle. Also the tab counts and the tier ladder in the row tooltips.</summary>
    internal sealed partial class JunimoShrineMenu
    {
        /// <summary>Height of the band between the top of the tab strip and the first row.</summary>
        private const int HeaderBandHeight = 72;
        private const int AffordableToggleId = 7902;
        /// <summary>OptionsCheckbox's boxes in Cursors.png (9x9), drawn at 4x.</summary>
        private static readonly Rectangle CheckboxUnchecked = new(227, 425, 9, 9);
        private static readonly Rectangle CheckboxChecked = new(236, 425, 9, 9);
        private const float CheckboxScale = 4f;
        private const int CheckboxSize = 36;
        private const int ToggleLabelGap = 8;
        private const int BandGap = 16;
        /// <summary>Smallest the budget text shrinks to when its lines outgrow the band.</summary>
        private const float MinBudgetScale = 0.7f;

        private ClickableComponent _affordableToggle;
        private bool _affordableOnly;

        /// <summary>Game tick of the last toggle: A on the toggle reaches the menu twice in one tick
        /// (the gamepad press and a synthesized click), which would flip it back.</summary>
        private int _lastFilterToggleTick = -1;

        private ClickableComponent BuildAffordableToggle(int listX, int listWidth, int bandY)
        {
            int labelWidth = (int)Game1.smallFont.MeasureString(Strings.Get("menu.shrine.affordable-only")).X;
            int toggleWidth = CheckboxSize + ToggleLabelGap + labelWidth;
            return new ClickableComponent(
                new Rectangle(listX + listWidth - toggleWidth, bandY + (HeaderBandHeight - CheckboxSize) / 2, toggleWidth, CheckboxSize),
                "affordable-only")
            {
                myID = AffordableToggleId,
                downNeighborID = RowIdBase,
                leftNeighborID = TabIdBase,
                rightNeighborID = -1,
                upNeighborID = -1,
            };
        }

        /// <summary>The rows the toggle leaves: everything when it is off; otherwise banish rows and
        /// the rows the JP buys right now, at the price the row shows.</summary>
        private List<ShrineRow> ApplyAffordableFilter(IReadOnlyList<ShrineRow> rows)
        {
            var kept = new List<ShrineRow>(rows.Count);
            foreach (ShrineRow row in rows)
                if (!_affordableOnly || row.IsBanish || IsAffordableNow(row))
                    kept.Add(row);
            return kept;
        }

        private bool IsAffordableNow(ShrineRow row)
        {
            if (row.IsFocus) return _store.State.JunimoPoints >= row.Quote.Price;
            return row.Buyable && _store.State.JunimoPoints >= _purchases.EffectiveCost(row.Def);
        }

        private static string RowIdentity(ShrineRow row)
            => row.IsBanish ? "banish:" + row.BanishSlot : (row.IsFocus ? "focus:" : "") + row.Def.Id;

        /// <summary>Flips the filter. A controller selection on a row stays on that row when it
        /// survives, else moves to the nearest row that does, else to the toggle.</summary>
        private void ToggleAffordableOnly()
        {
            if (Game1.ticks == _lastFilterToggleTick)
                return;
            _lastFilterToggleTick = Game1.ticks;

            IReadOnlyList<ShrineRow> before = VisibleCatalogForActiveCategory();
            int snappedSlot = currentlySnappedComponent == null ? -1 : currentlySnappedComponent.myID - RowIdBase;
            int oldIndex = snappedSlot >= 0 && snappedSlot < _rowsPerPage ? _scrollIndex + snappedSlot : -1;

            _affordableOnly = !_affordableOnly;
            Game1.playSound("drumkit6");
            _monitor.Log($"Upgrade menu: affordable only {(_affordableOnly ? "on" : "off")}.", LogLevel.Trace);

            IReadOnlyList<ShrineRow> after = VisibleCatalogForActiveCategory();
            ClampScroll();
            if (oldIndex < 0 || oldIndex >= before.Count)
                return;   // the selection was not on a row: leave it where it is

            int target = NearestSurvivor(before, after, oldIndex);
            if (target < 0)
            {
                currentlySnappedComponent = _affordableToggle;
                snapCursorToCurrentSnappedComponent();
                return;
            }
            if (target < _scrollIndex) _scrollIndex = target;
            else if (target >= _scrollIndex + _rowsPerPage) _scrollIndex = target - _rowsPerPage + 1;
            ClampScroll();
            currentlySnappedComponent = getComponentWithID(RowIdBase + target - _scrollIndex);
            snapCursorToCurrentSnappedComponent();
        }

        /// <summary>Index in <paramref name="after"/> of the row at <paramref name="oldIndex"/> in
        /// <paramref name="before"/>, or of the closest row to it that is still listed (the earlier
        /// one on a tie); -1 when nothing is left.</summary>
        private static int NearestSurvivor(IReadOnlyList<ShrineRow> before, IReadOnlyList<ShrineRow> after, int oldIndex)
        {
            var indexAfter = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < after.Count; i++)
                indexAfter[RowIdentity(after[i])] = i;
            for (int distance = 0; distance < before.Count; distance++)
                foreach (int candidate in new[] { oldIndex - distance, oldIndex + distance })
                    if (candidate >= 0 && candidate < before.Count
                        && indexAfter.TryGetValue(RowIdentity(before[candidate]), out int found))
                        return found;
            return -1;
        }

        /// <summary>The tier ladder after a keep's description, for a tiered keep (spec amendment I).</summary>
        private string LadderBlock(ShrineRow row)
            => row.IsFocus || row.IsBanish ? "" : KeepLadder.TooltipBlock(row.Def.Id, _store.State, _purchases.PriceFactor);

        private string TabCount(UpgradeCategory category)
            => KeepShopFilter.CategoryProgressText(category, _store.State);

        private void DrawHeaderBand(SpriteBatch b)
        {
            if (_affordableToggle == null || _rowSlots.Count == 0) return;
            Rectangle toggle = _affordableToggle.bounds;
            b.Draw(Game1.mouseCursors, new Vector2(toggle.X, toggle.Y), _affordableOnly ? CheckboxChecked : CheckboxUnchecked,
                Color.White, 0f, Vector2.Zero, CheckboxScale, SpriteEffects.None, 0.4f);
            string label = Strings.Get("menu.shrine.affordable-only");
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(toggle.X + CheckboxSize + ToggleLabelGap,
                    TextInk.CentredY(Game1.smallFont, label, toggle.Y, toggle.Height)),
                Game1.textColor);

            IReadOnlyList<string> lines = PinnedBudget.Lines(
                PinnedBudget.Compute(_store.State, _purchases.PriceFactor, RunReachEvaluator.Meets));
            if (lines.Count == 0) return;
            int left = _rowSlots[0].bounds.X;
            int bandTop = _rowSlots[0].bounds.Y - HeaderBandHeight;
            float room = toggle.X - BandGap - left;
            string text = Wrap(lines, room);
            float height = Game1.smallFont.MeasureString(text).Y;
            float scale = 1f;
            if (height > HeaderBandHeight)
            {
                scale = Math.Max(MinBudgetScale, HeaderBandHeight / height);
                text = Wrap(lines, room / scale);   // the smaller text fits more per line
                height = Game1.smallFont.MeasureString(text).Y * scale;
            }
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(left, bandTop + Math.Max(0, (HeaderBandHeight - height) / 2f)), Game1.textColor, scale);
        }

        private static string Wrap(IReadOnlyList<string> lines, float width)
        {
            var wrapped = new List<string>(lines.Count);
            foreach (string line in lines)
                wrapped.Add(Game1.parseText(line, Game1.smallFont, (int)width));
            return string.Join("\n", wrapped);
        }
    }
}
