using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ContextTagMatcherTests
{
    private static RawObjectEntry Obj(int category, string name, params string[] tags)
        => new("Basic", category, 10, false, tags, name);

    [Fact]
    public void Category_tags_match_by_category_number()
    {
        Assert.True(ContextTagMatcher.Matches("398", Obj(-79, "Grape"), "category_fruit"));
        Assert.False(ContextTagMatcher.Matches("24", Obj(-75, "Parsnip"), "category_fruit"));
        Assert.True(ContextTagMatcher.Matches("184", Obj(-6, "Milk"), "category_milk"));
    }

    [Fact]
    public void Item_and_id_tags_match_by_name_and_id()
    {
        Assert.True(ContextTagMatcher.Matches("698", Obj(-4, "Sturgeon"), "item_sturgeon"));
        Assert.True(ContextTagMatcher.Matches("795", Obj(-4, "Void Salmon"), "item_void_salmon"));
        Assert.True(ContextTagMatcher.Matches("262", Obj(-75, "Wheat"), "id_o_262"));
        Assert.False(ContextTagMatcher.Matches("262", Obj(-75, "Wheat"), "id_o_304"));
    }

    [Fact]
    public void Other_tags_fall_back_to_the_objects_own_tag_list()
    {
        Assert.True(ContextTagMatcher.Matches("92", Obj(-16, "Sap", "sap_item"), "sap_item"));
        Assert.False(ContextTagMatcher.Matches("92", Obj(-16, "Sap"), "sap_item"));
    }

    /// <summary>The game puts an object's own ContextTags in the same set as the tags it makes from
    /// the name, id and category (ItemContextTagManager 26-101), compared without case, so a listed
    /// item_, id_o_ or category_ tag matches even when the made one differs (Alchemistry's Newt is
    /// named Morghoula.AlchemistryCP_Newt and lists item_newt, which its fish pond asks for).</summary>
    [Theory]
    [InlineData("Morghoula.AlchemistryCP_Newt", -4, "item_newt", "item_newt", true)]
    [InlineData("Morghoula.AlchemistryCP_Newt", -4, "Item_Newt", "item_newt", true)]
    [InlineData("Morghoula.AlchemistryCP_Newt", -4, "item_newt", "id_o_newt", false)]
    [InlineData("Some.Mod_Ore", -15, "id_o_ore", "id_o_ore", true)]
    [InlineData("Some.Mod_Gem", -999, "category_gem", "category_gem", true)]
    [InlineData("Some.Mod_Gem", -999, "category_fish", "category_gem", false)]
    public void Listed_tags_match_whatever_the_made_tag_says(string name, int category, string listed, string tag, bool matches)
        => Assert.Equal(matches, ContextTagMatcher.Matches(name, Obj(category, name, listed), tag));

    [Fact]
    public void IdsMatchingAll_returns_qualified_ids_in_ordinal_order()
    {
        var objects = new Dictionary<string, RawObjectEntry>
        {
            ["398"] = Obj(-79, "Grape"), ["24"] = Obj(-75, "Parsnip"), ["613"] = Obj(-79, "Apple"),
        };
        Assert.Equal(new[] { "(O)398", "(O)613" },
            ContextTagMatcher.IdsMatchingAll(objects, new[] { "category_fruit" }));
    }
}
