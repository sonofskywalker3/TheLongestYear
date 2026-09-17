using System.Collections.Generic;
using TheLongestYear.Core.Intro;
using Xunit;

namespace TheLongestYear.Tests;

public class OpeningStringsTests
{
    [Fact]
    public void Every_vanilla_deathbed_and_letter_key_is_mapped()
    {
        string[] vanilla =
        {
            "GrandpaStory.cs.12026", "GrandpaStory.cs.12028", "GrandpaStory.cs.12029", "GrandpaStory.cs.12030",
            "GrandpaStory.cs.12031", "GrandpaStory.cs.12034", "GrandpaStory.cs.12035", "GrandpaStory.cs.12036",
            "GrandpaStory.cs.12038", "GrandpaStory.cs.12040", "GrandpaStory.cs.12051", "GrandpaStory.cs.12055",
        };
        foreach (string key in vanilla)
            Assert.True(OpeningStrings.Replacements.ContainsKey(key), key);
        Assert.Equal(vanilla.Length, OpeningStrings.Replacements.Count);
    }

    [Fact]
    public void Apply_overwrites_mapped_keys_and_leaves_empty_text_alone()
    {
        var data = new Dictionary<string, string>
        {
            ["GrandpaStory.cs.12029"] = "vanilla envelope line",
            ["GrandpaStory.cs.12051"] = "Dear {0}, {1} Farm",
            ["Game1.cs.3689"] = "Loading...",
        };
        int replaced = OpeningStrings.Apply(data, key => key == "opening.grandpa-2" ? "our envelope line" : "");
        Assert.Equal(1, replaced);
        Assert.Equal("our envelope line", data["GrandpaStory.cs.12029"]);
        Assert.Equal("Dear {0}, {1} Farm", data["GrandpaStory.cs.12051"]);   // empty text: untouched
        Assert.Equal("Loading...", data["Game1.cs.3689"]);
    }

    [Fact]
    public void Placeholders_must_match_vanilla()
    {
        Assert.True(OpeningStrings.PlaceholdersMatch("Dear {0}, welcome to {1} Farm", "{1} Farm is yours, {0}"));
        Assert.False(OpeningStrings.PlaceholdersMatch("Dear {0}, welcome to {1} Farm", "Dear {0}"));
        Assert.True(OpeningStrings.PlaceholdersMatch("no tokens", "still none"));
    }
}
