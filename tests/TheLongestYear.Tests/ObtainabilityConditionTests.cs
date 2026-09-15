using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityConditionTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
    };

    private static ConditionReading Read(string? condition) => ConditionSeasons.Read(condition, Festivals);

    [Fact]
    public void No_condition_is_every_week()
    {
        ConditionReading r = Read(null);
        Assert.Equal(WeekMask.All, r.Weeks);
        Assert.False(r.YearTwo || r.FewDays || r.Chance || r.RainOnly || r.Unresolved || r.IslandHint);
        Assert.Empty(r.Other);
    }

    [Theory]
    [InlineData("SEASON Spring", "1-4")]
    [InlineData("SEASON spring summer", "1-8")]
    [InlineData("LOCATION_SEASON Here fall", "9-12")]
    [InlineData("!SEASON Winter", "1-12")]
    [InlineData("SEASON Spring Fall, !SEASON Fall", "1-4")]
    [InlineData("DAYS_PLAYED 29", "5-16")]
    [InlineData("DAYS_PLAYED 1 28", "1-4")]
    [InlineData("DAY_OF_MONTH 1 2", "1,5,9,13")]
    [InlineData("DAY_OF_MONTH even", "1-16")]
    public void Temporal_clauses_narrow_the_weeks(string condition, string expected)
        => Assert.Equal(expected, Read(condition).Weeks.ToString());

    [Theory]
    [InlineData("FALSE")]
    [InlineData("!TRUE")]
    [InlineData("!YEAR 1")]
    public void Clauses_that_never_pass_in_year_one_empty_the_weeks(string condition)
        => Assert.True(Read(condition).Weeks.IsEmpty);

    [Fact]
    public void Year_two_is_flagged_not_counted()
    {
        Assert.True(Read("YEAR 2").YearTwo);
        Assert.False(Read("YEAR 1").YearTwo);
        Assert.False(Read("!YEAR 2").YearTwo);
        Assert.Equal(WeekMask.All, Read("!YEAR 2").Weeks);
        Assert.False(Read("YEAR 1 1").YearTwo);
        Assert.Equal(WeekMask.All, Read("YEAR 1 1").Weeks);
    }

    [Fact]
    public void Festival_and_day_clauses_are_few_days()
    {
        Assert.Equal(WeekMask.Of(15), Read("IS_PASSIVE_FESTIVAL_OPEN NightMarket").Weeks);
        Assert.True(Read("IS_PASSIVE_FESTIVAL_TODAY NightMarket").FewDays);
        Assert.Equal(WeekMask.Of(6), Read("SEASON_DAY Summer 11").Weeks);
        Assert.True(Read("DAY_OF_WEEK Friday").FewDays);
        Assert.Equal(WeekMask.All, Read("DAY_OF_WEEK Friday").Weeks);
    }

    [Fact]
    public void Wet_weather_is_rain_only_and_random_is_chance()
    {
        Assert.True(Read("WEATHER Here Rain Storm").RainOnly);
        Assert.False(Read("WEATHER Here Sun").RainOnly);
        Assert.True(Read("RANDOM 0.1").Chance);
    }

    [Fact]
    public void Prerequisites_are_notes_and_unknown_clauses_are_unresolved()
    {
        ConditionReading friend = Read("PLAYER_HEARTS Current Abigail 4");
        Assert.Equal(new[] { "PLAYER_HEARTS Current Abigail 4" }, friend.Other);
        Assert.False(friend.Unresolved);
        ConditionReading modded = Read("SOME_MOD_QUERY 5");
        Assert.True(modded.Unresolved);
        Assert.True(Read("ITEMX 3").Unresolved);          // a known prefix fragment is not a known query
        Assert.True(Read("!DAY_OF_MONTH 5").Unresolved);  // negated day lists are not narrowed, so they are flagged
        Assert.False(Read("ITEM_CONTEXT_TAG Target fish").Unresolved);
        Assert.Equal(WeekMask.All, modded.Weeks);
        Assert.True(Read("ANY \"SEASON Spring\" \"SEASON Fall\"").Unresolved);
        Assert.True(Read("PLAYER_HAS_MAIL Current Island_Resort").IslandHint);
    }

    [Fact]
    public void Apply_folds_flags_and_notes_into_conditions()
    {
        ObtainConditions c = ConditionSeasons.Apply(
            ObtainConditions.None with { Requires = new[] { "shop:Sandy" } },
            Read("YEAR 2, WEATHER Here Rain, SOME_MOD_QUERY"));
        Assert.True(c.YearTwo);
        Assert.True(c.RainOnly);
        Assert.True(c.Unresolved);
        Assert.Equal(new[] { "shop:Sandy", "SOME_MOD_QUERY" }, c.Requires);
    }

    [Fact]
    public void Conditions_compare_by_content_not_by_list_reference()
    {
        var a = ObtainConditions.None with { Requires = new[] { "shop:SeedShop" } };
        var b = ObtainConditions.None with { Requires = new List<string> { "shop:SeedShop" } };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Requires = new[] { "shop:Sandy" } });
    }
}
