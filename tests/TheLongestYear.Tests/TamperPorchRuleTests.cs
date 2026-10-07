using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07: the Junimos' "tainted" scene no longer plays on waking. It plays the
/// first time the farmer steps out of the farmhouse onto the Farm with a tamper report waiting,
/// where he stands. Review fix round 1: only a farmhouse-door exit counts, because the scene's
/// marks are laid out from the step below the door.</summary>
public class TamperPorchRuleTests
{
    // The standard farm: the farmhouse's warp onto the Farm lands the farmer on (64,16).
    private static readonly IReadOnlyList<(int X, int Y)> Door = new[] { (64, 16) };

    private static bool Start(
        bool pending = true, string entered = "Farm", string previous = "FarmHouse",
        bool local = true, bool busy = false, int x = 64, int y = 16, IReadOnlyList<(int X, int Y)>? exits = null)
        => TamperPorchRule.ShouldStart(pending, entered, previous, local, busy, x, y, exits ?? Door);

    [Fact]
    public void Walking_out_of_the_farmhouse_door_with_a_tamper_waiting_starts_it()
        => Assert.True(Start());

    [Fact]
    public void Nothing_waiting_means_no_scene()
        => Assert.False(Start(pending: false));

    [Theory]
    [InlineData("FarmHouse")]
    [InlineData("Town")]
    [InlineData("Cellar")]
    [InlineData("farm")]
    public void Arriving_anywhere_but_the_farm_waits(string where)
        => Assert.False(Start(entered: where));

    [Theory]
    [InlineData("Forest")]
    [InlineData("BusStop")]
    [InlineData("Backwoods")]
    [InlineData("Cellar")]
    [InlineData("Greenhouse")]
    [InlineData("Town")]
    [InlineData(null)]
    public void A_farm_entry_from_anywhere_but_the_farmhouse_waits(string? from)
        => Assert.False(Start(previous: from!));

    [Theory]
    [InlineData(48, 7)]    // a Warp Totem: Farm lands by the farm's totem spot, far from the porch
    [InlineData(64, 18)]   // two tiles below the exit
    [InlineData(66, 16)]   // two tiles beside it
    public void A_totem_from_inside_the_house_lands_away_from_the_door_and_waits(int x, int y)
        => Assert.False(Start(x: x, y: y));

    [Theory]
    [InlineData(64, 16)]
    [InlineData(63, 16)]
    [InlineData(65, 17)]
    [InlineData(64, 15)]
    public void On_or_within_one_tile_of_the_door_exit_starts_it(int x, int y)
        => Assert.True(Start(x: x, y: y));

    [Fact]
    public void A_farm_type_with_its_door_elsewhere_reads_its_own_exit()
    {
        var beachDoor = new[] { (82, 14) };
        Assert.True(Start(x: 82, y: 14, exits: beachDoor));
        Assert.False(Start(x: 64, y: 16, exits: beachDoor));
    }

    [Fact]
    public void No_known_door_exit_means_no_scene()
        => Assert.False(Start(exits: new (int X, int Y)[0]));

    [Fact]
    public void Another_players_warp_does_not_start_it()
        => Assert.False(Start(local: false));

    [Fact]
    public void A_busy_entry_waits_for_the_next_one()
        => Assert.False(Start(busy: true));
}
