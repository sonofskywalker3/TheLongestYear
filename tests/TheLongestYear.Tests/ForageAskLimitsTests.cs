using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ForageAskLimitsTests
{
    /// <summary>The report that started this: a Summer Foraging bundle asked for 95 Rainbow Shells.
    /// Three measured years gave 11/5/6, mean 7.3, so the ceiling is 6. Nothing may ask for more.</summary>
    [Fact]
    public void Rainbow_shell_can_never_be_asked_for_ninety_five()
    {
        Assert.Equal(6, ForageAskLimits.MaxAsk(Season.Summer, "(O)394"));
        Assert.Equal(6, ForageAskLimits.Clamp(Season.Summer, "(O)394", 95));
        Assert.Equal(6, ForageAskLimits.ClampAnySeason("(O)394", 95));
    }

    /// <summary>The ceiling is 80% of the measured mean, rounded up (Jeff, 2026-08-30). Sampled on
    /// items Wild Seeds cannot grow - a growable has no ceiling at all, see below.</summary>
    [Theory]
    [InlineData("(O)393", 46)]   // Coral, mean 56.3
    [InlineData("(O)259", 20)]   // Fiddlehead Fern, mean 24.7
    [InlineData("(O)394", 6)]    // Rainbow Shell, mean 7.3
    public void Ceiling_is_eighty_percent_of_the_measured_mean(string itemId, int expected)
        => Assert.Equal(expected, ForageAskLimits.MaxAsk(Season.Summer, itemId));

    [Fact]
    public void A_stack_already_under_the_ceiling_is_untouched()
        => Assert.Equal(3, ForageAskLimits.Clamp(Season.Summer, "(O)394", 3));

    /// <summary>Anything the table does not cover must pass straight through: this clamp speaks
    /// only for measured forage, and must never quietly cap a fish, mineral or artisan ask.</summary>
    [Fact]
    public void An_unmeasured_item_is_never_clamped()
    {
        Assert.Null(ForageAskLimits.MaxAsk(Season.Summer, "(O)128"));   // Pufferfish, a fish
        Assert.Equal(99, ForageAskLimits.Clamp(Season.Summer, "(O)128", 99));
        Assert.Equal(99, ForageAskLimits.ClampAnySeason("(O)128", 99));
        Assert.Equal(50, ForageAskLimits.ClampAnySeason(null, 50));
    }

    /// <summary>Items whose real supply is not wild forage are deliberately absent, so the clamp
    /// cannot brand them near-unobtainable off a number that never saw their actual route: Seaweed
    /// is a rod catch, and Cockle, Mussel and Oyster come out of crab pots.</summary>
    [Theory]
    [InlineData("(O)152")]   // Seaweed
    [InlineData("(O)718")]   // Cockle
    public void Items_with_a_non_forage_route_are_left_unclamped(string itemId)
        => Assert.Null(ForageAskLimits.MaxAskAnySeason(itemId));

    /// <summary>Fiddlehead Fern is the instructive one: as Secret Woods forage it is properly
    /// measured in SUMMER (mean 24.7, ceiling 20) and is clamped there, while its stray
    /// Spring/Fall/Winter counts fall under the floor and contribute nothing. Green Rain would only
    /// ever add to the Summer figure, so the ceiling stays on the generous side.</summary>
    [Fact]
    public void Fiddlehead_fern_is_clamped_on_its_real_summer_yield()
    {
        Assert.Equal(20, ForageAskLimits.MaxAsk(Season.Summer, "(O)259"));
        Assert.Null(ForageAskLimits.MaxAsk(Season.Winter, "(O)259"));
    }

    /// <summary>The season-agnostic ceiling takes the item's best season, so it can only ever be
    /// too lenient - never tighter than what the asking season can actually grow.</summary>
    [Fact]
    public void Any_season_ceiling_is_the_most_generous_season()
    {
        int? summer = ForageAskLimits.MaxAsk(Season.Summer, "(O)393");   // Coral, mean 56.3
        int? anySeason = ForageAskLimits.MaxAskAnySeason("(O)393");
        Assert.NotNull(summer);
        Assert.NotNull(anySeason);
        Assert.True(anySeason.Value >= summer.Value);
    }

    /// <summary>No ceiling may exceed a single inventory stack, or the ask is unfillable.</summary>
    [Fact]
    public void No_ceiling_exceeds_the_ninety_nine_stack_cap()
    {
        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
            foreach (string id in new[] { "(O)393", "(O)394", "(O)259", "(O)283" })
            {
                int? max = ForageAskLimits.MaxAsk(season, id);
                if (max != null)
                    Assert.InRange(max.Value, 1, StackScaling.MaxStack);
            }
    }

    /// <summary>Wild Seeds turn 4 forage into 10 seeds and each seed grows one of these back, so
    /// their supply is land and time, not the spawn rate. Capping them on a measured spawn count
    /// would be meaningless (decompile Crop.getRandomWildCropForSeason).</summary>
    [Theory]
    [InlineData("(O)402")]   // Sweet Pea, measured 84.3 - was wrongly capped at 68
    [InlineData("(O)410")]   // Blackberry, measured 57.3 - was wrongly capped at 46
    [InlineData("(O)16")]    // Wild Horseradish
    [InlineData("(O)418")]   // Crocus
    public void Wild_seed_growables_are_never_capped(string itemId)
    {
        Assert.True(ForageAskLimits.IsWildSeedGrowable(itemId));
        Assert.Null(ForageAskLimits.MaxAskAnySeason(itemId));
        Assert.Equal(99, ForageAskLimits.ClampAnySeason(itemId, 99));
    }

    /// <summary>The distinction that matters: you cannot grow a shell. Summer Seeds only ever give
    /// Spice Berry, Grape or Sweet Pea, so no amount of farming produces a Rainbow Shell and its
    /// measured ceiling stands.</summary>
    [Theory]
    [InlineData("(O)394")]   // Rainbow Shell
    [InlineData("(O)393")]   // Coral
    [InlineData("(O)259")]   // Fiddlehead Fern
    public void Shells_and_other_ungrowables_keep_their_measured_cap(string itemId)
    {
        Assert.False(ForageAskLimits.IsWildSeedGrowable(itemId));
        Assert.NotNull(ForageAskLimits.MaxAskAnySeason(itemId));
    }
}
