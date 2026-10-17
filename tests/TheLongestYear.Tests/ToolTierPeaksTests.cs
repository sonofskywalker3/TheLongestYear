using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ToolTierPeaksTests
{
    [Fact]
    public void Highest_tier_wins_whatever_order_the_tools_are_seen_in()
    {
        var peaks = ToolTierPeaks.Highest(new[] { ("pickaxe", 0), ("pickaxe", 4), ("pickaxe", 2) });
        Assert.Equal(4, ToolTierPeaks.Level(peaks, "pickaxe"));

        peaks = ToolTierPeaks.Highest(new[] { ("pickaxe", 4), ("pickaxe", 0) });
        Assert.Equal(4, ToolTierPeaks.Level(peaks, "pickaxe"));
    }

    [Fact]
    public void A_basic_duplicate_in_the_bag_never_masks_the_upgraded_tool_in_a_chest()
    {
        var peaks = ToolTierPeaks.Highest(new[] { ("hoe", 0), ("axe", 1), ("hoe", 3) });
        Assert.Equal(3, ToolTierPeaks.Level(peaks, "hoe"));
        Assert.Equal(1, ToolTierPeaks.Level(peaks, "axe"));
    }

    [Fact]
    public void A_kind_never_seen_reads_as_the_caller_s_missing_value()
    {
        var peaks = ToolTierPeaks.Highest(new[] { ("hoe", 2) });
        Assert.Equal(0, ToolTierPeaks.Level(peaks, "watering_can"));
        Assert.Equal(ToolTierPeaks.NoRod, ToolTierPeaks.Level(peaks, "fishing_rod", ToolTierPeaks.NoRod));
    }

    [Fact]
    public void A_bamboo_rod_is_a_rod_not_a_missing_one()
    {
        var peaks = ToolTierPeaks.Highest(new[] { ("fishing_rod", 0) });
        Assert.Equal(0, ToolTierPeaks.Level(peaks, "fishing_rod", ToolTierPeaks.NoRod));
    }

    [Fact]
    public void Empty_and_null_sightings_give_an_empty_table()
    {
        Assert.Empty(ToolTierPeaks.Highest(System.Array.Empty<(string, int)>()));
        Assert.Empty(ToolTierPeaks.Highest(null!));
    }
}
