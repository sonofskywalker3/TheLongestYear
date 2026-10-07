using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Review I1, ruling 2026-10-07: a scene that cannot stage must not loop through the queue.
/// (a) The staging checks run at pick time: while the thief scene is due, a chest with no tile
/// beside it to stand on is not filmable, and a Junimo group is filmable only through a chest that
/// has one. (b) A staging failure that still slips through postpones WITHOUT queuing; only a slot
/// collision queues.</summary>
public class StagingAtPickTests
{
    private static bool[,] Grid(int w, int h, bool open)
    {
        var g = new bool[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) g[x, y] = open;
        return g;
    }

    [Fact]
    public void A_chest_with_open_ground_beside_it_can_be_reached()
        => Assert.True(ScenePath.CanStandBeside(Grid(5, 5, true), (2, 2)));

    [Fact]
    public void A_boxed_in_chest_cannot_be_reached()
    {
        bool[,] g = Grid(5, 5, true);
        g[1, 2] = g[3, 2] = g[2, 1] = g[2, 3] = false;
        Assert.False(ScenePath.CanStandBeside(g, (2, 2)));
        Assert.Empty(ScenePath.WalkTo(g, (2, 2), Array.Empty<(int X, int Y)>(), 14, 8));
    }

    [Fact]
    public void A_chest_in_a_corner_of_the_map_with_one_open_side_can_be_reached()
    {
        bool[,] g = Grid(3, 3, false);
        g[1, 0] = true;
        Assert.True(ScenePath.CanStandBeside(g, (0, 0)));
        Assert.NotEmpty(ScenePath.WalkTo(g, (0, 0), Array.Empty<(int X, int Y)>(), 14, 8));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]   // on the farm, boxed in
    [InlineData(false, true, false)]   // in the barn
    public void A_seat_is_filmable_on_a_scene_map_with_a_way_in(bool onSceneMap, bool wayIn, bool filmable)
        => Assert.Equal(filmable, BlightRule.SeatFilmable(onSceneMap, wayIn));

    [Fact]
    public void While_the_scene_is_due_a_boxed_in_chest_is_out_of_the_draw()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(1, OnFarm: BlightRule.SeatFilmable(onSceneMap: true, hasWayIn: false), Warded: false),
            new ChestSeat(2, OnFarm: BlightRule.SeatFilmable(onSceneMap: true, hasWayIn: true), Warded: false),
        });
        Assert.False(BlightRule.InThiefDraw(hosts[0], thiefSceneDue: true));
        Assert.True(BlightRule.InThiefDraw(hosts[1], thiefSceneDue: true));
    }

    [Fact]
    public void A_junimo_group_is_filmable_only_through_a_chest_he_can_reach()
    {
        // A boxed-in Junimo Chest on the farm and a reachable one in the shed: staged at the shed.
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, BlightRule.SeatFilmable(true, false), false),
            new ChestSeat(7, BlightRule.SeatFilmable(true, true), false),
        });
        Assert.Equal(new[] { false, true }, hosts.Select(h => BlightRule.InThiefDraw(h, thiefSceneDue: true)).ToArray());
        // Both boxed in: the group waits for the scene.
        IReadOnlyList<ChestHost> boxed = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, BlightRule.SeatFilmable(true, false), false),
            new ChestSeat(7, BlightRule.SeatFilmable(true, false), false),
        });
        Assert.DoesNotContain(boxed, h => BlightRule.InThiefDraw(h, thiefSceneDue: true));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]   // the scene has played: every machine is at stake again
    public void A_placed_machine_needs_a_way_in_while_the_scene_is_due(bool due, bool wayIn, bool inDraw)
        => Assert.Equal(inDraw, BlightRule.MachineInThiefDraw(wayIn, due));

    [Fact]
    public void Only_a_slot_collision_queues_a_postponed_strike()
    {
        Assert.True(StrikeQueue.Queues(PostponeCause.SlotTaken));
        Assert.False(StrikeQueue.Queues(PostponeCause.CannotStage));
        Assert.False(StrikeQueue.Queues(PostponeCause.NeverStaged));
    }

    [Fact]
    public void A_strike_left_waiting_on_a_fail_night_settles_unqueued_and_the_reset_starts_clean()
    {
        // A fail or restart night leaves the slot alone, so the strike is only ever settled by the
        // net (NeverStaged), which does not queue; the loop reset then clears anything queued earlier.
        Assert.Equal(StrikeSlotVerdict.LeaveAlone, StrikeSlot.Decide(true, false, OvernightEvent.None, false, false, () => false));
        var run = new RunState();
        if (StrikeQueue.Queues(PostponeCause.NeverStaged)) StrikeQueue.Enqueue(run, DarknessEvent.CropBlight);
        Assert.Empty(run.QueuedStrikes);
        StrikeQueue.Enqueue(run, DarknessEvent.Reversion);   // queued by a collision earlier in the loop
        run.BeginNewRun(seed: 7);
        Assert.Empty(run.QueuedStrikes);
    }
}
