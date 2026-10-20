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
    [InlineData("SomeMod/RoommateSince", false)]
    [InlineData("SomeMod/MarriedOn", false)]
    [InlineData("SomeMod/SpouseGiftCount", false)]
    [InlineData("sonofskywalker3.TheLongestYear/kept", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_known_marriage_mod_data_keys_are_recognised(string key, bool expected)
    {
        Assert.Equal(expected, MarriageResetRules.IsMarriageModDataKey(key));
    }

    [Theory]
    [InlineData("Sebastian", false, false, true)]   // hearts wiped, name left: the bug's save
    [InlineData("Sebastian", true, false, true)]    // entry back at Friendly: still orphaned
    [InlineData("Sebastian", true, true, false)]    // a real marriage this loop
    [InlineData(null, false, false, false)]         // single
    [InlineData("", false, false, false)]
    public void Orphaned_spouse_is_one_with_no_marriage_behind_it(string spouse, bool hasEntry, bool partner, bool expected)
    {
        Assert.Equal(expected, MarriageResetRules.IsOrphanedSpouse(spouse, hasEntry, partner));
    }
}
