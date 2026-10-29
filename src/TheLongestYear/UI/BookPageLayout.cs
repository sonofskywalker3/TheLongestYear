namespace TheLongestYear.UI
{
    /// <summary>One panel size and header band for every page of the one book
    /// (<see cref="OneBookMenu"/>): the Cookbook, Craftbook and Herd Book pages are the same size
    /// and their first row starts at the same height, so switching tabs does not resize the menu
    /// (Jeff, 2026-10-29). The size is the Herd Book's, the largest, so nothing clips.</summary>
    internal static class BookPageLayout
    {
        public const int PanelWidth = 1035;
        public const int PanelHeight = 828;
        public const int PanelPad = 32;
        /// <summary>Space reserved right of the rows for the scroll arrows.</summary>
        public const int ScrollbarGutter = 52;
        public const int TitleY = 24;
        /// <summary>The line under the title (rewind-night subtitle, over-cap notice).</summary>
        public const int SubtitleY = 84;
        public const int HeaderPlain = 80;
        public const int HeaderWithSubtitle = 120;
    }
}
