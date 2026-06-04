using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core.Day28
{
    /// <summary>Turns a Stardew dialogue command string (the same token format used by event
    /// <c>speak</c>/<c>message</c>) into clean, display-ready pages for the self-drawn
    /// <c>Day28CutsceneMenu</c>: substitutes <c>@</c> → the player name, splits on the <c>#$b#</c>
    /// page break, and strips the <c>$&lt;letter&gt;</c> portrait/emote pose codes (and stray
    /// <c>#</c>) the menu can't render. Pure + unit-tested; the menu is glue.</summary>
    public static class Day28DialogueScript
    {
        private const string PageBreak = "#$b#";

        public static IReadOnlyList<string> ToPages(string raw, string playerName)
        {
            var pages = new List<string>();
            if (string.IsNullOrEmpty(raw))
                return pages;

            string named = raw.Replace("@", playerName ?? string.Empty);
            foreach (string segment in named.Split(new[] { PageBreak }, System.StringSplitOptions.None))
            {
                string clean = StripCodes(segment).Trim();
                if (clean.Length > 0)
                    pages.Add(clean);
            }
            return pages;
        }

        /// <summary>Drop the <c>$x</c> pose/emote codes (e.g. <c>$h</c> happy, <c>$s</c> sad) and any
        /// leftover <c>#</c> separators, collapsing the doubled spaces that can leave behind.</summary>
        private static string StripCodes(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '$' && i + 1 < s.Length)
                {
                    i++; // skip the code letter after '$'
                    continue;
                }
                if (c == '#')
                    continue;
                sb.Append(c);
            }
            // Collapse any run of 2+ spaces (stripping a code from between spaces can leave 3+) to one.
            return Regex.Replace(sb.ToString(), " {2,}", " ");
        }
    }
}
