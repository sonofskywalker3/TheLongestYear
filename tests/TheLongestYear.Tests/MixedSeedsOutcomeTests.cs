using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Cornucopia with Item Extensions puts 6 seeds a season beside vanilla's Mixed Seeds roll, so
/// the roll counted 1 time in 7 and this mod's seed upgrades, its boost and Fall's Artichoke chance
/// all fell to a seventh (2026-11-06). The roll made when the bag is used now stands where it matters.</summary>
public class MixedSeedsOutcomeTests
{
    private const string Turnip = "Cornucopia_TurnipSeeds";

    [Fact]
    public void Without_another_mod_the_roll_is_what_is_planted()
    {
        Assert.Equal("487", MixedSeedsOutcome.Final("487", "487", substituted: false));
        Assert.Equal("476", MixedSeedsOutcome.Final("476", "476", substituted: true));
    }

    [Fact]
    public void An_upgrade_or_boost_pick_stands_over_the_other_mods_pick()
        => Assert.Equal("476", MixedSeedsOutcome.Final(Turnip, "476", substituted: true));

    [Fact]
    public void Artichoke_seeds_from_vanillas_roll_stand()
        => Assert.Equal("489", MixedSeedsOutcome.Final(Turnip, "489", substituted: false));

    [Fact]
    public void Any_other_roll_gives_way_to_the_other_mods_pick()
        => Assert.Equal(Turnip, MixedSeedsOutcome.Final(Turnip, "487", substituted: false));

    [Fact]
    public void The_other_mods_vanilla_slot_plants_the_roll_already_made_not_a_second_one()
        => Assert.Equal("488", MixedSeedsOutcome.Final(MixedSeedsOutcome.MixedSeedsId, "488", substituted: false));
}
