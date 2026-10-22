using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FuzzyNameIndexTests
{
    private static FuzzyNameIndex Index(params string[] names)
    {
        var entries = new List<KeyValuePair<string, string>>();
        foreach (string name in names)
            entries.Add(new KeyValuePair<string, string>(name, "(O)" + name));
        return new FuzzyNameIndex(entries);
    }

    [Fact]
    public void Exact_name_beats_an_earlier_prefix_match()
    {
        var index = Index("Salmonberry", "Salmon");

        Assert.Equal("(O)Salmon", index.Find("Salmon"));
    }

    [Fact]
    public void Formatting_match_ignores_case_spaces_and_punctuation()
    {
        var index = Index("Cranberry Candy", "Jack-O-Lantern");

        Assert.Equal("(O)Jack-O-Lantern", index.Find("jack o lantern"));
    }

    [Fact]
    public void Prefix_beats_contains()
    {
        var index = Index("Wild Plum Tea", "Plum Pudding");

        Assert.Equal("(O)Plum Pudding", index.Find("Plum"));
    }

    [Fact]
    public void Ties_go_to_the_first_name_in_order()
    {
        var index = Index("Red Cabbage", "Red Mushroom");

        Assert.Equal("(O)Red Cabbage", index.Find("Red"));
    }

    [Fact]
    public void First_id_wins_for_a_repeated_name()
    {
        var index = new FuzzyNameIndex(new[]
        {
            new KeyValuePair<string, string>("Stone", "(O)390"),
            new KeyValuePair<string, string>("Stone", "(BC)Stone"),
        });

        Assert.Equal("(O)390", index.Find("Stone"));
    }

    [Fact]
    public void No_match_returns_null()
    {
        Assert.Null(Index("Parsnip").Find("Starfruit"));
    }

    [Fact]
    public void A_name_made_only_of_punctuation_still_matches_itself()
    {
        var index = Index("???");

        Assert.Equal("(O)???", index.Find(" ??? "));
    }
}
