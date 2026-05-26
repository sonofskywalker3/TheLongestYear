using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SelectionServiceTests
{
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
        // Sanity: 5 themes exist; exactly 1 stays un-selected over a 4-week month.
        Assert.Single(System.Enum.GetValues(typeof(Theme)).Cast<Theme>().Except(selected));
    }
}
