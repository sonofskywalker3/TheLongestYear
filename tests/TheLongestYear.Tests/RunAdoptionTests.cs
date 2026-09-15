using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus, 2026-09-14: quit on Spring 1 right after character creation, reload, and the
/// mod is gone. The game saved at character creation, before the mod ever wrote its marker.</summary>
public class RunAdoptionTests
{
    [Fact]
    public void A_never_slept_first_morning_with_no_mod_data_is_adopted()
    {
        Assert.True(RunAdoption.IsUnsavedFirstMorning(hasSavedMetaData: false, daysPlayed: 1, Season.Spring, dayOfMonth: 1, year: 1));
    }

    [Fact]
    public void A_save_that_already_carries_mod_data_is_never_adopted()
    {
        Assert.False(RunAdoption.IsUnsavedFirstMorning(hasSavedMetaData: true, daysPlayed: 1, Season.Spring, dayOfMonth: 1, year: 1));
    }

    [Theory]
    [InlineData(2u, Season.Spring, 2, 1)]  // slept once: a vanilla save the player kept playing
    [InlineData(28u, Season.Spring, 1, 1)] // Spring 1 again after a loop, but with days on the clock
    [InlineData(1u, Season.Summer, 1, 1)]
    [InlineData(1u, Season.Spring, 1, 2)]
    public void Anything_past_the_first_morning_is_left_alone(uint daysPlayed, Season season, int day, int year)
    {
        Assert.False(RunAdoption.IsUnsavedFirstMorning(hasSavedMetaData: false, daysPlayed, season, day, year));
    }
}
