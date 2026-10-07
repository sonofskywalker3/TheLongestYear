using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>The counted ask in "Bring us {{new}} instead" (designer, 2026-10-07: "I prefer jars of
    /// wild honey, and sea jellies, and bottles of blueberry wine"). For more than one:
    /// a liquid or spread names its container ("3 bottles of Blueberry Wine"), a countable thing
    /// takes its plural ("3 Sea Jellies"), and bulk stuff with no container stays bare ("3 Clay").
    /// One is always the bare name.
    ///
    /// Keyed on the item's base id, not its name, so a flavoured good finds its container whatever
    /// the flavour ("3 jars of Pickled Beets"); the container goes before the full display name.
    /// The containers follow the item's sprite, the way the designer picked them: a mug for Beer,
    /// a pilsner glass for Pale Ale, a loose cluster of eggs for Roe. An id these tables do not
    /// know (a modded item) falls back to <see cref="ItemPlurals.Plural"/>'s name rules.
    ///
    /// English only, like the rest of <see cref="ItemPlurals"/>.</summary>
    public static class AskPhrases
    {
        /// <summary>An item with no Object category given (a caller that does not know it).</summary>
        public const int NoCategory = 0;

        /// <summary>Base id to the container word, plural and lowercase.</summary>
        private static readonly IReadOnlyDictionary<string, string> Containers = BuildContainers();

        /// <summary>Bulk stuff with no natural container: "3 Clay", "3 Wool". Also countable names
        /// whose plural is the same word ("3 Smoked Fish") or already plural ("3 Cookies"), which
        /// makePlural would get wrong ("Smoked Fishes", "Cookieses").</summary>
        private static readonly HashSet<string> SameInThePlural = new(StringComparer.Ordinal)
        {
            "(O)178",          // Hay
            "(O)440",          // Wool
            "(O)428",          // Cloth
            "(O)771",          // Fiber
            "(O)92",           // Sap
            "(O)Moss",         // Moss
            "(O)152",          // Seaweed
            "(O)153",          // Green Algae
            "(O)157",          // White Algae
            "(O)684",          // Bug Meat
            "(O)766",          // Slime
            "(O)388",          // Wood
            "(O)709",          // Hardwood
            "(O)378",          // Copper Ore
            "(O)380",          // Iron Ore
            "(O)384",          // Gold Ore
            "(O)386",          // Iridium Ore
            "(O)909",          // Radioactive Ore
            "(O)338",          // Refined Quartz
            "(O)SmokedFish",   // Smoked Fish, and every flavour ("3 Smoked Salmon")
            "(O)198",          // Baked Fish
            "(O)242",          // Dish O' The Sea
            "(O)223",          // Cookies
        };

        /// <summary>Countable things whose last word the name rules treat as a mass noun: the
        /// jellyfish are fish ("3 Sea Jellies").</summary>
        private static readonly HashSet<string> Countable = new(StringComparer.Ordinal)
        {
            "(O)SeaJelly", "(O)RiverJelly", "(O)CaveJelly",
        };

        private static Dictionary<string, string> BuildContainers()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string container, params string[] ids)
            {
                foreach (string id in ids) map.Add(id, container);
            }

            Add("mugs", "(O)346");                                     // Beer
            Add("glasses", "(O)303");                                  // Pale Ale (a pilsner glass)
            Add("cups", "(O)395", "(O)614", "(O)253");                 // Coffee, Green Tea, Triple Shot Espresso
            Add("tins", "(O)445");                                     // Caviar
            Add("clusters", "(O)812");                                 // Roe (loose eggs)
            Add("jugs", "(O)459");                                     // Mead (a jug with a handle)
            Add("jars",
                "(O)447",                                              // Aged Roe
                "(O)340",                                              // Honey
                "(O)344",                                              // Jelly
                "(O)342",                                              // Pickles
                "(O)306", "(O)307", "(O)308", "(O)807",                // the four Mayonnaises
                "(O)726",                                              // Pine Tar
                "(O)DriedFruit");                                      // Dried Fruit
            Add("bottles",
                "(O)348",                                              // Wine
                "(O)350",                                              // Juice
                "(O)724",                                              // Maple Syrup
                "(O)725",                                              // Oak Resin
                "(O)MysticSyrup",                                      // Mystic Syrup
                "(O)247", "(O)432",                                    // Oil, Truffle Oil
                "(O)419",                                              // Vinegar
                "(O)184", "(O)186", "(O)436", "(O)438",                // Milk, Large Milk, Goat Milk, L. Goat Milk
                "(O)814",                                              // Squid Ink
                "(O)903",                                              // Ginger Ale
                "(O)773",                                              // Life Elixir
                "(O)772");                                             // Oil of Garlic
            Add("cans", "(O)167");                                     // Joja Cola
            Add("bags",
                "(O)245", "(O)246", "(O)423",                          // Sugar, Wheat Flour, Rice
                "(O)DriedMushrooms");                                  // Dried Mushrooms (a tied pouch)
            Add("sprigs", "(O)283");                                   // Holly (designer, 2026-10-07)
            Add("boxes", "(O)Raisins");                                // Raisins
            Add("loaves", "(O)216");                                   // Bread
            Add("bowls",
                "(O)199", "(O)218", "(O)219", "(O)236", "(O)456",      // Parsnip, Tom Kha, Trout, Pumpkin, Algae Soup
                "(O)MossSoup",                                         // Moss Soup
                "(O)457", "(O)727", "(O)730", "(O)728",                // Pale Broth, Chowder, Lobster Bisque, Fish Stew
                "(O)207",                                              // Bean Hotpot
                "(O)232",                                              // Rice Pudding (the game's own "bowls of")
                "(O)238", "(O)605",                                    // Cranberry Sauce, Artichoke Dip
                "(O)239", "(O)648",                                    // Stuffing, Coleslaw
                "(O)906", "(O)907", "(O)649",                          // Poi, Tropical Curry, Fiddlehead Risotto
                "(O)921");                                             // Squid Ink Ravioli
            Add("plates",
                "(O)224", "(O)606", "(O)905",                          // Spaghetti, Stir Fry, Mango Sticky Rice
                "(O)202", "(O)231", "(O)227", "(O)729");               // Fried Calamari, Eggplant Parmesan, Sashimi, Escargot
            return map;
        }

        /// <summary>Is this item's plural the bare word ("3 Wood", "3 Clay")?</summary>
        public static bool IsSameInThePlural(string? itemId)
            => !string.IsNullOrEmpty(itemId) && SameInThePlural.Contains(BundleParsing.NormalizeItemId(itemId!));

        /// <summary>Is this a countable thing whose last word the mass-noun rule would catch?</summary>
        public static bool IsCountable(string? itemId)
            => !string.IsNullOrEmpty(itemId) && Countable.Contains(BundleParsing.NormalizeItemId(itemId!));

        /// <summary>The container word for this item ("bottles"), or null when it has none.</summary>
        public static string? ContainerFor(string? itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return Containers.TryGetValue(BundleParsing.NormalizeItemId(itemId!), out string? c) ? c : null;
        }

        /// <summary>Does this item keep the same word in the plural because it is a fish (designer,
        /// 2026-10-07: "7 Pike", "7 Salmon", "3 Largemouth Bass")? Every Object of the Fish category
        /// (<see cref="FlavoredSlotRules.FishCategory"/>), except the jellies he chose to count
        /// ("Sea Jellies"). Fish goods with a container or a bare word of their own (Roe, Aged Roe,
        /// Caviar, Smoked Fish) are matched by those tables first.</summary>
        public static bool IsFishSameInThePlural(string? itemId, int category)
            => category == FlavoredSlotRules.FishCategory && !IsCountable(itemId);

        /// <summary>The ask: the bare <paramref name="name"/> for one, otherwise the count and a
        /// container phrase, plural or bare word. <paramref name="gamePlural"/> is the game's own
        /// pluralizer (Lexicon.makePlural); null leaves the name unpluralised. <paramref name="category"/> is
        /// the item's Object category, which tells a fish (it keeps the same word).</summary>
        public static string Ask(int count, string itemId, string name, Func<string, string> gamePlural, int category = NoCategory)
        {
            if (count <= 1 || string.IsNullOrEmpty(name)) return name;
            string id = BundleParsing.NormalizeItemId(itemId ?? "");
            if (Containers.TryGetValue(id, out string? container)) return $"{count} {container} of {name}";
            if (SameInThePlural.Contains(id)) return $"{count} {name}";
            if (Countable.Contains(id)) return $"{count} {ItemPlurals.CountedPlural(name, gamePlural)}";
            if (IsFishSameInThePlural(id, category)) return $"{count} {name}";
            return $"{count} {ItemPlurals.Plural(name, gamePlural)}";
        }
    }
}
