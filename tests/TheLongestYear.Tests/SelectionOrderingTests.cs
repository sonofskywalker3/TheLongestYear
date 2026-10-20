using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Every theme not picked this month that can ask for at least one goal is dealt before
/// any theme picked this month comes back, in the weekly offer and in re-rolls.</summary>
public class SelectionOrderingTests
{
    private const int Seeds = 200;
    private static readonly Theme[] AllThemes = (Theme[])Enum.GetValues(typeof(Theme));

    [Fact]
    public void An_unpicked_thin_activity_theme_beats_a_picked_full_one()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Kitchen => 1, Theme.Farming => 3, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(new[] { Theme.Artisan, Theme.Kitchen }, SelectionService.OfferForWeek(seed, 6, new[] { Theme.Farming }, Ask));
    }

    [Fact]
    public void An_unpicked_one_goal_theme_is_dealt_before_the_refill()
    {
        // Review example: Kitchen (unpicked, 1 goal) and Farming (picked, 1 goal). Kitchen must show.
        int Ask(Theme t) => t switch { Theme.Kitchen => 1, Theme.Farming => 1, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(new[] { Theme.Kitchen, Theme.Farming }, SelectionService.OfferForWeek(seed, 3, new[] { Theme.Farming }, Ask));
    }

    [Fact]
    public void An_unpicked_thin_room_theme_beats_a_picked_full_one()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Fishing => 1, Theme.Mixed => 5, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(new[] { Theme.Artisan, Theme.Fishing }, SelectionService.OfferForWeek(seed, 8, new[] { Theme.Mixed }, Ask));
    }

    [Fact]
    public void Unpicked_themes_always_come_before_picked_ones_for_any_counts()
    {
        var gen = new Random(12345);
        for (int run = 0; run < 2000; run++)
        {
            var counts = AllThemes.ToDictionary(t => t, _ => gen.Next(4) == 0 ? 0 : gen.Next(4));
            var picked = AllThemes.Where(_ => gen.Next(3) == 0).ToList();
            int week = 1 + gen.Next(16);
            var offer = SelectionService.OfferForWeek(gen.Next(), week, picked, t => counts[t]);

            var unpickedAskable = AllThemes.Where(t => !picked.Contains(t) && counts[t] >= 1).ToList();
            int expectedUnpicked = Math.Min(SelectionService.OfferSize, unpickedAskable.Count);
            Assert.Equal(expectedUnpicked, offer.Count(t => !picked.Contains(t)));
            Assert.Equal(offer.Count, offer.Distinct().Count());
            Assert.All(offer, t => Assert.True(counts[t] >= 1));
            // A picked card only ever follows the unpicked ones.
            for (int i = 1; i < offer.Count; i++)
                Assert.False(picked.Contains(offer[i - 1]) && !picked.Contains(offer[i]));
        }
    }

    [Fact]
    public void The_activity_pad_is_seeded_and_deterministic()
    {
        int Ask(Theme t) => t is Theme.Kitchen or Theme.Spelunking or Theme.Artisan ? 1 : 0;
        var pairs = new HashSet<string>();
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var a = SelectionService.OfferForWeek(seed, 5, Array.Empty<Theme>(), Ask);
            Assert.Equal(a, SelectionService.OfferForWeek(seed, 5, Array.Empty<Theme>(), Ask));
            Assert.Equal(2, a.Distinct().Count());
            pairs.Add(RerollCycle.PairKey(a));
        }
        Assert.True(pairs.Count > 1);
    }

    [Fact]
    public void A_reroll_keeps_the_only_unpicked_theme_on_every_pair()
    {
        int Ask(Theme t) => t switch { Theme.Kitchen => 1, Theme.Mixed => 3, Theme.Fishing => 3, Theme.Mining => 2, _ => 0 };
        var picked = new[] { Theme.Mixed, Theme.Fishing, Theme.Mining };
        var candidates = SelectionService.Candidates(picked, Ask);
        var seen = new List<string>();
        IReadOnlyList<Theme> current = SelectionService.OfferForWeek(4, 9, picked, Ask);
        Assert.Contains(Theme.Kitchen, current);
        Assert.True(RerollCycle.CanChange(candidates, current, picked));
        var shown = new HashSet<string>();
        for (int n = 1; n <= 30; n++)
        {
            current = RerollCycle.Next(candidates, seen, current, new Random(n), picked);
            Assert.Contains(Theme.Kitchen, current);
            shown.Add(RerollCycle.PairKey(current));
        }
        Assert.Equal(3, shown.Count);
    }

    [Fact]
    public void Reroll_candidates_take_full_picked_themes_before_thin_ones()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Mixed => 3, Theme.Farming => 1, _ => 0 };
        Assert.Equal(new[] { Theme.Artisan, Theme.Mixed },
            SelectionService.Candidates(new[] { Theme.Farming, Theme.Mixed }, Ask));
        // No full picked theme left: the thin one joins.
        int Thin(Theme t) => t switch { Theme.Artisan => 4, Theme.Farming => 1, _ => 0 };
        Assert.Equal(new[] { Theme.Artisan, Theme.Farming },
            SelectionService.Candidates(new[] { Theme.Farming, Theme.Mixed }, Thin));
    }

    [Fact]
    public void A_reroll_that_can_only_show_one_pair_cannot_change()
    {
        int Ask(Theme t) => t switch { Theme.Kitchen => 1, Theme.Mixed => 3, _ => 0 };
        var picked = new[] { Theme.Mixed };
        var candidates = SelectionService.Candidates(picked, Ask);
        Assert.False(RerollCycle.CanChange(candidates, new[] { Theme.Kitchen, Theme.Mixed }, picked));
    }

    [Fact]
    public void A_stored_reroll_with_a_refilled_pick_survives_while_it_can_ask()
    {
        var run = new RunState { Seed = 3 };
        run.Select(Theme.Mixed);
        run.RecordReroll(run.WeekOfYear, new[] { Theme.Kitchen, Theme.Mixed }, 1);

        // Counts moved since the re-roll (Mining now fills a card), but Mixed can still ask.
        int Moved(Theme t) => t switch { Theme.Kitchen => 1, Theme.Mixed => 1, Theme.Mining => 4, _ => 0 };
        Assert.Equal(new[] { Theme.Kitchen, Theme.Mixed },
            run.RerolledOfferFor(run.WeekOfYear, SelectionService.StaleRerollPicks(run.SelectedThemesThisMonth, Moved)));

        // Mixed can ask nothing now: the stored re-roll is dropped.
        int Dry(Theme t) => t == Theme.Kitchen ? 1 : 0;
        Assert.Null(run.RerolledOfferFor(run.WeekOfYear, SelectionService.StaleRerollPicks(run.SelectedThemesThisMonth, Dry)));
    }

    [Fact]
    public void A_refilled_pair_can_be_taken_together_on_a_double_week()
    {
        int Ask(Theme t) => t is Theme.Mixed or Theme.Fishing ? 3 : 0;
        var run = new RunState { Seed = 9 };
        run.Select(Theme.Mixed);
        run.Select(Theme.Fishing);
        var offer = SelectionService.OfferForWeek(run.Seed, run.WeekOfYear, run.SelectedThemesThisMonth, Ask);
        Assert.Equal(2, offer.Count);

        run.Select(offer[0]);
        run.SelectSecond(offer[1]);
        Assert.Equal(offer[0], run.CurrentSelection);
        Assert.Equal(offer[1], run.SecondSelection);
        Assert.Equal(new[] { Theme.Mixed, Theme.Fishing }, run.SelectedThemesThisMonth);
    }
}
