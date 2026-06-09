using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcItemEquivalenceTests
{
    [Theory]
    [InlineData("(O)174", "(O)182")] // Large Egg white <-> brown
    [InlineData("(O)182", "(O)174")]
    [InlineData("(O)176", "(O)180")] // Egg white <-> brown
    [InlineData("174", "182")]       // bare ids fold too
    public void Egg_color_variants_match(string a, string b)
    {
        Assert.True(CcItemEquivalence.Matches(a, b));
    }

    [Theory]
    [InlineData("(O)174", "(O)176")] // Large Egg vs Egg — different items
    [InlineData("(O)174", "(O)186")] // Large Egg vs Large Milk
    [InlineData("(O)24", "(O)188")]  // Parsnip vs Green Bean
    public void Distinct_items_do_not_match(string a, string b)
    {
        Assert.False(CcItemEquivalence.Matches(a, b));
    }

    [Fact]
    public void Same_id_matches_itself()
    {
        Assert.True(CcItemEquivalence.Matches("(O)388", "(O)388")); // Wood
    }

    [Fact]
    public void Canonical_folds_brown_to_white_and_passes_others_through()
    {
        Assert.Equal("174", CcItemEquivalence.Canonical("(O)182"));
        Assert.Equal("176", CcItemEquivalence.Canonical("(O)180"));
        Assert.Equal("388", CcItemEquivalence.Canonical("(O)388"));
    }
}
