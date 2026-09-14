using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class UnstackableAsksTests
{
    [Theory]
    [InlineData("(H)8", true)]      // Skeleton Mask
    [InlineData("(W)13", true)]     // Insect Head
    [InlineData("(O)522", true)]    // Vampire Ring, a Gil trophy
    [InlineData("(O)388", false)]   // Wood
    [InlineData("388", false)]      // unqualified object id
    [InlineData("-1", false)]       // money slot
    [InlineData("", false)]
    public void IsUnstackable(string itemId, bool expected)
        => Assert.Equal(expected, UnstackableAsks.IsUnstackable(itemId));

    [Fact]
    public void ClampStack_caps_unstackable_items_at_one_and_leaves_the_rest()
    {
        Assert.Equal(1, UnstackableAsks.ClampStack("(H)8", 2));
        Assert.Equal(2, UnstackableAsks.ClampStack("(O)388", 2));
    }

    [Fact]
    public void RepairBundleValue_rewrites_only_unstackable_asks_above_one()
    {
        const string value = "Gil's Trophies/O 787 1/(H)27 2 0 (H)8 2 0 (O)388 3 0/4/2//Gil's Trophies";

        Assert.Equal(
            "Gil's Trophies/O 787 1/(H)27 1 0 (H)8 1 0 (O)388 3 0/4/2//Gil's Trophies",
            UnstackableAsks.RepairBundleValue(value));
    }

    [Fact]
    public void RepairBundleValue_returns_null_when_the_bundle_is_already_fine()
    {
        Assert.Null(UnstackableAsks.RepairBundleValue("Gil's Trophies/O 787 1/(H)27 1 0 (O)388 3 0/4/2//Gil's Trophies"));
        Assert.Null(UnstackableAsks.RepairBundleValue("Vault/-1 2500 2500/4/1"));
    }
}
