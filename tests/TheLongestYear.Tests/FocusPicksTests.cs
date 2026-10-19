using System.Collections.Generic;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusPicksTests
{
    private static FocusHand Hand(FocusTheme t, params string[] chains)
    {
        var h = new FocusHand { Theme = t };
        h.Chains.AddRange(chains);
        return h;
    }

    private static List<FocusHand> Hands(string[] tools, string[] buildings, string[] knacks) => new()
    {
        Hand(FocusTheme.Tools, tools), Hand(FocusTheme.Buildings, buildings), Hand(FocusTheme.Knacks, knacks),
    };

    [Fact]
    public void Picks_run_tools_then_buildings_then_knacks_and_skip_empty_hands()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new[] { "hoe", "axe" }, new string[0], new[] { "stash" }));
        Assert.Equal(FocusTheme.Tools, FocusPicks.NextToPick(run)!.Theme);
        Assert.True(FocusPicks.Choose(run, FocusTheme.Tools, "axe"));
        Assert.Equal(FocusTheme.Knacks, FocusPicks.NextToPick(run)!.Theme);
        Assert.True(FocusPicks.Choose(run, FocusTheme.Knacks, "stash"));
        Assert.Null(FocusPicks.NextToPick(run));
        Assert.False(FocusPicks.Pending(run));
    }

    [Fact]
    public void All_empty_hands_are_not_pending()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new string[0], new string[0], new string[0]));
        Assert.False(FocusPicks.NeedsDeal(run));
        Assert.Null(FocusPicks.NextToPick(run));
        Assert.False(FocusPicks.Pending(run));
    }

    [Fact]
    public void A_fresh_run_state_needs_a_deal()
    {
        var run = new RunState();
        Assert.True(FocusPicks.NeedsDeal(run));
        Assert.True(FocusPicks.Pending(run));
    }

    [Fact]
    public void A_pick_is_final()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new[] { "hoe", "axe" }, new[] { "silo" }, new[] { "stash" }));
        Assert.True(FocusPicks.Choose(run, FocusTheme.Tools, "hoe"));
        Assert.False(FocusPicks.Choose(run, FocusTheme.Tools, "axe"));
        Assert.Equal("hoe", run.FocusHandFor(FocusTheme.Tools)!.Chosen);
    }

    [Fact]
    public void A_chain_outside_the_hand_cannot_be_chosen()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new[] { "hoe" }, new[] { "silo" }, new[] { "stash" }));
        Assert.False(FocusPicks.Choose(run, FocusTheme.Tools, "axe"));
        Assert.Null(run.FocusHandFor(FocusTheme.Tools)!.Chosen);
    }

    [Fact]
    public void A_new_run_needs_a_new_deal()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new[] { "hoe" }, new[] { "silo" }, new[] { "stash" }));
        Assert.False(FocusPicks.NeedsDeal(run));
        run.BeginNewRun(9);
        Assert.True(FocusPicks.NeedsDeal(run));
        Assert.Empty(run.FocusHands);
    }

    [Fact]
    public void Focus_state_round_trips_through_json()
    {
        var run = new RunState();
        FocusPicks.Install(run, Hands(new[] { "hoe", "axe" }, new[] { "silo" }, new[] { "stash" }));
        FocusPicks.Choose(run, FocusTheme.Tools, "axe");
        var tools = run.FocusHandFor(FocusTheme.Tools)!;
        tools.Bought = true;
        tools.BoughtUpgradeId = "keep_axe_2";

        var back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(run.FocusDealtRun, back.FocusDealtRun);
        Assert.Equal(3, back.FocusHands.Count);
        var t = back.FocusHandFor(FocusTheme.Tools)!;
        Assert.Equal(new[] { "hoe", "axe" }, t.Chains);
        Assert.Equal("axe", t.Chosen);
        Assert.True(t.Bought);
        Assert.Equal("keep_axe_2", t.BoughtUpgradeId);
        Assert.Null(back.FocusHandFor(FocusTheme.Knacks)!.Chosen);
    }
}
