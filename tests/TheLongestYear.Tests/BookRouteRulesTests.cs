using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-09-15: The Alleyway Buffet sits behind a gold axe and gold pickaxe, Mapping
/// Cave Systems behind 1,000 monster kills. Both take time, so be fair on the easier difficulties.</summary>
public class BookRouteRulesTests
{
    [Fact]
    public void The_two_slow_books_are_the_late_ones()
    {
        Assert.Contains("(O)Book_Trash", AvailabilityWeeks.LateBookIds);
        Assert.Contains("(O)Book_Marlon", AvailabilityWeeks.LateBookIds);
        Assert.DoesNotContain("(O)Book_PriceCatalogue", AvailabilityWeeks.LateBookIds);
        Assert.DoesNotContain("(O)Book_Speed", AvailabilityWeeks.LateBookIds);
    }

    [Fact]
    public void Their_weeks_pin_them_to_Fall_and_Winter()
    {
        Assert.Equal(Season.Fall, AvailabilityWeeks.SeasonOf(AvailabilityWeeks.BookWeeks["(O)Book_Trash"]));
        Assert.Equal(Season.Winter, AvailabilityWeeks.SeasonOf(AvailabilityWeeks.BookWeeks["(O)Book_Marlon"]));
    }

    [Fact]
    public void Easy_bans_them_and_every_other_step_keeps_them()
    {
        Assert.Equal(AvailabilityWeeks.LateBookIds, BookRouteRules.BannedFor(DifficultyStep.Easy));
        Assert.Empty(BookRouteRules.BannedFor(DifficultyStep.Normal));
        Assert.Empty(BookRouteRules.BannedFor(DifficultyStep.Hard));
        Assert.Empty(BookRouteRules.BannedFor(DifficultyStep.Extreme));
    }
}
