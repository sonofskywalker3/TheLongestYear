using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RecipeSourcesTests
{
    private static readonly IReadOnlySet<string> VanillaItems = new HashSet<string> { "(O)128", "(O)194" };
    private static readonly IReadOnlySet<string> VanillaKeys = new HashSet<string> { "Fried Egg", "Pumpkin Soup" };

    private static string Label(string key) => key switch
    {
        RecipeSources.Vanilla => "Stardew Valley",
        RecipeSources.OtherMods => "Other mods",
        RecipeSources.All => "All",
        "Cornucopia.MoreCrops" => "Cornucopia More Crops",
        _ => "Seven Deadly Sins",
    };

    private static IReadOnlyDictionary<string, string> Owners() => RecipeSources.ItemOwners(new[]
    {
        ("Cornucopia.MoreCrops", (IEnumerable<string>)new[] { "(O)Cornucopia_AsparagusSoup", "(O)128" }),
        ("ricehit.SevenDeadlySinsCP", (IEnumerable<string>)new[] { "(O)SDS_chocolate", "(O)Cornucopia_AsparagusSoup" }),
    }, VanillaItems);

    [Fact]
    public void A_base_game_item_a_layer_only_changes_is_not_the_mods()
        => Assert.False(Owners().ContainsKey("(O)128"));

    [Fact]
    public void The_first_layer_wins_an_item_two_layers_add()
        => Assert.Equal("Cornucopia.MoreCrops", Owners()["(O)Cornucopia_AsparagusSoup"]);

    [Theory]
    [InlineData("Pumpkin Soup", "(O)199", RecipeSources.Vanilla)]
    [InlineData("Cornucopia_AsparagusSoup", "(O)Cornucopia_AsparagusSoup", "Cornucopia.MoreCrops")]
    [InlineData("SDS_chocolate", "(O)SDS_chocolate", "ricehit.SevenDeadlySinsCP")]
    [InlineData("Someone_Pie", "(O)Someone_Pie", RecipeSources.OtherMods)]
    [InlineData("Someone_Omelet", "(O)194", RecipeSources.OtherMods)]
    [InlineData("Someone_Broken", null, RecipeSources.OtherMods)]
    public void Each_recipe_goes_to_its_source(string key, string? output, string expected)
        => Assert.Equal(expected, RecipeSources.SourceOf(key, output, VanillaKeys, Owners()));

    [Fact]
    public void Tabs_put_the_base_game_first_then_mods_then_other_then_all_each_A_to_Z()
    {
        var tabs = RecipeSources.Tabs(new[]
        {
            ("o1", "Zesty Pie", RecipeSources.OtherMods),
            ("s1", "Love Chocolate", "ricehit.SevenDeadlySinsCP"),
            ("v2", "Pumpkin Soup", RecipeSources.Vanilla),
            ("c1", "Asparagus Soup", "Cornucopia.MoreCrops"),
            ("v1", "fried egg", RecipeSources.Vanilla),
        }, Label);
        Assert.Equal(new[] { "Stardew Valley", "Cornucopia More Crops", "Seven Deadly Sins", "Other mods", "All" },
            tabs.Select(t => t.Label));
        Assert.Equal(new[] { "v1", "v2" }, tabs[0].RecipeIds);
        Assert.Equal(new[] { "c1", "v1", "s1", "v2", "o1" }, tabs[^1].RecipeIds);
    }

    [Theory]
    [InlineData("Seven.Deadly.Sins-CP", "Seven Deadly Sins")]
    [InlineData("[CP] Cornucopia More Crops", "Cornucopia More Crops")]
    [InlineData("Cornucopia - More Crops", "Cornucopia - More Crops")]
    [InlineData("Stardew Valley Expanded", "Stardew Valley Expanded")]
    [InlineData("Sunberry Village (CP)", "Sunberry Village")]
    [InlineData("East_Scarp", "East Scarp")]
    [InlineData("[CP]", "[CP]")]
    public void Tab_names_drop_pack_tags_and_read_dots_as_spaces(string mod, string expected)
        => Assert.Equal(expected, RecipeSources.TabName(mod));

    [Fact]
    public void One_source_is_one_tab_with_no_All()
    {
        var tabs = RecipeSources.Tabs(new[] { ("v2", "Pumpkin Soup", RecipeSources.Vanilla), ("v1", "Fried Egg", RecipeSources.Vanilla) }, Label);
        Assert.Single(tabs);
        Assert.Equal(new[] { "v1", "v2" }, tabs[0].RecipeIds);
    }

    [Fact]
    public void The_picker_opens_on_the_last_tab_chosen_else_the_base_game()
    {
        var tabs = RecipeSources.Tabs(new[]
        {
            ("c1", "Asparagus Soup", "Cornucopia.MoreCrops"),
            ("v1", "Fried Egg", RecipeSources.Vanilla),
        }, Label);
        Assert.Equal(1, RecipeSources.StartTab(tabs, "Cornucopia.MoreCrops"));
        Assert.Equal(0, RecipeSources.StartTab(tabs, "ricehit.SevenDeadlySinsCP"));
        Assert.Equal(0, RecipeSources.StartTab(tabs, null));
    }
}
