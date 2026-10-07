using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

public class TaintedItemsTests
{
    private static TamperRecord Rec(string id) => new() { OldItemId = id };

    [Fact]
    public void Refresh_collects_qualified_ids_and_qualifies_bare_ones()
    {
        var cache = new TaintedItems();
        var ids = cache.Refresh(new List<TamperRecord> { Rec("(O)24"), Rec("78"), Rec("") });
        Assert.Equal(2, ids.Count);
        Assert.Contains("(O)24", ids);
        Assert.Contains("(O)78", ids);
    }

    [Fact]
    public void Refresh_rebuilds_when_the_count_changes_and_empties_after_an_in_place_clear()
    {
        var cache = new TaintedItems();
        var list = new List<TamperRecord> { Rec("(O)24") };
        Assert.Single(cache.Refresh(list));
        list.Add(Rec("(O)90"));
        Assert.Equal(2, cache.Refresh(list).Count);
        list.Clear();
        Assert.Empty(cache.Refresh(list));
    }

    [Fact]
    public void Refresh_returns_the_same_set_instance_when_unchanged()
    {
        var cache = new TaintedItems();
        var list = new List<TamperRecord> { Rec("(O)24") };
        Assert.Same(cache.Refresh(list), cache.Refresh(list));
    }

    [Fact]
    public void Refresh_handles_null()
    {
        Assert.Empty(new TaintedItems().Refresh(null));
    }

    [Fact]
    public void Pulse_stays_between_a_quarter_and_two_thirds()
    {
        for (double t = 0; t < 10000; t += 37)
            Assert.InRange(TaintedItems.Pulse(t), 0.25f, 0.6501f);
    }
}
