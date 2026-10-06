using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CardMultiplierTests
{
    [Fact]
    public void Off_is_always_one()
        => Assert.All(Enumerable.Range(0, 50), s => Assert.Equal(1.0, CardMultiplier.For(s, 3, Theme.Mining, false)));

    [Fact]
    public void Random_multiplier_stays_in_half_to_one_and_a_half_in_steps_of_five_hundredths()
    {
        var seen = Enumerable.Range(0, 2000).Select(s => CardMultiplier.For(s, 3, Theme.Mining, true)).ToList();
        Assert.All(seen, m => { Assert.InRange(m, 0.5, 1.5); Assert.Equal(0, Math.Round(m * 100) % 5); });
        Assert.Contains(0.5, seen); Assert.Contains(1.5, seen);
    }

    [Fact]
    public void Mystery_weeks_are_about_one_in_four()
    {
        int n = Enumerable.Range(0, 4000).Count(s => CardMultiplier.IsMysteryWeek(s, 6, true));
        Assert.InRange(n, 800, 1200);
    }

    [Fact]
    public void No_mystery_when_the_option_is_off()
        => Assert.DoesNotContain(true, Enumerable.Range(0, 500).Select(s => CardMultiplier.IsMysteryWeek(s, 6, false)));

    [Fact]
    public void The_mystery_card_always_pays_one_and_a_quarter_to_one_and_three_quarters()
        => Assert.All(Enumerable.Range(0, 1000), s => Assert.InRange(CardMultiplier.Mystery(s, 2, Theme.Farming), 1.25, 1.75));

    [Fact]
    public void ForCard_gives_the_sealed_slot_the_mystery_value()
    {
        var r = new RandomizerSettings { MysteryCard = true };
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        int sealedSlot = CardMultiplier.SealedSlot(seed, 5);
        Assert.Equal(CardMultiplier.Mystery(seed, 5, Theme.Fishing), CardMultiplier.ForCard(seed, 5, Theme.Fishing, sealedSlot, r));
        Assert.Equal(1.0, CardMultiplier.ForCard(seed, 5, Theme.Fishing, 1 - sealedSlot, r));
    }

    [Fact]
    public void An_old_save_pays_one_times()
        => Assert.Equal(1.0, System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.CurrentGoalMultiplier);

    [Fact]
    public void A_new_pick_starts_at_one_times()
    {
        var run = new RunState { CurrentGoalMultiplier = 1.4 };
        run.Select(Theme.Mining);
        Assert.Equal(1.0, run.CurrentGoalMultiplier);
    }

    [Fact]
    public void The_pre_pick_multiplier_carries_into_the_new_month_once()
    {
        var run = new RunState { NextMonthSelection = Theme.Fishing, NextMonthGoalMultiplier = 1.35 };
        run.BeginNewMonth(Season.Summer);
        Assert.Equal(Theme.Fishing, run.CurrentSelection);
        Assert.Equal(1.35, run.CurrentGoalMultiplier);
        Assert.Equal(1.0, run.NextMonthGoalMultiplier);
    }

    [Fact]
    public void A_new_month_without_a_pre_pick_pays_one_times()
    {
        var run = new RunState { CurrentGoalMultiplier = 0.6, NextMonthGoalMultiplier = 1.2 };
        run.BeginNewMonth(Season.Summer);
        Assert.Equal(1.0, run.CurrentGoalMultiplier);
        Assert.Equal(1.0, run.NextMonthGoalMultiplier);
    }

    [Fact]
    public void A_new_run_resets_both_multipliers()
    {
        var run = new RunState { CurrentGoalMultiplier = 0.6, NextMonthGoalMultiplier = 1.2 };
        run.BeginNewRun(42);
        Assert.Equal(1.0, run.CurrentGoalMultiplier);
        Assert.Equal(1.0, run.NextMonthGoalMultiplier);
    }

    [Fact]
    public void Rerolling_keeps_the_same_slot_sealed()
    {
        // The sealed slot depends on seed and week only, never on the themes shown.
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 9, true));
        int slot = CardMultiplier.SealedSlot(seed, 9);
        Assert.All(Enumerable.Range(0, 20), _ => Assert.Equal(slot, CardMultiplier.SealedSlot(seed, 9)));
    }
}
