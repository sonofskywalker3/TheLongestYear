using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BookKitTests
{
    [Fact]
    public void Three_book_ids_are_stable()
    {
        Assert.Equal(
            new[] { "sonofskywalker3.TheLongestYear_Cookbook",
                    "sonofskywalker3.TheLongestYear_Craftbook",
                    "sonofskywalker3.TheLongestYear_BundleLog" },
            BookKit.AllBookQualifiedIds);
    }

    [Theory]
    [InlineData(0, 1)]   // none held -> grant 1
    [InlineData(1, 0)]   // exactly one -> grant 0
    [InlineData(3, 0)]   // too many -> grant 0 (extras removed separately)
    public void GrantCount_targets_exactly_one(int held, int expectedGrant)
        => Assert.Equal(expectedGrant, BookKit.GrantCountToReachOne(held));
}
