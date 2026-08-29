using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SelectionServiceTests
{
    private static int Askable(Theme t) => t switch
    {
        Theme.Spelunking => 0, Theme.Artisan => 5, Theme.Kitchen => 1, Theme.Farming => 3, _ => 0,
    };

    [Fact]
    public void Themes_with_fewer_than_two_askable_goals_are_never_offered()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 5, System.Array.Empty<Theme>(), Askable);
            Assert.Equal(2, offer.Count);
            Assert.DoesNotContain(Theme.Spelunking, offer);
            Assert.DoesNotContain(Theme.Kitchen, offer);
            Assert.Contains(Theme.Artisan, offer);
            Assert.Contains(Theme.Farming, offer);
        }
    }

    [Fact]
    public void Zero_askable_everywhere_offers_nothing()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 3, System.Array.Empty<Theme>(), _ => 0);
            Assert.Empty(offer);
        }
    }

    [Fact]
    public void The_offer_never_pads_with_a_theme_that_can_ask_nothing()
    {
        int Askable(Theme t) => t == Theme.Fishing ? 3 : 0;
        var offer = SelectionService.OfferForWeek(1, 1, System.Array.Empty<Theme>(), Askable);
        Assert.Equal(new[] { Theme.Fishing }, offer);
    }

    [Fact]
    public void A_theme_with_one_goal_may_pad_the_second_card()
    {
        int Askable(Theme t) => t == Theme.Fishing ? 3 : t == Theme.Foraging ? 1 : 0;
        var offer = SelectionService.OfferForWeek(1, 1, System.Array.Empty<Theme>(), Askable);
        Assert.Equal(2, offer.Count);
        Assert.Contains(Theme.Foraging, offer);
    }

    [Fact]
    public void Weighted_draw_is_deterministic_for_a_seed()
    {
        int Ask(Theme t) => t switch { Theme.Farming => 3, Theme.Artisan => 5, Theme.Fishing => 2, _ => 0 };
        var a = SelectionService.OfferForWeek(7, 2, System.Array.Empty<Theme>(), Ask);
        var b = SelectionService.OfferForWeek(7, 2, System.Array.Empty<Theme>(), Ask);
        Assert.Equal(a, b);
        Assert.Equal(2, a.Distinct().Count());
    }

    [Fact]
    public void A_single_qualified_theme_is_padded_from_room_themes_that_can_ask_something()
    {
        int Askable(Theme t) => t == Theme.Artisan ? 4 : t == Theme.Fishing ? 1 : 0;
        var offer = SelectionService.OfferForWeek(3, 6, new[] { Theme.Farming }, Askable);
        Assert.Equal(2, offer.Count);
        Assert.Contains(Theme.Artisan, offer);
        Assert.DoesNotContain(Theme.Farming, offer);
        Assert.Contains(offer.First(t => t != Theme.Artisan), ThemeDomains.RoomThemes);
    }

    [Fact]
    public void A_single_qualified_theme_with_nothing_to_pad_offers_one_card()
    {
        var offer = SelectionService.OfferForWeek(3, 6, new[] { Theme.Farming }, t => t == Theme.Artisan ? 4 : 0);
        Assert.Equal(new[] { Theme.Artisan }, offer);
    }

    [Fact]
    public void Candidates_are_the_qualified_themes_or_the_room_themes_when_short()
    {
        Assert.Equal(new[] { Theme.Farming, Theme.Artisan }, SelectionService.Candidates(System.Array.Empty<Theme>(), Askable));
        var padded = SelectionService.Candidates(new[] { Theme.Farming }, Askable);
        Assert.Equal(new[] { Theme.Artisan }, padded);
    }

    [Fact]
    public void Candidates_pad_with_room_themes_that_can_ask_at_least_one()
    {
        int Askable(Theme t) => t == Theme.Artisan ? 5 : t == Theme.Mining ? 1 : 0;
        var padded = SelectionService.Candidates(new[] { Theme.Farming }, Askable);
        Assert.Contains(Theme.Artisan, padded);
        Assert.Contains(Theme.Mining, padded);
        Assert.DoesNotContain(Theme.Farming, padded);
    }
    [Fact]
    public void Offer_has_two_distinct_themes()
    {
        var run = new RunState { Seed = 1 };
        var offer = SelectionService.OfferForWeek(run);
        Assert.Equal(2, offer.Count);
        Assert.Equal(2, offer.Distinct().Count());
    }

    [Fact]
    public void Offer_is_deterministic_for_the_same_seed_and_week()
    {
        var a = SelectionService.OfferForWeek(new RunState { Seed = 7 });
        var b = SelectionService.OfferForWeek(new RunState { Seed = 7 });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Offer_excludes_already_selected_themes()
    {
        var run = new RunState { Seed = 3 };
        run.Select(Theme.Mining);
        var offer = SelectionService.OfferForWeek(run);
        Assert.DoesNotContain(Theme.Mining, offer);
    }

    [Fact]
    public void Over_a_month_exactly_four_distinct_themes_can_be_selected()
    {
        var run = new RunState { Seed = 11 };
        var selected = new List<Theme>();

        for (int week = 1; week <= 4; week++)
        {
            run.DayOfMonth = (week - 1) * 7 + 1; // 1, 8, 15, 22
            var offer = SelectionService.OfferForWeek(run);
            Assert.Equal(2, offer.Count);
            Assert.All(offer, t => Assert.DoesNotContain(t, selected));
            run.Select(offer[0]);                // always pick the first offered
            selected.Add(offer[0]);
        }

        Assert.Equal(4, selected.Distinct().Count());
    }
}
