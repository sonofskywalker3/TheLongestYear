using System.Collections.Generic;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The Cookbook and Craftbook pages read aloud for a screen reader, through the book
    /// that hosts them (<see cref="OneBookMenu"/>).</summary>
    internal abstract partial class RecipeBookMenu
    {
        /// <summary>The title drawn now: the slot count, or the picker's prompt.</summary>
        private string CurrentTitle()
            => _pickerList != null ? Strings.Get("menu.books.choose") : TitleText(Banked.Count, _slotCount);

        /// <summary>A slot row's label: the banked recipe, or the empty slot's prompt.</summary>
        private string SlotLabel(int slotIndex)
            => slotIndex < Banked.Count
                ? RecipeDisplayName(Banked[slotIndex], _isCooking)
                : Strings.Get("menu.books.empty-slot");

        public string NarrationHeader()
            => NarrationText.Sentences(
                CurrentTitle(),
                Notice,
                _pickerList != null ? Strings.Get("narrate.book.picker-help") : null);

        public string NarrationSection()
            => HasSourceTabs
                ? Strings.Get("narrate.book.source", new Dictionary<string, string>
                {
                    ["name"] = _sourceTabs[_sourceTab].Label,
                    ["position"] = (_sourceTab + 1).ToString(),
                    ["total"] = _sourceTabs.Count.ToString(),
                })
                : null;

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;
            bool picking = _pickerList != null;
            int total = picking ? _pickerList.Count : RowCount;
            int start = picking ? _pickerScroll : _scroll;

            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                int index = start + (id - RowIdBase);
                if (index >= total) return "";
                return NarrationText.Sentences(
                    picking ? RecipeDisplayName(_pickerList[index], _isCooking) : SlotLabel(index),
                    NarrationParts.Position(index, total));
            }
            if (id == ScrollUpId) return NarrationParts.Scroll(true, start, _rowsPerPage, total);
            if (id == ScrollDownId) return NarrationParts.Scroll(false, start, _rowsPerPage, total);
            if (focus == upperRightCloseButton)
                return picking ? Strings.Get("narrate.book.back") : Strings.Get("narrate.shrine.close");
            return "";
        }
    }
}
