using System.Collections.Generic;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The upgrade shop read aloud for a screen reader: the JP, the pinned budget and
    /// whichever tab, row or button the snap is on.</summary>
    internal sealed partial class JunimoShrineMenu : INarratedMenu
    {
        public string NarrationIntro()
        {
            var parts = new List<string> { Strings.Get("menu.shrine.title"), JpLine() };
            parts.AddRange(PinnedBudget.Lines(
                PinnedBudget.Compute(_store.State, _purchases.PriceFactor, RunReachEvaluator.Meets)));
            parts.Add(Strings.Get("narrate.shrine.help"));
            return NarrationText.Sentences(parts);
        }

        /// <summary>The spoken text itself: a purchase, a pin, a scroll or a tab change all alter
        /// what the same slot says, and the rows are already rebuilt every frame for drawing.</summary>
        public object NarrationFocusKey => NarrateFocus();

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;

            if (id >= TabIdBase && id < TabIdBase + CategoryCount)
            {
                var category = (UpgradeCategory)(id - TabIdBase);
                string name = ThemeDisplay.CategoryName(category);
                string count = TabCount(category);
                return category == _activeCategory
                    ? Strings.Get("narrate.shrine.tab-open", new Dictionary<string, string> { ["name"] = name, ["count"] = count })
                    : Strings.Get("narrate.shrine.tab", new Dictionary<string, string> { ["name"] = name, ["count"] = count });
            }

            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                IReadOnlyList<ShrineRow> rows = VisibleRows(out int total, out int start);
                int slot = id - RowIdBase;
                if (slot >= rows.Count)
                    return total == 0 ? EmptyTabText() : "";
                ShrineRow row = rows[slot];
                return NarrationText.Sentences(
                    row.IsFocus ? Strings.Get("focus.row.your-focus") : null,
                    RowHoverText(row),
                    (row.IsFocus || row.Buyable) && !row.IsBanish && !IsAffordableNow(row)
                        ? Strings.Get("menu.shrine.insufficient")
                        : null,
                    IsSuggested(row) ? LoopRecapText.SuggestedHeader() : null,
                    Strings.Get("narrate.shrine.row", new Dictionary<string, string>
                    {
                        ["position"] = (start + slot + 1).ToString(),
                        ["total"] = total.ToString(),
                    }),
                    JpLine());
            }

            if (id == AffordableToggleId)
            {
                string name = Strings.Get("menu.shrine.affordable-only");
                return _affordableOnly
                    ? Strings.Get("narrate.on", new Dictionary<string, string> { ["name"] = name })
                    : Strings.Get("narrate.off", new Dictionary<string, string> { ["name"] = name });
            }
            if (id == ScrollUpId) return Strings.Get("narrate.shrine.scroll-up");
            if (id == ScrollDownId) return Strings.Get("narrate.shrine.scroll-down");
            if (focus == upperRightCloseButton) return Strings.Get("narrate.shrine.close");
            return "";
        }

        private string JpLine()
            => Strings.Get("menu.shrine.jp", new Dictionary<string, string> { ["jp"] = _store.State.JunimoPoints.ToString() });

        private string EmptyTabText()
            => !_affordableOnly && ThemeDisplay.ShowsEmptyNote(_activeCategory, 0)
                ? Strings.Get("menu.shrine.gifts.empty")
                : Strings.Get("narrate.shrine.empty");
    }
}
