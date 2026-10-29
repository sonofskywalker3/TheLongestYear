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
    }
}
