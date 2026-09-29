using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class PetCarryoverTests
{
    [Fact]
    public void MigrateLegacy_moves_single_snapshot_into_list_once()
    {
        var s = new MetaState { PetState = new PetSnapshot("Cat", "1", "Mochi", 800) };
        Assert.True(PetCarryover.MigrateLegacy(s));
        Assert.Null(s.PetState);
        Assert.Single(s.PetStates);
        Assert.Equal("Mochi", s.PetStates[0].Name);
        Assert.False(PetCarryover.MigrateLegacy(s));
    }

    [Fact]
    public void MigrateLegacy_does_not_clobber_an_existing_list()
    {
        var s = new MetaState
        {
            PetState = new PetSnapshot("Cat", "1", "Old", 100),
            PetStates = new List<PetSnapshot> { new("Dog", "0", "Rex", 500) },
        };
        Assert.False(PetCarryover.MigrateLegacy(s));
        Assert.Single(s.PetStates);
        Assert.Equal("Rex", s.PetStates[0].Name);
        Assert.Null(s.PetState);
    }

    [Theory]
    [InlineData(0, 54, 8)]
    [InlineData(1, 52, 8)]
    [InlineData(3, 48, 8)]
    public void RestoreTile_staggers_west_by_two_columns(int index, int x, int y)
        => Assert.Equal((x, y), PetCarryover.RestoreTile(index));

    [Theory]
    [InlineData(0, 53, 7)]
    [InlineData(1, 51, 7)]
    [InlineData(3, 47, 7)]
    public void BowlTile_sits_up_left_of_each_restore_tile(int index, int x, int y)
        => Assert.Equal((x, y), PetCarryover.BowlTile(index));

    [Fact]
    public void BowlTile_zero_is_vanillas_default_bowl_tile()
        => Assert.Equal((53, 7), PetCarryover.BowlTile(0));

    [Fact]
    public void ClampFriendship_bounds_0_to_1000()
    {
        Assert.Equal(0, PetCarryover.ClampFriendship(-5));
        Assert.Equal(1000, PetCarryover.ClampFriendship(4000));
        Assert.Equal(300, PetCarryover.ClampFriendship(300));
    }

    [Fact]
    public void Legacy_json_with_PetState_still_loads_and_migrates()
    {
        string json = "{\"JunimoPoints\":5,\"PetState\":{\"PetType\":\"Dog\",\"WhichBreed\":\"2\",\"Name\":\"Rex\",\"Friendship\":400}}";
        MetaState s = JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.NotNull(s.PetState);
        Assert.Empty(s.PetStates);
        PetCarryover.MigrateLegacy(s);
        Assert.Equal("Rex", s.PetStates[0].Name);
    }

    /// <summary>A rewind with no pet on the farm lets Marnie's pet visit play again (Jeff,
    /// 2026-09-29; a streamer thought the pet chance was gone). Both arrival scenes, cat and dog.</summary>
    [Fact]
    public void A_petless_farm_reopens_both_pet_arrival_scenes()
    {
        var seen = new HashSet<string> { "1590166", "897405", "60367" };
        Assert.Equal(2, PetCarryover.ReopenArrivalScenes(seen, farmHasPet: false));
        Assert.Equal(new[] { "60367" }, seen);
    }

    [Fact]
    public void A_farm_with_a_pet_keeps_the_arrival_scenes_seen()
    {
        var seen = new HashSet<string> { "1590166", "897405" };
        Assert.Equal(0, PetCarryover.ReopenArrivalScenes(seen, farmHasPet: true));
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void Keep_Pet_costs_50_JP()
        => Assert.Equal(50L, UpgradeCatalog.All.Single(u => u.Id == "keep_pet").Cost);
}
