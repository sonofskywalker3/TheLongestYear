using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>Item names in the Junimos' "tainted" lines with their plurals right (Jeff,
    /// 2026-10-07: "all the Parsnip", "Bring us 3 Beer"). The game's own pluralizer does the work
    /// (<c>Lexicon.makePlural</c>, handed in by the mod), so the lines say what vanilla says;
    /// this class only corrects the words it gets wrong.
    ///
    /// What makePlural does, read off the 1.6 decompile: a fixed list it leaves alone (Tea Leaves,
    /// Clay, Hops, Bream, Driftwood and the like), a few phrases ("lumps of Coal", "bushels of
    /// Wheat"), then -y to -ies, -s/-x/-z/-sh/-ch to -es, and -s on everything else. In any
    /// language but English it returns the word unchanged. Its gaps: mass nouns that are not on
    /// its list (Beer becomes "Beers", Wool "Wools", Hay "Hays") and a vowel before the y (Honey
    /// becomes "Honies").</summary>
    public static class ItemPlurals
    {
        /// <summary>Mass nouns: "all the Beer", "3 Beer". Countable foods like Salad stay with
        /// the game ("Salads").</summary>
        private static readonly HashSet<string> MassNouns = new HashSet<string>(StringComparer.Ordinal)
        {
            "Beer", "Wine", "Mead", "Juice", "Coffee", "Green Tea", "Milk", "Large Milk", "Goat Milk",
            "L. Goat Milk", "Wool", "Cloth", "Hay", "Fiber", "Honey", "Rice", "Sugar", "Wheat Flour",
            "Oil", "Truffle Oil", "Vinegar", "Maple Syrup", "Oak Resin", "Pine Tar", "Sap", "Bug Meat",
            "Roe", "Aged Roe", "Caviar", "Squid Ink", "Mayonnaise", "Duck Mayonnaise",
            "Void Mayonnaise", "Dinosaur Mayonnaise", "Moss", "Cranberry Sauce", "Seaweed",
        };

        /// <summary>The plural of an item's display name. <paramref name="gamePlural"/> is the
        /// game's own pluralizer; null leaves the name as it is.</summary>
        public static string Plural(string name, Func<string, string> gamePlural)
        {
            if (string.IsNullOrEmpty(name) || gamePlural == null) return name;
            if (MassNouns.Contains(name)) return name;
            string plural = gamePlural(name);
            // The game left it alone (its own list, or a language it does not pluralize): so do we.
            if (plural == name) return name;
            // A vowel before the final y takes a plain s: Honey style names, Turkey, Key.
            if (name.Length > 1 && name[name.Length - 1] == 'y' && IsVowel(name[name.Length - 2]))
                return name + "s";
            return plural;
        }

        /// <summary>The ask in "Bring us {{new}} instead": the bare name for one, "3 Parsnips" for
        /// more.</summary>
        public static string Ask(int count, string name, Func<string, string> gamePlural)
            => count > 1 ? $"{count} {Plural(name, gamePlural)}" : name;

        /// <summary>Whether the ask reads as plural, which picks "They remain pure." over "It
        /// remains pure.".</summary>
        public static bool AskIsPlural(int count) => count > 1;

        private static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;
    }
}
