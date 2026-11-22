using System;
using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Spoken pieces several narrated menus share: a tab, a row's place in its list and a
    /// scroll button.</summary>
    internal static class NarrationParts
    {
        /// <summary>"Boosts tab" or, for the tab being shown, "Boosts tab, open".</summary>
        public static string Tab(string name, bool open)
            => open
                ? Strings.Get("narrate.tab-open", new Dictionary<string, string> { ["name"] = name })
                : Strings.Get("narrate.tab", new Dictionary<string, string> { ["name"] = name });

        /// <summary>"3 of 12" for the row at zero-based <paramref name="index"/>.</summary>
        public static string Position(int index, int total)
            => Strings.Get("narrate.shrine.row", new Dictionary<string, string>
            {
                ["position"] = (index + 1).ToString(),
                ["total"] = total.ToString(),
            });

        /// <summary>A scroll button, with the rows now shown: the range is what changes when the
        /// button is pressed, so the press is heard.</summary>
        public static string Scroll(bool up, int start, int rowsPerPage, int total)
        {
            string button = up ? Strings.Get("narrate.shrine.scroll-up") : Strings.Get("narrate.shrine.scroll-down");
            if (total <= 0) return NarrationText.Sentences(button);
            return NarrationText.Sentences(button, Strings.Get("narrate.rows", new Dictionary<string, string>
            {
                ["first"] = (start + 1).ToString(),
                ["last"] = Math.Min(total, start + rowsPerPage).ToString(),
                ["total"] = total.ToString(),
            }));
        }
    }
}
