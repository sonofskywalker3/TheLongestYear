using System.Collections.Generic;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The season goals read aloud for a screen reader, when the log is its own menu. Shown
    /// inside the game menu's Collections page it is not the menu taking input, so the narrator
    /// never asks it.</summary>
    internal sealed partial class SeasonGoalsMenu : INarratedMenu
    {
        public string NarrationIntro()
            => NarrationText.Sentences(TitleText(), Strings.Get("narrate.goals.help"));

        /// <summary>The snapped component and the scroll position: scrolling changes what the same
        /// row slot, and the scroll buttons, say.</summary>
        public object NarrationFocusKey => (currentlySnappedComponent?.myID ?? -1, _scrollIndex);

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;

            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                int index = _scrollIndex + (id - RowIdBase);
                if (index >= _entries.Count) return "";
                BundleEntry e = _entries[index];
                return NarrationText.Sentences(
                    Headline(e),
                    BadgeFor(e),
                    e.IsVault ? VaultHoverText(e) : MissingLine(e),
                    NarrationParts.Position(index, _entries.Count));
            }
            if (id == ScrollUpId) return NarrationParts.Scroll(true, _scrollIndex, _rowsPerPage, _entries.Count);
            if (id == ScrollDownId) return NarrationParts.Scroll(false, _scrollIndex, _rowsPerPage, _entries.Count);
            if (focus == upperRightCloseButton) return Strings.Get("narrate.shrine.close");
            return "";
        }

        /// <summary>The row's icons as words: every ingredient this season's checkpoint still
        /// needs, the ones past the "+N more" note included.</summary>
        private string MissingLine(BundleEntry e)
        {
            if (e.IsMet || e.MissingItems.Count == 0) return null;
            var items = new List<string>();
            for (int k = 0; k < e.MissingItems.Count; k++)
                items.Add(MissingItemText(e, k));
            return Strings.Get("narrate.goals.missing", new Dictionary<string, string> { ["items"] = string.Join(", ", items) });
        }
    }
}
