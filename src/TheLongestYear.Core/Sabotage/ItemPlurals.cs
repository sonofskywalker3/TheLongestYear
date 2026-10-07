using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>Item names in the Junimos' "tainted" lines with their plurals right (Jeff,
    /// 2026-10-07: "all the Parsnip", "Bring us 3 Beer"). The game's own pluralizer does the work
    /// (<c>Lexicon.makePlural</c>, handed in by the mod), so the lines say what vanilla says;
    /// this class only corrects the words it gets wrong. The counted ask ("Bring us 3 mugs of
    /// Beer") is <see cref="AskPhrases"/>, which falls back here for an item it does not know.
    ///
    /// What makePlural does, read off the 1.6 decompile: a fixed list it leaves alone (Tea Leaves,
    /// Clay, Hops, Bream, Driftwood and the like), a few phrases ("lumps of Coal", "bushels of
    /// Wheat"), then -y to -ies, -s/-x/-z/-sh/-ch to -es, and -s on everything else. In any
    /// language but English it returns the word unchanged. Its gaps: mass nouns that are not on
    /// its list (Beer becomes "Beers", Wool "Wools", Hay "Hays") and a vowel before the y (Honey
    /// becomes "Honies").</summary>
    public static class ItemPlurals
    {
        /// <summary>Mass nouns: "all the Beer". Countable foods like Salad stay with
        /// the game ("Salads").</summary>
        private static readonly HashSet<string> MassNouns = new HashSet<string>(StringComparer.Ordinal)
        {
            "Beer", "Wine", "Mead", "Juice", "Coffee", "Green Tea", "Milk", "Large Milk", "Goat Milk",
            "L. Goat Milk", "Wool", "Cloth", "Hay", "Fiber", "Honey", "Rice", "Sugar", "Wheat Flour",
            "Oil", "Truffle Oil", "Vinegar", "Maple Syrup", "Oak Resin", "Pine Tar", "Sap", "Bug Meat",
            "Roe", "Aged Roe", "Caviar", "Squid Ink", "Mayonnaise", "Duck Mayonnaise",
            "Void Mayonnaise", "Dinosaur Mayonnaise", "Moss", "Cranberry Sauce", "Seaweed",
            "Jelly",
        };

        /// <summary>The plural of an item's display name. <paramref name="gamePlural"/> is the
        /// game's own pluralizer; null leaves the name as it is.</summary>
        public static string Plural(string name, Func<string, string> gamePlural)
        {
            if (string.IsNullOrEmpty(name) || gamePlural == null) return name;
            if (MassNouns.Contains(name) || MassNouns.Contains(LastWord(name))) return name;
            return CountedPlural(name, gamePlural);
        }

        /// <summary>The plural of a name known to be countable, with no mass-noun check: the game's
        /// plural, a vowel before the final y taking a plain s. <see cref="AskPhrases"/> uses it for
        /// the jellyfish ("Sea Jellies"), whose last word the mass-noun list would otherwise catch.</summary>
        public static string CountedPlural(string name, Func<string, string> gamePlural)
        {
            if (string.IsNullOrEmpty(name) || gamePlural == null) return name;
            string plural = gamePlural(name);
            // The game left it alone (its own list, or a language it does not pluralize): so do we.
            if (plural == name) return name;
            // A vowel before the final y takes a plain s: Honey style names, Turkey, Key.
            if (name.Length > 1 && name[name.Length - 1] == 'y' && IsVowel(name[name.Length - 2]))
                return name + "s";
            return plural;
        }

        /// <summary>Whether the ask (<see cref="AskPhrases.Ask"/>) reads as plural, which picks "They
        /// remain pure." over "It remains pure.". A container phrase is plural too ("3 jars of
        /// Wild Honey. They remain pure.").</summary>
        public static bool AskIsPlural(int count) => count > 1;

        /// <summary>The old item in "It has tainted all the {{old}}": the exact item, flavour
        /// included, pluralised (designer, 2026-10-07: "all the Dried Apples"). <paramref name="name"/>
        /// is the game's display name for it. The game's own names for a flavoured Dried Fruit or
        /// Smoked Fish already read right: Object.loadDisplayName runs Dried Fruit through
        /// makePlural itself ("Dried Apples"), and "all the Smoked Salmon" is how the fish is said,
        /// so those stay as the game wrote them. Anything else takes <see cref="Plural"/>.</summary>
        public static string Tainted(string name, string baseItemId, bool flavored, Func<string, string> gamePlural)
        {
            if (flavored && FlavoredSlotRules.IsFlavored(baseItemId)) return name;
            // The ask's own word lists (AskPhrases): bulk stuff and container goods are mass nouns
            // here ("all the Wood", "all the Copper Ore", "all the Pumpkin Soup"), the jellyfish
            // are counted ("all the Sea Jellies").
            if (AskPhrases.ContainerFor(baseItemId) != null || AskPhrases.IsSameInThePlural(baseItemId)) return name;
            if (AskPhrases.IsCountable(baseItemId)) return CountedPlural(name, gamePlural);
            return Plural(name, gamePlural);
        }

        /// <summary>The head noun of a multi-word name ("Blueberry Jelly" to "Jelly").</summary>
        private static string LastWord(string name)
        {
            int space = name.LastIndexOf(' ');
            return space < 0 ? name : name.Substring(space + 1);
        }

        private static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;
    }
}
