using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BookKitTests
{
    [Fact]
    public void Book_ids_are_stable()
    {
        // Saves hold these ids; the retired three must still match what older saves wrote.
        Assert.Equal("sonofskywalker3.TheLongestYear_Cookbook", BookKit.CookbookId);
        Assert.Equal("sonofskywalker3.TheLongestYear_Craftbook", BookKit.CraftbookId);
        Assert.Equal("sonofskywalker3.TheLongestYear_BundleLog", BookKit.BundleLogId);
        Assert.Equal("sonofskywalker3.TheLongestYear_HerdBook", BookKit.HerdBookId);
    }

    [Theory]
    [InlineData(0, 1)]   // none held -> grant 1
    [InlineData(1, 0)]   // exactly one -> grant 0
    [InlineData(3, 0)]   // too many -> grant 0 (extras removed separately)
    public void GrantCount_targets_exactly_one(int held, int expectedGrant)
        => Assert.Equal(expectedGrant, BookKit.GrantCountToReachOne(held));
}
