using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusChainsTests
{
    [Fact]
    public void Every_rung_id_is_in_the_catalog()
        => Assert.All(FocusChains.All.SelectMany(c => c.Rungs), id => Assert.NotNull(UpgradeCatalog.TryGet(id)));

    [Fact]
    public void No_gift_skill_or_book_chain_is_a_focus_chain()
        => Assert.DoesNotContain(FocusChains.All.SelectMany(c => c.Rungs),
            id => id.StartsWith("cookbook_") || id.StartsWith("craftbook_") || id.Contains("_level_")
                  || UpgradeCatalog.TryGet(id)!.Category == UpgradeCategory.Gifts);

    [Fact]
    public void Tools_has_five_chains_buildings_six_knacks_ten()
    {
        Assert.Equal(5, FocusChains.ForTheme(FocusTheme.Tools).Count);
        Assert.Equal(6, FocusChains.ForTheme(FocusTheme.Buildings).Count);
        Assert.Equal(10, FocusChains.ForTheme(FocusTheme.Knacks).Count);
    }

    [Fact]
    public void Chain_ids_are_unique_and_look_up()
    {
        Assert.Equal(FocusChains.All.Count, FocusChains.All.Select(c => c.Id).Distinct().Count());
        Assert.All(FocusChains.All, c => Assert.Same(c, FocusChains.TryGet(c.Id)));
        Assert.Null(FocusChains.TryGet("nope"));
    }
}
