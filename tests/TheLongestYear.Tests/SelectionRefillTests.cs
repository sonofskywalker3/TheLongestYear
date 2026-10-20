using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>A short or empty weekly offer refills from themes already picked this month that can
/// still ask for goals (player report, 0.19.97: one card in week 1, no hub the next Monday, then a
/// failed gate because no theme left that month could ask for what the season still needed).</summary>
public class SelectionRefillTests
{
    private const int Seeds = 200;

    [Fact]
    public void An_empty_offer_refills_from_themes_picked_this_month()
    {
        // The report: Mixed was picked in week 1 and is the only theme that can still ask anything.
        int Ask(Theme t) => t == Theme.Mixed ? 3 : 0;
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(new[] { Theme.Mixed }, SelectionService.OfferForWeek(seed, 2, new[] { Theme.Mixed }, Ask));
    }

    [Fact]
    public void A_one_card_offer_gets_a_second_card_from_the_picked_themes()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Farming => 2, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 6, new[] { Theme.Farming }, Ask);
            Assert.Equal(2, offer.Count);
            Assert.Equal(Theme.Artisan, offer[0]);
            Assert.Equal(Theme.Farming, offer[1]);
        }
    }

    [Fact]
    public void Refill_takes_picked_themes_with_a_full_card_before_thin_ones()
    {
        // Mixed (3 goals) beats Farming (1 goal) for the empty second card on every seed.
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Mixed => 3, Theme.Farming => 1, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 7, new[] { Theme.Farming, Theme.Mixed, Theme.Fishing }, Ask);
            Assert.Equal(new[] { Theme.Artisan, Theme.Mixed }, offer);
        }
    }

    [Fact]
    public void A_thin_picked_theme_still_refills_when_nothing_better_is_left()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Farming => 1, _ => 0 };
        var offer = SelectionService.OfferForWeek(5, 7, new[] { Theme.Farming, Theme.Fishing }, Ask);
        Assert.Equal(new[] { Theme.Artisan, Theme.Farming }, offer);
    }

    [Fact]
    public void Refill_never_puts_the_same_theme_on_both_cards()
    {
        // Duplicate picks in the month's list (an old save) must not deal one theme twice.
        int Ask(Theme t) => t is Theme.Mixed or Theme.Fishing ? 3 : 0;
        var picked = new[] { Theme.Mixed, Theme.Mixed, Theme.Fishing, Theme.Fishing };
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 3, picked, Ask);
            Assert.Equal(2, offer.Count);
            Assert.Equal(2, offer.Distinct().Count());
        }
    }

    [Fact]
    public void Refill_is_deterministic_for_a_seed_and_week()
    {
        int Ask(Theme t) => t switch { Theme.Mixed => 3, Theme.Fishing => 2, Theme.Mining => 5, Theme.Kitchen => 1, _ => 0 };
        var picked = new[] { Theme.Mixed, Theme.Fishing, Theme.Mining, Theme.Kitchen };
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 4, picked, Ask);
            Assert.Equal(offer, SelectionService.OfferForWeek(seed, 4, picked.ToList(), Ask));
            // Three picked themes fill a card, so thin Kitchen never refills.
            Assert.Equal(2, offer.Count);
            Assert.DoesNotContain(Theme.Kitchen, offer);
        }
    }

    [Fact]
    public void Refill_order_follows_the_seed_among_equal_picked_themes()
    {
        // Three picked themes that can all fill a card: the seed decides which two show, so
        // different seeds do not always deal the same pair.
        int Ask(Theme t) => t is Theme.Mixed or Theme.Fishing or Theme.Mining ? 3 : 0;
        var picked = new[] { Theme.Mixed, Theme.Fishing, Theme.Mining };
        var pairs = new HashSet<string>();
        for (int seed = 1; seed <= Seeds; seed++)
            pairs.Add(RerollCycle.PairKey(SelectionService.OfferForWeek(seed, 4, picked, Ask)));
        Assert.True(pairs.Count > 1);
    }

    [Fact]
    public void A_full_offer_never_deals_a_picked_theme()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Fishing => 2, Theme.Mixed => 6, _ => 0 };
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.DoesNotContain(Theme.Mixed, SelectionService.OfferForWeek(seed, 5, new[] { Theme.Mixed }, Ask));
    }

    [Fact]
    public void A_picked_theme_that_can_ask_nothing_never_refills()
    {
        var offer = SelectionService.OfferForWeek(1, 9, new[] { Theme.Mixed, Theme.Farming }, _ => 0);
        Assert.Empty(offer);
    }

    [Fact]
    public void Candidates_add_picked_themes_when_fewer_than_two_are_left()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Mixed => 3, Theme.Farming => 1, _ => 0 };
        // Mixed fills a card, so thin Farming stays out (the offer's refill order).
        var candidates = SelectionService.Candidates(new[] { Theme.Farming, Theme.Mixed, Theme.Fishing }, Ask);
        Assert.Equal(new[] { Theme.Artisan, Theme.Mixed }, candidates);
    }

    [Fact]
    public void Candidates_leave_picked_themes_out_when_two_are_left()
    {
        int Ask(Theme t) => t switch { Theme.Artisan => 4, Theme.Kitchen => 1, Theme.Mixed => 3, _ => 0 };
        Assert.Equal(new[] { Theme.Artisan, Theme.Kitchen },
            SelectionService.Candidates(new[] { Theme.Mixed }, Ask));
    }
}
