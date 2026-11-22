using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The banish pick read aloud for a screen reader: the price, the two tabs and the
    /// row the snap is on.</summary>
    internal sealed partial class BanishPickMenu : INarratedMenu
    {
        public string NarrationIntro()
            => NarrationText.Sentences(
                Strings.Get("banish.pick.title"),
                SubtitleText(out _),
                Strings.Get("narrate.banish.help"));

        /// <summary>The spoken text itself: a tab change or a scroll alters what the same slot says.</summary>
        public object NarrationFocusKey => NarrateFocus();

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;

            if (id >= TabIdBase && id < TabIdBase + _lists.Length)
            {
                int tab = id - TabIdBase;
                bool open = tab == _tab;
                return NarrationText.Sentences(
                    NarrationParts.Tab(TabLabel(tab), open),
                    open && Active.Count == 0 ? Strings.Get("banish.pick.empty") : null);
            }
            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                if (Active.Count == 0) return Strings.Get("banish.pick.empty");
                int index = _scroll + (id - RowIdBase);
                if (index >= Active.Count) return "";
                return NarrationText.Sentences(RowText(Active[index]), NarrationParts.Position(index, Active.Count));
            }
            if (id == ScrollUpId) return NarrationParts.Scroll(true, _scroll, _rowsPerPage, Active.Count);
            if (id == ScrollDownId) return NarrationParts.Scroll(false, _scroll, _rowsPerPage, Active.Count);
            if (focus == upperRightCloseButton) return Strings.Get("narrate.shrine.close");
            return "";
        }
    }
}
