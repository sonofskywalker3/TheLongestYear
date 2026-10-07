using System;
using System.Linq;
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

    private static string VanillaPlural(string word)
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
    [InlineData(1, "Parsnip", "Parsnip")]
    [InlineData(3, "Parsnip", "3 Parsnips")]
    [InlineData(3, "Beer", "3 Beer")]
    [InlineData(5, "Tea Leaves", "5 Tea Leaves")]
    [InlineData(2, "Coal", "2 lumps of Coal")]
    [InlineData(0, "Parsnip", "Parsnip")]
    public void The_ask_is_the_bare_name_for_one_and_a_counted_plural_for_more(int count, string name, string ask)
        => Assert.Equal(ask, ItemPlurals.Ask(count, name, VanillaPlural));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void They_remain_pure_only_for_more_than_one(int count, bool plural)
        => Assert.Equal(plural, ItemPlurals.AskIsPlural(count));
}
