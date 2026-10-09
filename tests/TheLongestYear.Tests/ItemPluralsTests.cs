using System;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07: the Junimos' tamper lines read "all the Parsnip" and "Bring us 3
/// Beer". <see cref="VanillaPlural"/> is the 1.6 <c>Lexicon.makePlural</c> in English, copied from
/// the PC decompile (its fixed list trimmed to the names these tests use), so the tests check the
/// corrections against what the game would really say.</summary>
public class ItemPluralsTests
{
    private static readonly string[] VanillaLeavesAlone = { "Clay", "Tea Leaves", "Hops", "Bream", "Driftwood", "Mixed Seeds", "Weeds" };

    internal static string VanillaPlural(string word)
    {
        switch (word)
        {
            case "Coal": return "lumps of Coal";
            case "Wheat": return "bushels of Wheat";
            case "Jelly": return "Jellies";
        }
        if (VanillaLeavesAlone.Contains(word)) return word;
        switch (word.Last())
        {
            case 'y': return word.Substring(0, word.Length - 1) + "ies";
            case 's':
                return word.EndsWith(" Seeds") || word.EndsWith(" Bass") ? word : word + "es";
            case 'x':
            case 'z':
                return word + "es";
            default:
                return word.EndsWith("sh") || word.EndsWith("ch") ? word + "es" : word + "s";
        }
    }

    [Theory]
    [InlineData("Parsnip", "Parsnips")]
    [InlineData("Summer Squash", "Summer Squashes")]
    [InlineData("Peach", "Peaches")]
    [InlineData("Strawberry", "Strawberries")]
    [InlineData("Salad", "Salads")]
    [InlineData("Winter Root", "Winter Roots")]
    [InlineData("Coal", "lumps of Coal")]
    public void Countable_names_take_the_games_plural(string name, string plural)
        => Assert.Equal(plural, ItemPlurals.Plural(name, VanillaPlural));

    [Theory]
    [InlineData("Tea Leaves")]
    [InlineData("Clay")]
    [InlineData("Hops")]
    [InlineData("Driftwood")]
    public void Names_the_game_leaves_alone_stay_as_they_are(string name)
        => Assert.Equal(name, ItemPlurals.Plural(name, VanillaPlural));

    [Theory]
    [InlineData("Beer")]
    [InlineData("Wine")]
    [InlineData("Wool")]
    [InlineData("Hay")]
    [InlineData("Honey")]
    [InlineData("Milk")]
    [InlineData("Truffle Oil")]
    [InlineData("Mayonnaise")]
    public void Mass_nouns_the_game_gets_wrong_stay_bare(string name)
    {
        Assert.NotEqual(name, VanillaPlural(name));   // the game would get it wrong
        Assert.Equal(name, ItemPlurals.Plural(name, VanillaPlural));
    }

    // Wood became a tamper target with the per-bundle rule (Construction's double Wood, Jeff
    // 2026-10-07), and the first live run read "It has tainted all the Woods". The tainted name
    // follows the ask's own word lists: bulk stuff and container goods stay bare, the jellyfish
    // count.
    [Theory]
    [InlineData("Wood", "(O)388", "Wood")]
    [InlineData("Hardwood", "(O)709", "Hardwood")]
    [InlineData("Copper Ore", "(O)378", "Copper Ore")]
    [InlineData("Refined Quartz", "(O)338", "Refined Quartz")]
    [InlineData("Pumpkin Soup", "(O)236", "Pumpkin Soup")]
    [InlineData("Sea Jelly", "(O)SeaJelly", "Sea Jellies")]
    [InlineData("Parsnip", "(O)24", "Parsnips")]
    public void The_tainted_name_reads_like_the_ask(string name, string id, string expected)
        => Assert.Equal(expected, ItemPlurals.Tainted(name, id, flavored: false, VanillaPlural));

    // Designer, 2026-10-07: "all the Pike", "all the Holly"; the jellies still count.
    [Theory]
    [InlineData("Pike", "(O)144", "Pike")]
    [InlineData("Salmon", "(O)139", "Salmon")]
    [InlineData("Largemouth Bass", "(O)136", "Largemouth Bass")]
    [InlineData("Octopus", "(O)149", "Octopus")]
    [InlineData("Sea Jelly", "(O)SeaJelly", "Sea Jellies")]
    public void The_tainted_name_of_a_fish_keeps_the_same_word(string name, string id, string expected)
        => Assert.Equal(expected, ItemPlurals.Tainted(name, id, flavored: false, VanillaPlural, FlavoredSlotRules.FishCategory));

    [Theory]
    [InlineData("Mussel", "(O)719", "Mussels")]
    [InlineData("Oyster", "(O)723", "Oysters")]
    [InlineData("Clam", "(O)372", "Clams")]
    [InlineData("Crab", "(O)717", "Crabs")]
    [InlineData("Lobster", "(O)715", "Lobsters")]
    [InlineData("Snail", "(O)721", "Snails")]
    [InlineData("Cockle", "(O)718", "Cockles")]
    [InlineData("Periwinkle", "(O)722", "Periwinkles")]
    [InlineData("Shrimp", "(O)720", "Shrimp")]
    [InlineData("Crayfish", "(O)716", "Crayfish")]
    [InlineData("Super Cucumber", "(O)155", "Super Cucumbers")]
    [InlineData("Sea Cucumber", "(O)154", "Sea Cucumbers")]
    public void The_tainted_name_of_a_shellfish_takes_its_plural(string name, string id, string expected)
        => Assert.Equal(expected, ItemPlurals.Tainted(name, id, flavored: false, VanillaPlural, FlavoredSlotRules.FishCategory));

    [Theory]
    [InlineData("Super Cucumber", "(O)155")]
    [InlineData("Sea Cucumber", "(O)154")]
    public void A_tainted_cucumber_reads_as_counted(string name, string id)
        => Assert.False(ItemPlurals.TaintedReadsAsMass(name, id, flavored: false, VanillaPlural, FlavoredSlotRules.FishCategory));

    [Fact]
    public void The_tainted_name_of_holly_is_bare()
        => Assert.Equal("Holly", ItemPlurals.Tainted("Holly", "(O)283", flavored: false, VanillaPlural));

    [Fact]
    public void A_vowel_before_the_y_takes_a_plain_s()
        => Assert.Equal("Turkeys", ItemPlurals.Plural("Turkey", VanillaPlural));

    [Fact]
    public void A_language_the_game_does_not_pluralize_is_left_alone()
        => Assert.Equal("Pastinaca", ItemPlurals.Plural("Pastinaca", w => w));

    [Fact]
    public void No_pluralizer_leaves_the_name()
        => Assert.Equal("Parsnip", ItemPlurals.Plural("Parsnip", null!));

    [Theory]
    [InlineData(1, "(O)24", "Parsnip", "a Parsnip")]
    [InlineData(3, "(O)24", "Parsnip", "3 Parsnips")]
    [InlineData(3, "(O)346", "Beer", "3 mugs of Beer")]
    [InlineData(5, "(O)815", "Tea Leaves", "5 Tea Leaves")]
    [InlineData(2, "(O)382", "Coal", "2 lumps of Coal")]
    [InlineData(0, "(O)24", "Parsnip", "a Parsnip")]
    public void The_ask_is_one_item_for_one_and_a_counted_phrase_for_more(int count, string id, string name, string ask)
        => Assert.Equal(ask, AskPhrases.Ask(count, id, name, VanillaPlural));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void They_remain_pure_only_for_more_than_one(int count, bool plural)
        => Assert.Equal(plural, ItemPlurals.AskIsPlural(count));
}
