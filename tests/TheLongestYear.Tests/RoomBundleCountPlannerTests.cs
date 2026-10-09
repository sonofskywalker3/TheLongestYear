using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-10-09-bundle-count-dial, "Picking".</summary>
public class RoomBundleCountPlannerTests
{
    private static BundleSpec B(string name, int index) => new(
        "Pantry", index, name, name, "O 495 30", 0, 1,
        new[] { new BundleSlotSpec("24", 1, 0) });

    /// <summary>Vanilla-shaped Pantry: six positions, each with a couple of alternates.</summary>
    private static IReadOnlyList<IReadOnlyList<BundleSpec>> Positions() => new[]
    {
        (IReadOnlyList<BundleSpec>)new[] { B("Spring Crops", 0) },
        new[] { B("Summer Crops", 1) },
        new[] { B("Fall Crops", 2) },
        new[] { B("Quality Crops", 3), B("Rare Crops", 3) },
        new[] { B("Animal", 4), B("Brewer's", 4) },
        new[] { B("Artisan", 5), B("Fish Farmer's", 5), B("Garden", 5) },
    };

    private static IReadOnlyList<BundleSpec> Picks(IReadOnlyList<IReadOnlyList<BundleSpec>> positions)
        => positions.Select(p => p[0]).ToList();

    private static Func<int> Counter(int start = ReservedBundleIndices.First)
    {
        int next = start;
        return () => next++;
    }

    [Fact]
    public void Same_count_returns_the_picks_unchanged()
    {
        var picks = Picks(Positions());
        var planned = RoomBundleCountPlanner.Plan(picks, Positions(), 6, new Random(1), Counter(), out int shortfall);
        Assert.Same(picks, planned);
        Assert.Equal(0, shortfall);
    }

    [Fact]
    public void Fewer_drops_picks_but_never_a_seasonal_crops_bundle()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var planned = RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 4, new Random(seed), Counter(), out _);
            Assert.Equal(4, planned.Count);
            Assert.Contains(planned, b => b.Name == "Spring Crops");
            Assert.Contains(planned, b => b.Name == "Summer Crops");
            Assert.Contains(planned, b => b.Name == "Fall Crops");
        }
    }

    [Fact]
    public void Fewer_keeps_the_original_order_and_indices()
    {
        var planned = RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 4, new Random(3), Counter(), out _);
        var indices = planned.Select(b => b.Index).ToList();
        Assert.Equal(indices.OrderBy(i => i), indices);
        Assert.All(planned, b => Assert.InRange(b.Index, 0, 5));
    }

    [Fact]
    public void Fewer_drops_different_bundles_on_different_seeds()
    {
        var kept = Enumerable.Range(0, 30)
            .Select(s => string.Join(",", RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 5, new Random(s), Counter(), out _).Select(b => b.Name)))
            .Distinct()
            .Count();
        Assert.True(kept > 1);
    }

    [Fact]
    public void Fewer_falls_back_to_crops_only_when_nothing_else_can_go()
    {
        var positions = new[]
        {
            (IReadOnlyList<BundleSpec>)new[] { B("Spring Crops", 0) },
            new[] { B("Summer Crops", 1) },
            new[] { B("Fall Crops", 2) },
        };
        var planned = RoomBundleCountPlanner.Plan(Picks(positions), positions, 2, new Random(0), Counter(), out _);
        Assert.Equal(2, planned.Count);
    }

    [Fact]
    public void More_adds_new_names_from_the_room_pool_with_reserved_indices()
    {
        var planned = RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 8, new Random(5), Counter(), out int shortfall);

        Assert.Equal(8, planned.Count);
        Assert.Equal(0, shortfall);
        Assert.Equal(planned.Count, planned.Select(b => b.Name).Distinct().Count());
        var extras = planned.Skip(6).ToList();
        Assert.Equal(new[] { ReservedBundleIndices.First, ReservedBundleIndices.First + 1 }, extras.Select(b => b.Index));
        Assert.All(extras, b => Assert.Contains(b.Name, new[] { "Rare Crops", "Brewer's", "Fish Farmer's", "Garden" }));
        Assert.All(extras, b => Assert.Equal("Pantry", b.Room));
    }

    [Fact]
    public void More_stops_short_when_the_pool_runs_out_of_names_and_says_how_many()
    {
        // 10 distinct names in the pool; Extreme asks for 9 so it fits. Ask for 12 to run out.
        var planned = RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 12, new Random(5), Counter(), out int shortfall);

        Assert.Equal(10, planned.Count);
        Assert.Equal(2, shortfall);
        Assert.Equal(10, planned.Select(b => b.Name).Distinct().Count());
    }

    [Fact]
    public void A_short_room_takes_no_reserved_index_it_does_not_use()
    {
        var positions = new[] { (IReadOnlyList<BundleSpec>)new[] { B("A", 20) }, new[] { B("B", 21) } };
        Func<int> next = Counter();
        var planned = RoomBundleCountPlanner.Plan(Picks(positions), positions, 9, new Random(0), next, out int shortfall);

        Assert.Equal(2, planned.Count);
        Assert.Equal(7, shortfall);
        Assert.Equal(ReservedBundleIndices.First, next());
    }

    [Fact]
    public void Same_seed_same_plan()
    {
        string Run() => string.Join(",", RoomBundleCountPlanner.Plan(Picks(Positions()), Positions(), 9, new Random(42), Counter(), out _)
            .Select(b => $"{b.Name}@{b.Index}"));
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Reserved_indices_start_at_9000_and_skip_used_ones()
    {
        var reserved = new ReservedBundleIndices(new[] { 3, 9000, 9002 });
        Assert.Equal(9001, reserved.Next());
        Assert.Equal(9003, reserved.Next());
        Assert.Equal(9004, reserved.Next());
    }

    [Fact]
    public void Reserved_range_cannot_meet_vanilla()
    {
        int highestVanilla = VanillaBundleBoard.Standard.Keys.Max(k => int.Parse(k.Split('/')[1]));
        Assert.True(ReservedBundleIndices.First > highestVanilla);
        Assert.True(ReservedBundleIndices.IsReserved(ReservedBundleIndices.First));
        Assert.False(ReservedBundleIndices.IsReserved(highestVanilla));
    }
}
