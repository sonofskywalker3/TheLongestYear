using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleAskRewriteTests
{
    private static int CapAtFive(string id, int stack) => Math.Min(stack, 5);

    [Fact]
    public void Lowers_only_the_stacks_the_clamp_changes_and_keeps_every_other_field()
    {
        const string value = "Spring Foraging/O 495 30/16 1 0 18 9 0 20 1 0/0/4/Spring Foraging";

        string? lowered = BundleAskRewrite.LowerAsks(value, CapAtFive);

        Assert.Equal("Spring Foraging/O 495 30/16 1 0 18 5 0 20 1 0/0/4/Spring Foraging", lowered);
    }

    [Fact]
    public void Clamp_sees_each_item_id_and_its_stack()
    {
        var seen = new List<(string, int)>();
        BundleAskRewrite.LowerAsks("B/R/16 2 0 18 9 1", (id, stack) => { seen.Add((id, stack)); return stack; });
        Assert.Equal(new[] { ("16", 2), ("18", 9) }, seen);
    }

    [Fact]
    public void Returns_null_when_nothing_changed()
        => Assert.Null(BundleAskRewrite.LowerAsks("B/R/16 1 0 18 2 0", CapAtFive));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Name/Reward")]               // no ingredient field
    [InlineData("Name/Reward/16 9")]          // fewer than one full ingredient
    [InlineData("Name/Reward/16 9 0 18 9")]   // token count not a multiple of three
    public void Malformed_values_are_left_alone(string? value)
        => Assert.Null(BundleAskRewrite.LowerAsks(value!, CapAtFive));

    [Fact]
    public void A_non_numeric_stack_is_skipped_and_the_rest_still_clamp()
    {
        string? lowered = BundleAskRewrite.LowerAsks("B/R/16 lots 0 18 9 0", CapAtFive);
        Assert.Equal("B/R/16 lots 0 18 5 0", lowered);
    }

    [Fact]
    public void Extra_spaces_in_the_ingredient_field_are_tolerated()
        => Assert.Equal("B/R/16 5 0", BundleAskRewrite.LowerAsks("B/R/ 16  9 0 ", CapAtFive));

    [Fact]
    public void A_null_clamp_throws()
        => Assert.Throws<ArgumentNullException>(() => BundleAskRewrite.LowerAsks("B/R/16 9 0", null!));
}
