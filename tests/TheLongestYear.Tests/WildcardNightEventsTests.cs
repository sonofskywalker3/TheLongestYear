using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WildcardNightEventsTests
{
    private const int Seeds = 4000;

    /// <summary>The event that plays when every event in <paramref name="cancels"/> cancels itself.</summary>
    private static string? FirstRunnable(IReadOnlyList<string> order, ISet<string> cancels)
        => order.FirstOrDefault(id => !cancels.Contains(id));

    [Fact]
    public void Every_order_tries_all_five_once_and_repeats_for_the_same_seed_and_week()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var order = WildcardNightEvents.TryOrder(seed, 9);
            Assert.Equal(WildcardNightEvents.All.OrderBy(s => s), order.OrderBy(s => s));
            Assert.Equal(order, WildcardNightEvents.TryOrder(seed, 9));
        }
    }

    [Fact]
    public void Young_farm_cancels_split_evenly_between_the_owl_and_the_capsule()
    {
        // The fairy, the witch and the meteorite often cancel on a young farm. Stepping to the next
        // event in the list handed the owl their share; a random untried fallback splits them.
        var cancels = new HashSet<string> { WildcardNightEvents.Fairy, WildcardNightEvents.Witch, WildcardNightEvents.Meteorite };
        int owl = 0, capsule = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            string? plays = FirstRunnable(WildcardNightEvents.TryOrder(seed, 5), cancels);
            if (plays == WildcardNightEvents.Owl) owl++;
            else if (plays == WildcardNightEvents.Capsule) capsule++;
        }
        Assert.Equal(Seeds, owl + capsule);
        Assert.InRange(owl / (double)Seeds, 0.45, 0.55);
    }

    [Fact]
    public void The_fallback_after_a_cancelled_pick_is_not_always_its_neighbour()
    {
        foreach (string first in WildcardNightEvents.All)
        {
            var seconds = new HashSet<string>();
            for (int seed = 0; seed < Seeds; seed++)
            {
                var order = WildcardNightEvents.TryOrder(seed, 3);
                if (order[0] == first) seconds.Add(order[1]);
            }
            Assert.Equal(WildcardNightEvents.All.Count - 1, seconds.Count);
        }
    }

    [Fact]
    public void Each_event_starts_and_each_slot_is_filled_about_equally()
    {
        var counts = new Dictionary<(int slot, string id), int>();
        for (int seed = 0; seed < Seeds; seed++)
        {
            var order = WildcardNightEvents.TryOrder(seed, 12);
            for (int slot = 0; slot < order.Count; slot++)
                counts[(slot, order[slot])] = counts.GetValueOrDefault((slot, order[slot])) + 1;
        }
        double expected = Seeds / (double)WildcardNightEvents.All.Count;
        foreach (int count in counts.Values)
            Assert.InRange(count, expected * 0.85, expected * 1.15);
    }

    [Fact]
    public void Another_week_can_give_another_order()
    {
        var orders = new HashSet<string>();
        for (int week = 1; week <= 16; week++)
            orders.Add(string.Join(",", WildcardNightEvents.TryOrder(77, week)));
        Assert.True(orders.Count > 1);
    }
}
