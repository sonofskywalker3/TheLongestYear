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
    public void Zero_askable_everywhere_reproduces_the_room_theme_offer()
    {
        var activity = new HashSet<Theme>(ThemeDomains.ActivityThemes);
        for (int seed = 1; seed <= 30; seed++)
        {
            var withRules = SelectionService.OfferForWeek(seed, 3, System.Array.Empty<Theme>(), _ => 0);
            var legacyRooms = SelectionService.OfferForWeek(seed, 3, activity);   // legacy path, activity themes excluded
            Assert.Equal(legacyRooms, withRules);
        }
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
    public void A_single_qualified_theme_is_padded_from_room_themes()
    {
        var offer = SelectionService.OfferForWeek(3, 6, new[] { Theme.Farming }, t => t == Theme.Artisan ? 4 : 0);
        Assert.Equal(2, offer.Count);
        Assert.Contains(Theme.Artisan, offer);
        Assert.DoesNotContain(Theme.Farming, offer);
        Assert.Contains(offer.First(t => t != Theme.Artisan), ThemeDomains.RoomThemes);
    }

    [Fact]
    public void Candidates_are_the_qualified_themes_or_the_room_themes_when_short()
    {
        Assert.Equal(new[] { Theme.Farming, Theme.Artisan }, SelectionService.Candidates(System.Array.Empty<Theme>(), Askable));
        var padded = SelectionService.Candidates(new[] { Theme.Farming }, Askable);
        Assert.Contains(Theme.Artisan, padded);
        Assert.Contains(Theme.Fishing, padded);
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
