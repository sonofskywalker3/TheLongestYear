using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeightedSamplerTests
{
    private static PoolItem Item(string id, int weight = 1)
        => new(id, 10, weight, new List<Season>(), new List<string>());

    private static readonly IReadOnlyList<PoolItem> Pool = new[]
    {
        Item("(O)1"), Item("(O)2"), Item("(O)3"),
        Item("(O)night1"), Item("(O)night2"), Item("(O)night3"),
    };

    private static bool IsNight(PoolItem p) => p.ItemId.Contains("night");

    [Fact]
    public void Same_seed_gives_the_same_picks()
    {
        var a = WeightedSampler.Sample(Pool, 4, new Random(7));
        var b = WeightedSampler.Sample(Pool, 4, new Random(7));
        Assert.Equal(a.Select(p => p.ItemId), b.Select(p => p.ItemId));
    }

    [Fact]
    public void Picks_are_distinct_and_stop_when_the_pool_runs_out()
    {
        var picked = WeightedSampler.Sample(Pool, 10, new Random(1));
        Assert.Equal(Pool.Count, picked.Count);
        Assert.Equal(Pool.Count, picked.Select(p => p.ItemId).Distinct().Count());
    }

    [Fact]
    public void A_zero_weight_item_is_never_picked_while_others_remain()
    {
        var pool = new[] { Item("(O)never", 0), Item("(O)a"), Item("(O)b") };
        for (int seed = 0; seed < 50; seed++)
            Assert.DoesNotContain(WeightedSampler.Sample(pool, 2, new Random(seed)), p => p.ItemId == "(O)never");
    }

    [Fact]
    public void Group_cap_reached_partway_drops_the_rest_of_the_group()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var picked = WeightedSampler.Sample(Pool, 6, new Random(seed), IsNight, cap: 1);

            Assert.Equal(1, picked.Count(IsNight));
            // Capacity is the three uncapped items plus one from the group.
            Assert.Equal(4, picked.Count);
        }
    }

    [Fact]
    public void Cap_of_two_allows_two_from_the_group()
    {
        var picked = WeightedSampler.Sample(Pool, 6, new Random(3), IsNight, cap: 2);
        Assert.Equal(2, picked.Count(IsNight));
        Assert.Equal(5, picked.Count);
    }

    [Fact]
    public void Without_a_cap_the_rng_stream_matches_the_plain_call()
    {
        var plain = WeightedSampler.Sample(Pool, 3, new Random(11));
        var nullCap = WeightedSampler.Sample(Pool, 3, new Random(11), capped: null);
        Assert.Equal(plain.Select(p => p.ItemId), nullCap.Select(p => p.ItemId));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    [InlineData(3, 6)]
    [InlineData(9, 6)]
    public void Capacity_counts_uncapped_items_plus_the_cap(int cap, int expected)
        => Assert.Equal(expected, WeightedSampler.Capacity(Pool, IsNight, cap));

    [Fact]
    public void Capacity_without_a_group_is_the_pool_size()
        => Assert.Equal(Pool.Count, WeightedSampler.Capacity(Pool, null, 0));
}
