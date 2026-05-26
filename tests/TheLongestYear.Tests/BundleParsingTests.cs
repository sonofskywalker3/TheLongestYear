using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleParsingTests
{
    [Fact]
    public void Parse_extracts_room_index_name_slots_and_ingredients()
    {
        // key "Room/index", value: name/reward/ingredients/color/numSlots/sprite/displayName
        var b = BundleParsing.Parse("Pantry/0", "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0/4//Spring Crops");

        Assert.Equal("Pantry", b.Room);
        Assert.Equal(0, b.Index);
        Assert.Equal("Spring Crops", b.Name);
        Assert.Equal(4, b.NumberOfSlots);
        Assert.Equal(4, b.Ingredients.Count);
        Assert.Equal("24", b.Ingredients[0].ItemRef);
        Assert.Equal(1, b.Ingredients[0].Stack);
        Assert.Equal(0, b.Ingredients[0].Quality);
    }

    [Fact]
    public void Parse_defaults_slots_to_ingredient_count_when_field_blank()
    {
        var b = BundleParsing.Parse("Crafts Room/13", "Spring Foraging/O 495 30/16 1 0 18 1 0 20 1 0 22 1 0///");
        Assert.Equal(4, b.NumberOfSlots); // blank slot field -> all ingredients
    }

    [Fact]
    public void ParseIngredients_reads_id_stack_quality_triples()
    {
        var list = BundleParsing.ParseIngredients("24 5 0 -5 1 0 (O)128 1 2").ToList();
        Assert.Equal(3, list.Count);
        Assert.Equal("24", list[0].ItemRef);
        Assert.Equal(5, list[0].Stack);
        Assert.Equal("-5", list[1].ItemRef);
        Assert.Equal("(O)128", list[2].ItemRef);
        Assert.Equal(2, list[2].Quality);
    }

    [Theory]
    [InlineData("-5", true)]
    [InlineData("-777", true)]
    [InlineData("24", false)]
    [InlineData("(O)24", false)]
    public void IsCategoryRef_true_only_for_negative_numbers(string raw, bool expected)
        => Assert.Equal(expected, BundleParsing.IsCategoryRef(raw));

    [Theory]
    [InlineData("24", "(O)24")]
    [InlineData("(O)24", "(O)24")]
    [InlineData("(BC)10", "(BC)10")]
    public void NormalizeItemId_qualifies_bare_object_ids(string raw, string expected)
        => Assert.Equal(expected, BundleParsing.NormalizeItemId(raw));
}
