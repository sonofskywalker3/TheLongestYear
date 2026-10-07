using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07: the Junimos' "tainted" scene no longer plays on waking. It plays the
/// first time the farmer steps onto the Farm with a tamper report waiting, where he stands.</summary>
public class TamperPorchRuleTests
{
    [Fact]
    public void Walking_out_onto_the_farm_with_a_tamper_waiting_starts_it()
        => Assert.True(TamperPorchRule.ShouldStart(true, "Farm", isLocalPlayer: true, busy: false));

    [Fact]
    public void Nothing_waiting_means_no_scene()
        => Assert.False(TamperPorchRule.ShouldStart(false, "Farm", isLocalPlayer: true, busy: false));

    [Theory]
    [InlineData("FarmHouse")]
    [InlineData("Town")]
    [InlineData("Cellar")]
    [InlineData("farm")]
    public void Anywhere_but_the_farm_waits(string where)
        => Assert.False(TamperPorchRule.ShouldStart(true, where, isLocalPlayer: true, busy: false));

    [Fact]
    public void Waking_in_the_farmhouse_does_not_start_it()
        => Assert.False(TamperPorchRule.ShouldStart(true, "FarmHouse", isLocalPlayer: true, busy: false));

    [Fact]
    public void Another_players_warp_does_not_start_it()
        => Assert.False(TamperPorchRule.ShouldStart(true, "Farm", isLocalPlayer: false, busy: false));

    [Fact]
    public void A_busy_entry_waits_for_the_next_one()
        => Assert.False(TamperPorchRule.ShouldStart(true, "Farm", isLocalPlayer: true, busy: true));

    [Fact]
    public void A_null_location_never_starts_it()
        => Assert.False(TamperPorchRule.ShouldStart(true, null!, isLocalPlayer: true, busy: false));
}
