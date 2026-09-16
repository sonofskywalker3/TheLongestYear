using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;
using static TheLongestYear.Tests.FairnessFixtures;

namespace TheLongestYear.Tests;

/// <summary>The fairness rule against the mine-depth ruling (2026-09-16): the model delays a mine
/// route by the days it takes to reach its floor, and the rule's answers must not move.</summary>
public class FairnessRuleMineDepthTests
{
    // Task 2b (2026-09-16): the model now delays a mine route by the days it takes to get there from
    // nothing. The values below were captured from the rule BEFORE that change, with undelayed tables,
    // and the rule must give exactly the same verdicts, landing days and setup days for the tables the
    // builder emits now: a delayed table beside the undelayed one, on direct and made routes alike.
    private const int DeepFloor = 80;
    private const int TightDeadline = 66;
    private static readonly int FloorDelay = MineDepth.DaysToReach(DeepFloor);

    /// <summary>The floor 80 route exactly as the builder emits it: its table delayed by the travel,
    /// the undelayed one kept.</summary>
    private static ObtainSource MineRoute()
        => MineDepth.WithTravel(new(SourceKind.MineNode, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { MineDepth.FloorPrefix + DeepFloor } }, "test"));

    /// <summary>A machine route as the builder emits it: its delayed table, and the same chain applied
    /// to its input's undelayed table.</summary>
    private static ObtainSource MachineRoute(DayTable lands, DayTable undelayed, string input)
        => new(SourceKind.Machine, lands, Reliability.Dependable, ObtainConditions.None, "test")
        {
            Inputs = new IReadOnlyList<string>[] { new[] { input } },
            UndelayedLands = undelayed.Equals(lands) ? null : undelayed,
        };

    private static ObtainSource MachineOnMine(string input)
        => MachineRoute(DayTable.Always.Delay(FloorDelay), DayTable.Always, input);

    public static TheoryData<DifficultyStep, int, int, bool, int?, int> MineDepthBefore() => Before(derived: false);

    /// <summary>The same table for a route made from the mine item, which differs in one cell: with
    /// the tight deadline and no floor reached, the input itself is late, so the made route is out
    /// with no landing rather than late.</summary>
    public static TheoryData<DifficultyStep, int, int, bool, int?, int> MadeFromMineBefore() => Before(derived: true);

    private static TheoryData<DifficultyStep, int, int, bool, int?, int> Before(bool derived)
    {
        var data = new TheoryData<DifficultyStep, int, int, bool, int?, int>();
        foreach (DifficultyStep level in new[] { DifficultyStep.Normal, DifficultyStep.Hard })
        {
            data.Add(level, 0, Deadline, true, 69, 8);
            data.Add(level, 40, Deadline, true, 65, 4);
            data.Add(level, 80, Deadline, true, 61, 0);
            data.Add(level, 100, Deadline, true, 61, 0);
            if (derived) data.Add(level, 0, TightDeadline, false, null, 0);
            else data.Add(level, 0, TightDeadline, false, 69, 8);
            data.Add(level, 40, TightDeadline, true, 65, 4);
        }
        data.Add(DifficultyStep.Easy, 0, Deadline, false, null, 0);
        data.Add(DifficultyStep.Easy, 40, Deadline, false, null, 0);
        data.Add(DifficultyStep.Easy, 80, Deadline, true, 61, 0);
        data.Add(DifficultyStep.Easy, 100, Deadline, true, 61, 0);
        data.Add(DifficultyStep.Extreme, 0, Deadline, true, 61, 0);
        data.Add(DifficultyStep.Extreme, 100, TightDeadline, true, 61, 0);
        return data;
    }

    private static void AssertBest(FairnessVerdict verdict, bool counts, int? landing, int added)
    {
        Assert.Equal(counts, verdict.Counts);
        RouteVerdict route = Assert.Single(verdict.Routes);
        Assert.Equal(landing, route.LandingDay);
        Assert.Equal(added, route.AddedDays);
    }

    [Theory]
    [MemberData(nameof(MineDepthBefore))]
    public void A_delayed_mine_route_keeps_its_verdict_at_every_depth(
        DifficultyStep level, int deepest, int deadline, bool counts, int? landing, int added)
    {
        var model = Model(MineRoute());
        AssertBest(FairnessRule.Judge(Item, Hit, deadline, level, Save(floor: deepest), model), counts, landing, added);
    }

    // Late Winter: the delayed table never lands past day 112, but the rule judges a direct route on
    // its undelayed table, so a deep save still counts it. Values from the pre-2b rule (83f8c3b).
    [Theory]
    [MemberData(nameof(LateWinterBefore))]
    public void A_delayed_mine_route_keeps_its_verdict_in_late_winter(
        int hit, DifficultyStep level, int deepest, bool counts, int? landing, int added)
    {
        var model = Model(MineRoute());
        AssertBest(FairnessRule.Judge(Item, hit, Deadline, level, Save(floor: deepest), model), counts, landing, added);
    }

    private const int WinterWeek3Hit = 104;
    private const int WinterWeek4Hit = 108;

    public static TheoryData<int, DifficultyStep, int, bool, int?, int> LateWinterBefore()
    {
        var data = new TheoryData<int, DifficultyStep, int, bool, int?, int>();
        foreach (DifficultyStep level in new[] { DifficultyStep.Normal, DifficultyStep.Hard })
        {
            data.Add(WinterWeek3Hit, level, 0, false, 113, 8);
            data.Add(WinterWeek3Hit, level, 40, true, 109, 4);
            data.Add(WinterWeek3Hit, level, 80, true, 105, 0);
            data.Add(WinterWeek3Hit, level, 100, true, 105, 0);
            data.Add(WinterWeek4Hit, level, 0, false, 117, 8);
            data.Add(WinterWeek4Hit, level, 40, false, 113, 4);
            data.Add(WinterWeek4Hit, level, 80, true, 109, 0);
            data.Add(WinterWeek4Hit, level, 100, true, 109, 0);
        }
        foreach (int hit in new[] { WinterWeek3Hit, WinterWeek4Hit })
        {
            data.Add(hit, DifficultyStep.Easy, 0, false, null, 0);
            data.Add(hit, DifficultyStep.Easy, 40, false, null, 0);
            data.Add(hit, DifficultyStep.Easy, 80, true, hit + 1, 0);
            data.Add(hit, DifficultyStep.Easy, 100, true, hit + 1, 0);
            data.Add(hit, DifficultyStep.Extreme, 0, true, hit + 1, 0);
        }
        return data;
    }

    [Fact]
    public void A_route_made_from_a_mine_item_still_counts_once_its_delayed_table_runs_past_the_year()
    {
        // The made route's delayed table never lands from start day 113 - travel, but it is judged on
        // its undelayed table, so a save deep enough counts it on the pre-2b day (109).
        var model = ModelOf(
            (Item, new[] { MachineOnMine(Ingredient) }),
            (Ingredient, new[] { MineRoute() }));
        AssertBest(FairnessRule.Judge(Item, WinterWeek4Hit, Deadline, DifficultyStep.Normal, Save(floor: 100), model), true, 109, 0);
        AssertBest(FairnessRule.Judge(Item, WinterWeek3Hit, Deadline, DifficultyStep.Normal, Save(floor: 100), model), true, 105, 0);
    }

    [Fact]
    public void A_recipe_taught_after_its_mine_ingredient_lands_on_the_teaching_day()
    {
        // A floor 80 ingredient, a recipe the TV teaches on day 63, a start on day 20, a save at floor
        // 100: the pre-2b rule said day 63. Taking the travel back out of the gated table must not
        // move the landing before the episode (the credit approach said day 56).
        const int TaughtDay = 63;
        const int HitBeforeStart = 19;
        ObtainSource mine = MineRoute();
        var snapshot = ModelOf((Ingredient, new[] { mine }));
        DayTable tv = DayTable.Available(d => d >= TaughtDay);
        ObtainConditions tvConditions = ObtainConditions.None with
        {
            Requires = new[] { "recipe:Test", "unlock:Queen of Sauce episode 9 (Sunday of week 9)" },
        };
        ObtainSource[] recipe = Derived.Input.Free.Both(Derived.Of(snapshot, Ingredient))
            .Emit(SourceKind.Cooking, t => t.Latest(tv), tvConditions, "test").ToArray();
        var model = ModelOf((Item, recipe), (Ingredient, new[] { mine }));
        foreach (DifficultyStep level in new[] { DifficultyStep.Normal, DifficultyStep.Hard, DifficultyStep.Extreme })
            AssertBest(FairnessRule.Judge(Item, HitBeforeStart, Deadline, level, Save(floor: 100), model), true, TaughtDay, 0);
        // Floor 40: the ingredient still needs 4 days, and those are added after the episode.
        AssertBest(FairnessRule.Judge(Item, HitBeforeStart, Deadline, DifficultyStep.Normal, Save(floor: 40), model), true, TaughtDay + 4, 4);
    }

    [Theory]
    [MemberData(nameof(MadeFromMineBefore))]
    public void A_route_made_from_a_delayed_mine_item_keeps_its_verdict_at_every_depth(
        DifficultyStep level, int deepest, int deadline, bool counts, int? landing, int added)
    {
        var model = ModelOf(
            (Item, new[] { MachineOnMine(Ingredient) }),
            (Ingredient, new[] { MineRoute() }));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, deadline, level, Save(floor: deepest), model);
        // Easy with the floor unreached: the machine is out because its input is, as before.
        if (!counts && landing is null) Assert.StartsWith("needs " + Ingredient, verdict.Routes[0].Reason);
        AssertBest(verdict, counts, landing, added);
    }

    [Theory]
    [MemberData(nameof(MadeFromMineBefore))]
    public void A_two_step_chain_on_a_delayed_mine_item_keeps_its_verdict_at_every_depth(
        DifficultyStep level, int deepest, int deadline, bool counts, int? landing, int added)
    {
        var model = ModelOf(
            (Item, new[] { MachineOnMine(Other) }),
            (Other, new[] { MachineOnMine(Ingredient) }),
            (Ingredient, new[] { MineRoute() }));
        AssertBest(FairnessRule.Judge(Item, Hit, deadline, level, Save(floor: deepest), model), counts, landing, added);
    }

    // A recipe needing the mine item AND a second item waits for the later of the two. Values captured
    // before the change.
    [Theory]
    [InlineData(64, 40, true, 68, 4)]    // second item early: the recipe inherits 4 of the 7 days
    [InlineData(70, 40, true, 74, 4)]    // second item late: the recipe inherits none
    [InlineData(64, 100, true, 64, 0)]
    [InlineData(70, 100, true, 70, 0)]
    [InlineData(64, 0, true, 72, 8)]
    public void A_recipe_with_a_delayed_mine_ingredient_keeps_its_verdict(
        int secondLands, int deepest, bool counts, int? landing, int added)
    {
        DayTable mine = DayTable.Always.Delay(FloorDelay);
        DayTable second = DayTable.Available(d => d >= secondLands);
        var recipe = new ObtainSource(SourceKind.Cooking, mine.Latest(second), Reliability.Dependable, ObtainConditions.None, "test")
        {
            Inputs = new IReadOnlyList<string>[] { new[] { Ingredient }, new[] { Other } },
            UndelayedLands = DayTable.Always.Latest(second),
        };
        var model = ModelOf(
            (Item, new[] { recipe }),
            (Ingredient, new[] { MineRoute() }),
            (Other, new[] { Route(available: d => d >= secondLands) }));
        AssertBest(FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: deepest), model), counts, landing, added);
    }

    // An input with two routes: the machine's tables were built from the input's COMBINED tables,
    // whichever route this save can use. Values from the pre-2b rule (83f8c3b), where the combined
    // table was day 61 and the machine day 64.
    private const int MachineDays = 3;

    [Theory]
    [InlineData(false, 64, 100, 64, 0)]   // the other route lands first after the delay, but this save cannot use it
    [InlineData(false, 64, 40, 68, 4)]
    [InlineData(true, 63, 100, 64, 0)]    // the mine route is the one this save cannot use
    [InlineData(true, 63, 40, 64, 0)]
    public void A_machine_on_a_two_route_input_keeps_its_verdict(
        bool mineBlocked, int otherLands, int deepest, int landing, int added)
    {
        const string Unusable = "mail:neverSent";
        ObtainSource mine = MineRoute();
        if (mineBlocked)
            mine = mine with { Conditions = mine.Conditions with { Requires = mine.Conditions.Requires.Append(Unusable).ToList() } };
        ObtainSource other = Route(available: d => d >= otherLands, requires: mineBlocked ? null : new[] { Unusable });
        var model = ModelOf(
            (Item, new[] { MachineRoute(mine.Lands.Earliest(other.Lands).Delay(MachineDays),
                mine.Undelayed.Earliest(other.Undelayed).Delay(MachineDays), Ingredient) }),
            (Ingredient, new[] { mine, other }));
        AssertBest(FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: deepest), model), true, landing, added);
    }

    [Fact]
    public void A_route_with_no_floor_and_no_mine_input_lands_on_its_own_table()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Extreme, Save(),
            Model(Route(SourceKind.MonsterDrop, requires: new[] { "location:SkullCave" }, available: d => d >= 70)));
        Assert.Equal(70, verdict.Routes[0].LandingDay);
    }
}
