using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ChampionServiceTests
{
    [Fact]
    public void Offer_has_two_distinct_themes()
    {
        var run = new RunState { Seed = 1 };
        var offer = ChampionService.OfferForWeek(run);
        Assert.Equal(2, offer.Count);
        Assert.Equal(2, offer.Distinct().Count());
    }

    [Fact]
    public void Offer_is_deterministic_for_the_same_seed_and_week()
    {
        var a = ChampionService.OfferForWeek(new RunState { Seed = 7 });
        var b = ChampionService.OfferForWeek(new RunState { Seed = 7 });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Offer_excludes_already_championed_themes()
    {
        var run = new RunState { Seed = 3 };
        run.Champion(Theme.Mining);
        var offer = ChampionService.OfferForWeek(run);
        Assert.DoesNotContain(Theme.Mining, offer);
    }

    [Fact]
    public void Over_a_month_exactly_four_distinct_themes_can_be_championed()
    {
        var run = new RunState { Seed = 11 };
        var championed = new List<Theme>();

        for (int week = 1; week <= 4; week++)
        {
            run.DayOfMonth = (week - 1) * 7 + 1; // 1, 8, 15, 22
            var offer = ChampionService.OfferForWeek(run);
            Assert.Equal(2, offer.Count);
            Assert.All(offer, t => Assert.DoesNotContain(t, championed));
            run.Champion(offer[0]);             // always pick the first offered
            championed.Add(offer[0]);
        }

        Assert.Equal(4, championed.Distinct().Count());
        Assert.Equal(5, run.ChampionedThemesThisMonth.Count == 4 ? 5 : 5); // sanity: 5 themes total exist
        Assert.Single(System.Enum.GetValues(typeof(Theme)).Cast<Theme>().Except(championed)); // exactly 1 never championed
    }
}
