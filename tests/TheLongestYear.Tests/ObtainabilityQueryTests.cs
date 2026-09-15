using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityQueryTests
{
    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)775"] = new ObjInfo("(O)775", "Glacierfish", -4, 1000, new string[0], true),
        ["(O)800"] = new ObjInfo("(O)800", "Blobfish", -4, 900, new string[0], false),
        ["(O)Moss"] = new ObjInfo("(O)Moss", "Moss", -81, 5, new string[0], false),
    };

    [Fact]
    public void A_plain_id_is_normalized_and_dependable()
    {
        QueryResult r = ItemQueries.Resolve("24", Objects);
        Assert.Equal(new[] { "(O)24" }, r.ItemIds);
        Assert.False(r.Chance || r.Unresolved);
    }

    [Fact]
    public void Random_items_expand_by_range_and_flags_as_chance()
    {
        QueryResult r = ItemQueries.Resolve("RANDOM_ITEMS (O) 2 789 @isRandomSale @requirePrice", Objects);
        Assert.Equal(new[] { "(O)24" }, r.ItemIds);   // 775 excluded from sale, 800 out of range, Moss not numeric
        Assert.True(r.Chance);
    }

    [Fact]
    public void Flavored_items_resolve_to_their_base_object()
    {
        Assert.Equal(new[] { "(O)348" }, ItemQueries.Resolve("FLAVORED_ITEM Wine DROP_IN_ID", Objects).ItemIds);
        Assert.Equal(new[] { "(O)DriedMushrooms" }, ItemQueries.Resolve("FLAVORED_ITEM DriedMushroom 404", Objects).ItemIds);
        Assert.Equal("(O)447", ItemQueries.FlavoredBaseId("AgedRoe"));
        Assert.Null(ItemQueries.FlavoredBaseId("Mystery"));
    }

    [Fact]
    public void Book_and_note_queries_yield_their_fallback_and_say_so()
    {
        QueryResult book = ItemQueries.Resolve("LOST_BOOK_OR_ITEM (O)390", Objects);
        Assert.Equal(new[] { "(O)102", "(O)390" }, book.ItemIds);
        QueryResult note = ItemQueries.Resolve("SECRET_NOTE_OR_ITEM (O)390", Objects);
        Assert.Contains("(O)390", note.ItemIds);
        Assert.Contains("secret note", note.Note);
    }

    [Fact]
    public void Any_text_with_arguments_is_a_query_even_from_a_mod()
    {
        Assert.True(ItemQueries.IsQuery("MYMOD_SPECIAL_ITEM 3 4"));
        Assert.False(ItemQueries.IsQuery("(O)24"));
        Assert.False(ItemQueries.IsQuery("DeluxeBait"));
        QueryResult r = ItemQueries.Resolve("MYMOD_SPECIAL_ITEM 3 4", Objects);
        Assert.True(r.Unresolved);
        Assert.Empty(r.ItemIds);
    }

    [Fact]
    public void Unknown_queries_are_unresolved_and_emitted_under_the_marker()
    {
        QueryResult r = ItemQueries.Resolve("LOCATION_FISH Beach BOBBER_X", Objects);
        Assert.True(r.Unresolved);
        Assert.Empty(r.ItemIds);
        var template = new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "Forage at Beach");
        var emitted = ItemQueries.Emit("LOCATION_FISH Beach BOBBER_X", Objects, template).Single();
        Assert.StartsWith(ItemQueries.UnresolvedPrefix, emitted.ItemId);
        Assert.Equal(SourceKind.Other, emitted.Source.Kind);
        Assert.True(emitted.Source.Conditions.Unresolved);
    }
}
