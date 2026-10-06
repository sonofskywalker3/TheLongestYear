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
    public void The_face_down_card_lands_left_and_right_about_evenly()
    {
        // Over weeks that ARE mystery weeks, neither side may dominate (a correlated draw once
        // put the sealed card on the right about 95% of the time).
        var slots = Enumerable.Range(0, 8000)
            .Where(s => CardMultiplier.IsMysteryWeek(s, 7, true))
            .Select(s => CardMultiplier.SealedSlot(s, 7))
            .ToList();
        double right = slots.Count(x => x == 1) / (double)slots.Count;
        Assert.InRange(right, 0.35, 0.65);
    }

    [Fact]
    public void Default_settings_pay_one_times_on_both_cards_even_on_a_mystery_week()
    {
        var r = new RandomizerSettings();
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        Assert.Equal(1.0, CardMultiplier.ForCard(seed, 5, Theme.Fishing, 0, r));
        Assert.Equal(1.0, CardMultiplier.ForCard(seed, 5, Theme.Fishing, 1, r));
    }

    [Fact]
    public void The_offer_log_hides_the_sealed_theme_behind_a_question_mark()
    {
        // Final review I2: the Info log must not name the face-down theme.
        var r = new RandomizerSettings { MysteryCard = true };
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        int sealedSlot = CardMultiplier.SealedSlot(seed, 5);
        var offer = new[] { Theme.Fishing, Theme.Kitchen };
        var labels = CardMultiplier.OfferLabels(offer, seed, 5, r);
        Assert.Equal("?", labels[sealedSlot]);
        Assert.Equal(offer[1 - sealedSlot].ToString(), labels[1 - sealedSlot]);
        Assert.True(CardMultiplier.AnySealed(offer.Length, seed, 5, r));
    }

    [Fact]
    public void The_offer_log_names_both_themes_when_nothing_is_sealed()
    {
        var off = new RandomizerSettings();
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        var offer = new[] { Theme.Fishing, Theme.Kitchen };
        Assert.Equal(new[] { "Fishing", "Kitchen" }, CardMultiplier.OfferLabels(offer, seed, 5, off));
        Assert.False(CardMultiplier.AnySealed(offer.Length, seed, 5, off));
    }
}

public class CardMultiplierDoubleWeekTests
{
    [Fact]
    public void No_card_is_sealed_on_a_double_week_even_when_mystery_would_roll()
    {
        var r = new RandomizerSettings { MysteryCard = true };
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        int slot = CardMultiplier.SealedSlot(seed, 5);
        Assert.True(CardMultiplier.IsSealed(seed, 5, slot, r));
        Assert.False(CardMultiplier.IsSealed(seed, 5, slot, r, doubleWeek: true));
        Assert.Equal(1.0, CardMultiplier.ForCard(seed, 5, Theme.Fishing, slot, r, doubleWeek: true));
        Assert.False(CardMultiplier.AnySealed(2, seed, 5, r, doubleWeek: true));
    }
}
