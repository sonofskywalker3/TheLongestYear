using StardewValley.Menus;

namespace TheLongestYear.UI
{
    /// <summary>A tab page of the one book (<see cref="OneBookMenu"/>): the Cookbook, Craftbook
    /// and Herd Book menus.</summary>
    internal interface IBookPage
    {
        /// <summary>The book that hosts the page. The remove confirmation returns to it, and the
        /// page leaves the screen dimming to it so the tabs are not dimmed.</summary>
        IClickableMenu Host { get; set; }

        /// <summary>Height of the title area above the first row (taller with a subtitle or notice),
        /// so the tabs line up with the first row.</summary>
        int HeaderHeight { get; }

        /// <summary>For the screen reader: the page's title and the lines under it, with how to
        /// use the picker while it is open. The book speaks it again whenever it changes.</summary>
        string NarrationHeader();

        /// <summary>For the screen reader: a line that changes within one header (the picker's
        /// source tab), or null. The book speaks it whenever it changes.</summary>
        string NarrationSection();

        /// <summary>For the screen reader: the page's snapped row or button, read out in full.
        /// Empty when the snap is not on one of the page's own components.</summary>
        string NarrateFocus();
    }
}
