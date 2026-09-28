using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

// Nexus posts, Nijah 2026-09-28: the hub's Re-roll Themes button kept showing the same pair.
public class RerollCycleTests
{
    private static readonly Theme[] Four = { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining };

    [Fact]
    public void Pair_key_is_the_two_names_sorted()
    {
        Assert.Equal("Farming|Fishing", RerollCycle.PairKey(new[] { Theme.Fishing, Theme.Farming }));
        Assert.Equal("Farming|Fishing", RerollCycle.PairKey(new[] { Theme.Farming, Theme.Fishing }));
    }

    [Fact]
    public void No_pair_repeats_until_every_pair_has_been_shown()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            var rng = new Random(seed);
            var seen = new List<string>();
            IReadOnlyList<Theme> current = new[] { Theme.Foraging, Theme.Farming };
            seen.Add(RerollCycle.PairKey(current));
            var shown = new HashSet<string> { RerollCycle.PairKey(current) };

            // 4 candidates = 6 pairs; the initial offer is one, so 5 rerolls show the other 5.
            for (int i = 0; i < 5; i++)
            {
                current = RerollCycle.Next(Four, seen, current, rng);
                Assert.Equal(2, current.Count);
                Assert.NotEqual(current[0], current[1]);
                Assert.True(shown.Add(RerollCycle.PairKey(current)), $"seed {seed}: repeated {RerollCycle.PairKey(current)}");
            }
            Assert.Equal(6, shown.Count);
        }
    }

    [Fact]
    public void After_a_full_cycle_it_starts_over_but_never_reshows_the_pair_on_screen()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            var rng = new Random(seed);
            var seen = new List<string>();
            IReadOnlyList<Theme> current = new[] { Theme.Foraging, Theme.Farming };
            seen.Add(RerollCycle.PairKey(current));
            for (int i = 0; i < 5; i++)
                current = RerollCycle.Next(Four, seen, current, rng);

            string onScreen = RerollCycle.PairKey(current);
            IReadOnlyList<Theme> next = RerollCycle.Next(Four, seen, current, rng);
            Assert.NotEqual(onScreen, RerollCycle.PairKey(next));

            // The new cycle again shows every other pair before any repeat.
            var shown = new HashSet<string> { onScreen, RerollCycle.PairKey(next) };
            for (int i = 0; i < 4; i++)
            {
                next = RerollCycle.Next(Four, seen, next, rng);
                Assert.True(shown.Add(RerollCycle.PairKey(next)));
            }
        }
    }

    [Fact]
    public void Two_candidates_keep_returning_the_only_pair()
    {
        var seen = new List<string>();
        IReadOnlyList<Theme> current = new[] { Theme.Farming, Theme.Fishing };
        seen.Add(RerollCycle.PairKey(current));
        var next = RerollCycle.Next(new[] { Theme.Farming, Theme.Fishing }, seen, current, new Random(3));
        Assert.Equal("Farming|Fishing", RerollCycle.PairKey(next));
    }

    [Fact]
    public void Three_candidates_break_the_same_two_themes_loop()
    {
        // The reported bug: two themes qualified, a third could ask one goal. The reroll must reach it.
        var candidates = new[] { Theme.Farming, Theme.Fishing, Theme.Kitchen };
        var seen = new List<string>();
        IReadOnlyList<Theme> current = new[] { Theme.Farming, Theme.Fishing };
        seen.Add(RerollCycle.PairKey(current));
        IReadOnlyList<Theme> next = RerollCycle.Next(candidates, seen, current, new Random(9));
        Assert.Contains(Theme.Kitchen, next);
    }

    [Fact]
    public void Fewer_than_two_candidates_return_what_exists()
    {
        Assert.Equal(new[] { Theme.Mining }, RerollCycle.Next(new[] { Theme.Mining }, new List<string>(), Array.Empty<Theme>(), new Random(1)));
        Assert.Empty(RerollCycle.Next(Array.Empty<Theme>(), new List<string>(), Array.Empty<Theme>(), new Random(1)));
    }

    [Fact]
    public void Next_is_deterministic_for_the_rng()
    {
        var a = RerollCycle.Next(Four, new List<string> { "Farming|Foraging" }, new[] { Theme.Foraging, Theme.Farming }, new Random(42));
        var b = RerollCycle.Next(Four, new List<string> { "Farming|Foraging" }, new[] { Theme.Foraging, Theme.Farming }, new Random(42));
        Assert.Equal(a, b);
    }
}
