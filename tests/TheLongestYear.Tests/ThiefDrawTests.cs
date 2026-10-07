using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-07 (option a): while the thief scene is still due this loop, the chest
/// draw only considers chests the scene can show (the Farm, the FarmHouse, the Cellar, a Shed), so a
/// theft never lands without its scene. Once the scene has played, every chest is in the draw again
/// and later thefts land with no scene by design. A Junimo group is filmable when any of its chests
/// stands on a scene map.</summary>
public class ThiefDrawTests
{
    [Fact]
    public void While_the_scene_is_due_only_a_chest_on_a_scene_map_is_in_the_draw()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(1, OnFarm: true, Warded: false),   // a chest in the farmhouse
            new ChestSeat(2, OnFarm: false, Warded: false),  // a chest in the barn
        });
        Assert.True(BlightRule.InThiefDraw(hosts[0], thiefSceneDue: true));
        Assert.False(BlightRule.InThiefDraw(hosts[1], thiefSceneDue: true));
    }

    [Fact]
    public void Once_the_scene_has_played_every_chest_is_in_the_draw()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(1, OnFarm: true, Warded: false),
            new ChestSeat(2, OnFarm: false, Warded: false),
        });
        Assert.True(BlightRule.InThiefDraw(hosts[0], thiefSceneDue: false));
        Assert.True(BlightRule.InThiefDraw(hosts[1], thiefSceneDue: false));
    }

    [Fact]
    public void A_junimo_group_with_one_chest_on_a_scene_map_is_filmable_and_staged_there()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, OnFarm: false, Warded: false),  // in the greenhouse
            new ChestSeat(7, OnFarm: true, Warded: false),   // in the shed
        });
        Assert.Equal(new[] { false, true }, hosts.Select(h => BlightRule.InThiefDraw(h, thiefSceneDue: true)).ToArray());
        Assert.True(hosts[1].Filmable);
    }

    [Fact]
    public void A_junimo_group_with_no_chest_on_a_scene_map_waits_for_the_scene_to_play()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, OnFarm: false, Warded: false),
            new ChestSeat(7, OnFarm: false, Warded: false),
        });
        Assert.False(hosts.Any(h => BlightRule.InThiefDraw(h, thiefSceneDue: true)));
        Assert.Equal(new[] { true, false }, hosts.Select(h => BlightRule.InThiefDraw(h, thiefSceneDue: false)).ToArray());
    }

    [Fact]
    public void A_chest_that_does_not_host_its_stock_is_never_in_the_draw()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, OnFarm: true, Warded: false),
            new ChestSeat(7, OnFarm: true, Warded: false),
        });
        Assert.False(BlightRule.InThiefDraw(hosts[1], thiefSceneDue: true));
        Assert.False(BlightRule.InThiefDraw(hosts[1], thiefSceneDue: false));
    }
}
