using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The book read aloud for a screen reader. The book is the menu taking input, so it
    /// speaks: its own tabs itself, everything else through the page being shown
    /// (<see cref="IBookPage"/>).</summary>
    internal sealed partial class OneBookMenu : INarratedMenu
    {
        /// <summary>The page header and section last spoken. A tab switch, the picker opening or
        /// closing, and a banked recipe change them without moving the snap, and the new one is
        /// said ahead of the focused row.</summary>
        private string _spokenHeader;
        private string _spokenSection;

        private IBookPage Page => (IBookPage)_page;

        public string NarrationIntro()
        {
            _spokenHeader = Page.NarrationHeader();
            _spokenSection = Page.NarrationSection();
            return NarrationText.Sentences(_spokenHeader, _spokenSection, Strings.Get("narrate.book.help"));
        }

        public object NarrationFocusKey => (Page.NarrationHeader(), Page.NarrationSection(), FocusText());

        public string NarrateFocus()
        {
            string header = Page.NarrationHeader();
            string section = Page.NarrationSection();
            bool newHeader = header != _spokenHeader;
            bool newSection = section != _spokenSection;
            _spokenHeader = header;
            _spokenSection = section;
            return NarrationText.Sentences(newHeader ? header : null, newSection ? section : null, FocusText());
        }

        private string FocusText()
        {
            ClickableComponent focus = _page.currentlySnappedComponent;
            if (focus == null) return "";
            if (!IsTabButton(focus)) return Page.NarrateFocus();
            int index = focus.myID - TabIdBase;
            return index < _tabs.Count ? NarrationParts.Tab(TabLabel(_tabs[index]), _tabs[index] == ActiveTab) : "";
        }
    }
}
