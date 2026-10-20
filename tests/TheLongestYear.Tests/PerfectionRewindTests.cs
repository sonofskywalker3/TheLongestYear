using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class PerfectionRewindTests
{
    [Fact]
    public void Every_percentGameComplete_term_is_listed_once()
    {
        // Utility.percentGameComplete (1.6) adds eleven terms whose weights total 100.
        Assert.Equal(11, PerfectionRewind.Inputs.Count);
        Assert.Equal(100, PerfectionRewind.Inputs.Sum(i => i.Weight));
        Assert.Equal(PerfectionRewind.Inputs.Count, PerfectionRewind.Inputs.Select(i => i.Name).Distinct().Count());
    }

    [Fact]
    public void No_input_survives_the_rewind()
    {
        Assert.Empty(PerfectionRewind.Inputs.Where(i => i.Handling == PerfectionRewind.Handling.Persists));
    }

    [Theory]
    [InlineData("Fish caught", "fishCaught")]
    [InlineData("Cooking", "recipesCooked")]
    [InlineData("Monster Slayer", "hasCompletedAllMonsterSlayerQuests")]
    [InlineData("Shipped", "basicShipped")]
    public void Collections_ruled_on_2026_10_20_are_cleared_with_no_keep(string name, string record)
    {
        PerfectionRewind.Input input = PerfectionRewind.Inputs.Single(i => i.Name == name);
        Assert.Contains(record, input.Records);
        Assert.Equal(PerfectionRewind.Handling.Cleared, input.Handling);
    }

    [Theory]
    [InlineData("Farmer level")]
    [InlineData("Stardrops")]
    public void Bought_keeps_are_the_only_carry_over(string name)
    {
        Assert.Equal(PerfectionRewind.Handling.ClearedExceptBoughtKeep,
            PerfectionRewind.Inputs.Single(i => i.Name == name).Handling);
    }

    [Fact]
    public void Whole_fish_collection_is_cleared_not_just_legendaries()
    {
        var caught = new[] { "(O)163", "(O)128", "(O)682", "(O)145" };
        Assert.Equal(caught.OrderBy(x => x), PerfectionRewind.FishIdsToClear(caught).OrderBy(x => x));
    }

    [Fact]
    public void Every_cooked_dish_is_cleared()
    {
        var cooked = new[] { "194", "196", "(O)210" };
        Assert.Equal(cooked.OrderBy(x => x), PerfectionRewind.CookedIdsToClear(cooked).OrderBy(x => x));
    }

    [Fact]
    public void Empty_records_clear_nothing()
    {
        Assert.Empty(PerfectionRewind.FishIdsToClear(System.Array.Empty<string>()));
        Assert.Empty(PerfectionRewind.CookedIdsToClear(System.Array.Empty<string>()));
    }
}
