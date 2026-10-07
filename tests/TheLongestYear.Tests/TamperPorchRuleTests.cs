using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07: the Junimos' "tainted" scene plays the first time the farmer arrives on
/// the Farm by ANY route while a tamper report waits ("however you get to the farm map, just show
/// the scene, vanilla does this too"). Like vanilla's Community Center cutscene on entering Town, it
/// is staged at the farmhouse porch and the farmer is put back where he arrived when it ends.</summary>
public class TamperPorchRuleTests
{
    // The standard farm: the farmhouse's warp onto the Farm lands the farmer on (64,16).
    private static readonly (int X, int Y) Porch = (64, 16);

    private static bool Start(
        bool pending = true, string? entered = "Farm", bool local = true, bool busy = false, bool porchKnown = true)
        => TamperPorchRule.ShouldStart(pending, entered, local, busy, porchKnown ? Porch : null);

    [Fact]
    public void Arriving_on_the_farm_with_a_tamper_waiting_starts_it()
        => Assert.True(Start());

    [Fact]
    public void Nothing_waiting_means_no_scene()
        => Assert.False(Start(pending: false));

    [Theory]
    [InlineData("FarmHouse")]
    [InlineData("Town")]
    [InlineData("Cellar")]
    [InlineData("Greenhouse")]
    [InlineData("farm")]
    [InlineData(null)]
    public void Arriving_anywhere_but_the_farm_waits(string? where)
        => Assert.False(Start(entered: where));

    [Fact]
    public void Another_players_warp_does_not_start_it()
        => Assert.False(Start(local: false));

    [Fact]
    public void A_busy_arrival_waits_for_the_next_one()
        => Assert.False(Start(busy: true));

    [Fact]
    public void No_known_porch_means_no_scene()
        => Assert.False(Start(porchKnown: false));

    // ---------------------------------------------------------------- where it is staged

    [Fact]
    public void The_porch_is_where_the_house_door_warp_puts_the_farmer()
        => Assert.Equal((64, 16), TamperPorchRule.PorchTile(new[] { (64, 16) }, (64, 15)));

    [Fact]
    public void A_farm_type_with_its_door_elsewhere_reads_its_own_exit()
        => Assert.Equal((82, 14), TamperPorchRule.PorchTile(new[] { (82, 14) }, (64, 15)));

    [Fact]
    public void With_no_door_warp_the_porch_is_the_step_below_the_farms_door_tile()
    {
        Assert.Equal((64, 16), TamperPorchRule.PorchTile(new (int X, int Y)[0], (64, 15)));
        Assert.Equal((64, 16), TamperPorchRule.PorchTile(null, (64, 15)));
    }

    [Fact]
    public void With_neither_there_is_no_porch()
        => Assert.Null(TamperPorchRule.PorchTile(null, null));

    // ---------------------------------------------------------------- where he goes back to

    [Theory]
    // Vanilla's eventFinished adds one to X when an event ends on the Farm with the saved position
    // on row 64 (Game1.cs:6837). The rule hands vanilla a tile one to the left there, so the farmer
    // lands on the exact tile he arrived at.
    [InlineData(41, 64, 40, 64)]
    [InlineData(64, 16, 64, 16)]
    [InlineData(0, 30, 0, 30)]
    public void The_return_tile_undoes_vanillas_row_64_nudge(int x, int y, int handX, int handY)
        => Assert.Equal((handX, handY), TamperPorchRule.ReturnTileForVanilla((x, y)));
}
