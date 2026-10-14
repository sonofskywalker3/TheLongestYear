using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>A RANDOM_ITEMS range narrowed by the line's PerItemCondition offers only the items that
/// pass the condition. Read without it, a mod's "dig up any object 0-900 tagged fossil" line claimed
/// all 901 ids as artifact-spot proof (and as machine outputs).</summary>
public class ItemQueryIdsFilterTests
{
    private const string Range = "RANDOM_ITEMS (O) 100 110";
    private const string Condition = "ITEM_CONTEXT_TAG Target fossil_item";

    [Fact]
    public void UnfilteredRange_StillExpands()
    {
        Assert.Equal(11, ItemQueryIds.Expand(Range).Count);
        Assert.Equal(11, ItemQueryIds.Expand(Range, null).Count);
        Assert.Equal(11, ItemQueryIds.Expand(Range, "  ").Count);
        Assert.False(ItemQueryIds.NeedsItemFilter(Range, null));
    }

    [Fact]
    public void FilteredRange_ExpandsToNothing_AndAsksTheGameToFilter()
    {
        Assert.Empty(ItemQueryIds.Expand(Range, Condition));
        Assert.True(ItemQueryIds.NeedsItemFilter(Range, Condition));
    }

    [Fact]
    public void PlainIdsAndUnreadableQueries_AreUnchangedByACondition()
    {
        Assert.Equal(new[] { "(O)110" }, ItemQueryIds.Expand("(O)110", Condition));
        Assert.False(ItemQueryIds.NeedsItemFilter("(O)110", Condition));
        Assert.False(ItemQueryIds.NeedsItemFilter("RANDOM_ITEMS (F) 0 100", Condition));
        Assert.Empty(ItemQueryIds.Expand("RANDOM_ITEMS (O)", Condition));
    }
}
