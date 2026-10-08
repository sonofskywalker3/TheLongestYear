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
        /// so those stay as the game wrote them. Anything else takes <see cref="Plural"/>. A fish (by
        /// <paramref name="category"/>) keeps the same word: "all the Pike".</summary>
        public static string Tainted(string name, string baseItemId, bool flavored, Func<string, string> gamePlural, int category = AskPhrases.NoCategory)
        {
            if (flavored && FlavoredSlotRules.IsFlavored(baseItemId)) return name;
            // The ask's own word lists (AskPhrases): bulk stuff and container goods are mass nouns
            // here ("all the Wood", "all the Copper Ore", "all the Pumpkin Soup"), the jellyfish
            // are counted ("all the Sea Jellies").
            if (AskPhrases.ContainerFor(baseItemId) != null || AskPhrases.IsSameInThePlural(baseItemId)) return name;
            if (AskPhrases.IsCountable(baseItemId)) return CountedPlural(name, gamePlural);
            // A fish keeps the same word (designer, 2026-10-07: "all the Pike").
            if (AskPhrases.IsFishSameInThePlural(baseItemId, category)) return name;
            return Plural(name, gamePlural);
        }

        /// <summary>Goods counted one by one whose name stays the same word: "all the Smoked
        /// Salmon", "all the Dried Apples", "all the Cookies".</summary>
        private static readonly HashSet<string> CountedSameWord = new HashSet<string>(StringComparer.Ordinal)
        {
            FlavoredSlotRules.SmokedFish, FlavoredSlotRules.DriedFruit, FlavoredSlotRules.DriedMushrooms,
            "(O)198", // Baked Fish
            "(O)223", // Cookies
            "(O)Raisins",
        };

        /// <summary>Names that are already plural, which the game leaves alone (its own list in
        /// Lexicon.makePlural, plus the ones the ask tables keep bare).</summary>
        private static readonly HashSet<string> AlreadyPlural = new HashSet<string>(StringComparer.Ordinal)
        {
            "Broken Glasses", "Crab Cakes", "Cranberries", "Dried Cranberries", "Dried Sunflowers",
            "Fossilized Ribs", "Glass Shards", "Glazed Yams", "Green Canes", "Hashbrowns", "Hops",
            "Mixed Seeds", "Pancakes", "Pepper Poppers", "Pickles", "Red Canes", "Roasted Hazelnuts",
            "Star Shards", "Tea Leaves", "Weeds", "Cookies", "Raisins", "Dried Mushrooms",
        };

        /// <summary>Does "It has tainted all the X" read X as uncountable, so the next line says
        /// "The darkness has touched it" (designer, 2026-10-08)? True for a mass noun, bulk stuff or
        /// a container good whose tainted name stays singular ("all the Wood", "all the Honey",
        /// "all the Blueberry Wine"). False for anything counted: a name the rule pluralised ("all
        /// the Parsnips", "all the lumps of Coal"), a fish or shellfish ("all the Pike"), a counted
        /// good that keeps one word ("all the Smoked Salmon", "all the Dried Apples") and a name
        /// that is already plural ("all the Hops").</summary>
        public static bool TaintedReadsAsMass(string name, string baseItemId, bool flavored, Func<string, string> gamePlural, int category = AskPhrases.NoCategory)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Tainted(name, baseItemId, flavored, gamePlural, category) != name) return false;
            if (AskPhrases.IsFishSameInThePlural(baseItemId, category) || AskPhrases.IsCountable(baseItemId)) return false;
            if (!string.IsNullOrEmpty(baseItemId) && CountedSameWord.Contains(BundleParsing.NormalizeItemId(baseItemId))) return false;
            return !AlreadyPlural.Contains(name);
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
