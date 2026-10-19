using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class MarriageResetRulesTests
{
    [Fact]
    public void Former_partners_include_the_spouse_and_every_married_or_engaged_friend()
    {
        var friends = new[] { ("Sebastian", true), ("Krobus", true), ("Abigail", false), ("Leah", true) };
        Assert.Equal(new[] { "Krobus", "Leah", "Sebastian" },
            MarriageResetRules.FormerPartners("Sebastian", friends).OrderBy(x => x));
    }

    [Fact]
    public void Former_partners_keep_a_spouse_whose_friendship_was_already_gone()
    {
        Assert.Equal(new[] { "Sebastian" }, MarriageResetRules.FormerPartners("Sebastian", new (string, bool)[0]));
    }

    [Fact]
    public void Former_partners_are_empty_for_a_single_farmer()
    {
        Assert.Empty(MarriageResetRules.FormerPartners(null!, new[] { ("Abigail", false) }));
        Assert.Empty(MarriageResetRules.FormerPartners("", new (string, bool)[0]));
    }

    [Theory]
    [InlineData("ApryllForever.PolyamorySweetLove/WeddingDate", true)]
    [InlineData("SpouseNeedsPlaced", true)]
    [InlineData("ApryllForever.PolyamorySweetLove/OtherParent", true)]
    [InlineData("SomeMod/RoommateSince", true)]
    [InlineData("SomeMod/MarriedOn", true)]
    [InlineData("sonofskywalker3.TheLongestYear/kept", false)]
    [InlineData("SomeMod/GiftCount", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Marriage_mod_data_keys_are_recognised(string key, bool expected)
    {
        Assert.Equal(expected, MarriageResetRules.IsMarriageModDataKey(key));
    }

    [Theory]
    [InlineData("FarmHouse", true)]
    [InlineData("Cabin", true)]
    [InlineData("Cabin12345", true)]
    [InlineData("IslandFarmHouse", true)]
    [InlineData("SebastianRoom", false)]
    [InlineData("Town", false)]
    [InlineData(null, false)]
    public void Farmer_homes_are_recognised(string map, bool expected)
    {
        Assert.Equal(expected, MarriageResetRules.IsFarmerHomeMap(map));
    }
}
