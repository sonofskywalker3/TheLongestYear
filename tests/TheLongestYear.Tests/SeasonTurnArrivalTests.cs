using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-08 (TODO "Winter save pass" item 16): the season-turn porch scene no longer
/// plays on waking ("It's weird it happens and then I get out of bed"). It is owed from the Continue
/// morning and plays on the farmer's first arrival on the Farm by any route, staged at the porch like
/// the tamper scene, and it goes before a tamper scene owed on the same arrival.</summary>
public class SeasonTurnArrivalTests
{
    private static readonly (int X, int Y) Porch = (64, 15);

    private static FarmArrivalScene Pick(
        SeasonTurnKind? turn = SeasonTurnKind.Winter, bool tamper = false, bool ending = false,
        string? entered = "Farm", bool local = true, bool busy = false, bool porchKnown = true)
        => SeasonTurnArrival.Pick(turn, tamper, ending, entered, local, busy, porchKnown ? Porch : null);

    [Theory]
    [InlineData(SeasonTurnKind.Summer)]
    [InlineData(SeasonTurnKind.Fall)]
    [InlineData(SeasonTurnKind.Winter)]
    public void Arriving_on_the_farm_with_a_turn_owed_starts_it(SeasonTurnKind kind)
        => Assert.Equal(FarmArrivalScene.SeasonTurn, Pick(turn: kind));

    [Fact]
    public void Nothing_owed_means_no_scene()
        => Assert.Equal(FarmArrivalScene.None, Pick(turn: null));

    [Theory]
    [InlineData("FarmHouse")]
    [InlineData("BusStop")]
    [InlineData("Greenhouse")]
    [InlineData(null)]
    public void Anywhere_but_the_farm_keeps_it_owed(string? where)
        => Assert.Equal(FarmArrivalScene.None, Pick(entered: where, tamper: true));

    [Fact]
    public void A_busy_arrival_keeps_both_for_the_next_one()
        => Assert.Equal(FarmArrivalScene.None, Pick(tamper: true, busy: true));

    [Fact]
    public void Another_players_warp_starts_nothing()
        => Assert.Equal(FarmArrivalScene.None, Pick(local: false, tamper: true));

    [Fact]
    public void No_porch_known_starts_nothing()
        => Assert.Equal(FarmArrivalScene.None, Pick(porchKnown: false, tamper: true));

    [Fact]
    public void The_season_turn_goes_before_a_tamper_on_the_same_arrival()
        => Assert.Equal(FarmArrivalScene.SeasonTurn, Pick(tamper: true));

    [Fact]
    public void A_tamper_alone_still_plays()
        => Assert.Equal(FarmArrivalScene.Tamper, Pick(turn: null, tamper: true));

    [Fact]
    public void The_armed_ending_holds_the_turn_back()
        => Assert.Equal(FarmArrivalScene.None, Pick(ending: true));

    [Fact]
    public void The_armed_ending_does_not_hold_back_a_tamper()
        => Assert.Equal(FarmArrivalScene.Tamper, Pick(ending: true, tamper: true));

    // ---------------------------------------------------------------- what is stored

    [Theory]
    [InlineData(SeasonTurnKind.Summer)]
    [InlineData(SeasonTurnKind.Fall)]
    [InlineData(SeasonTurnKind.Winter)]
    public void An_owed_turn_round_trips(SeasonTurnKind kind)
        => Assert.Equal(kind, SeasonTurnArrival.Owed(SeasonTurnArrival.Owe(kind)));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Spring")]
    [InlineData("garbage")]
    public void Nothing_or_an_unknown_value_owes_nothing(string? stored)
        => Assert.Null(SeasonTurnArrival.Owed(stored));

    [Fact]
    public void A_new_run_owes_no_turn()
    {
        var run = new RunState { PendingSeasonTurn = SeasonTurnArrival.Owe(SeasonTurnKind.Fall) };
        run.BeginNewRun(seed: 7);
        Assert.Null(SeasonTurnArrival.Owed(run.PendingSeasonTurn));
    }

    [Fact]
    public void A_fresh_run_state_owes_no_turn()
        => Assert.Null(SeasonTurnArrival.Owed(new RunState().PendingSeasonTurn));

    [Fact]
    public void The_owed_turn_survives_a_save()
    {
        var run = new RunState { PendingSeasonTurn = SeasonTurnArrival.Owe(SeasonTurnKind.Summer) };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(run);
        RunState back = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>(json)!;
        Assert.Equal(SeasonTurnKind.Summer, SeasonTurnArrival.Owed(back.PendingSeasonTurn));
    }
}
