using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;
using static TheLongestYear.Tests.FairnessFixtures;

namespace TheLongestYear.Tests;

/// <summary>The fairness rule on routes made from other items: the inputs must count too, and their
/// setup days push the made route's landing.</summary>
public class FairnessRuleInputTests
{
    [Fact]
    public void A_machine_route_counts_only_when_its_input_is_obtainable_too()
    {
        // The Cheese case: the derived table says when the cheese lands, nothing about the cow. On
        // Easy the milk needs a barn, so a player with the press and no barn has no cheese route.
        ObtainSource cheese = Route(SourceKind.Machine, requires: new[] { "machine:(BC)16" },
            inputs: new[] { new[] { Ingredient } });
        ObtainSource milk = Route(SourceKind.Animal, requires: new[] { "building:Barn" });
        ObtainabilityModel model = ModelOf((Item, new[] { cheese }), (Ingredient, new[] { milk }));
        SaveSnapshot press = Save(machines: new[] { "(BC)16" });
        SaveSnapshot pressAndBarn = Save(machines: new[] { "(BC)16" }, buildings: new[] { "Barn" });
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, press, model));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, pressAndBarn, model));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Easy, press, model);
        Assert.Contains($"needs {Ingredient}, none obtainable", verdict.Routes[0].Reason);
        Assert.Contains("made from " + Ingredient, FairnessRule.Explain(verdict));
    }

    [Fact]
    public void A_recipe_route_needs_every_one_of_its_ingredient_groups()
    {
        ObtainSource dish = Route(SourceKind.Cooking, inputs: new[] { new[] { Ingredient }, new[] { Other } });
        ObtainSource free = Route();
        ObtainSource needsBarn = Route(SourceKind.Animal, requires: new[] { "building:Barn" });
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(),
            ModelOf((Item, new[] { dish }), (Ingredient, new[] { free }), (Other, new[] { needsBarn }))));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(),
            ModelOf((Item, new[] { dish }), (Ingredient, new[] { free }), (Other, new[] { free }))));
    }

    [Fact]
    public void One_obtainable_member_serves_a_whole_input_group()
    {
        // A machine that takes any of several items: one of them being reachable is enough.
        ObtainSource made = Route(SourceKind.Machine, inputs: new[] { new[] { Ingredient, Other } });
        ObtainSource needsBarn = Route(SourceKind.Animal, requires: new[] { "building:Barn" });
        ObtainabilityModel model = ModelOf((Item, new[] { made }), (Ingredient, new[] { needsBarn }), (Other, new[] { Route() }));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(), model));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(),
            ModelOf((Item, new[] { made }), (Ingredient, new[] { needsBarn }), (Other, new[] { needsBarn }))));
    }

    [Fact]
    public void An_input_cycle_terminates_and_rules_the_route_out()
    {
        // X is made from Y and Y from X: neither is a way in, and the rule must not recurse forever.
        ObtainabilityModel model = ModelOf(
            (Item, new[] { Route(SourceKind.Machine, inputs: new[] { new[] { Ingredient } }) }),
            (Ingredient, new[] { Route(SourceKind.Machine, inputs: new[] { new[] { Item } }) }));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(), model));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Extreme, Save(), model));
    }

    [Fact]
    public void Extreme_ignores_conditions_but_still_needs_an_obtainable_input()
    {
        // An item made from nothing obtainable is not obtainable at any level, so Extreme checks
        // inputs even though it ignores every condition (fix wave 2 ruling, 2026-09-15).
        ObtainSource made = Route(SourceKind.Machine, requires: new[] { "machine:(BC)16" },
            inputs: new[] { new[] { Ingredient } });
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Extreme, Save(),
            ModelOf((Item, new[] { made }))));
        // The input is judged at the same level: a chance-only ingredient serves on Extreme alone.
        ObtainabilityModel chanceInput = ModelOf(
            (Item, new[] { made }), (Ingredient, new[] { Route(SourceKind.Cart, Reliability.Chance) }));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Extreme, Save(), chanceInput));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, Save(machines: new[] { "(BC)16" }), chanceInput));
    }

    [Fact]
    public void An_inputs_setup_days_push_the_derived_landing()
    {
        ObtainSource x = Route(SourceKind.Machine, requires: new[] { "machine:(BC)12" },
            inputs: new[] { new[] { Ingredient } }, available: d => d >= 110);
        ObtainSource y = Route(SourceKind.Animal, requires: new[] { "building:Barn" },
            setup: new[] { new SetupStep("building:Barn", 3), new SetupStep("animal:Cow", 1) },
            available: d => d >= 108);
        ObtainabilityModel model = ModelOf((Item, new[] { x }), (Ingredient, new[] { y }));
        SaveSnapshot machineOnly = Save(machines: new[] { "(BC)12" });
        SaveSnapshot machineBarnCow = Save(machines: new[] { "(BC)12" }, buildings: new[] { "Barn" }, animals: new[] { "Cow" });

        // Y counts at 108 + 3 + 1 = 112 (right at the deadline). X's own landing (110) plus the
        // propagated 4 days is 114, past the deadline, so X must not count on the bare farm.
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, machineOnly, model));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, machineOnly, model);
        Assert.Equal(4, verdict.Routes[0].AddedDays);

        // With the barn and the cow already on the farm, Y needs no setup and X lands on its own day 110.
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, machineBarnCow, model));
    }

    [Fact]
    public void Parallel_input_groups_take_the_largest_setup_not_the_sum()
    {
        ObtainSource a = Route(SourceKind.Animal, requires: new[] { "building:Coop" },
            setup: new[] { new SetupStep("building:Coop", 3) });
        ObtainSource b = Route(SourceKind.Animal, requires: new[] { "building:Barn" },
            setup: new[] { new SetupStep("building:Barn", 5) });
        ObtainSource x = Route(SourceKind.Cooking, inputs: new[] { new[] { Ingredient }, new[] { Other } },
            available: d => d >= 100);
        ObtainabilityModel model = ModelOf((Item, new[] { x }), (Ingredient, new[] { a }), (Other, new[] { b }));
        SaveSnapshot save = Save();

        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, save, model);
        Assert.Equal(5, verdict.Routes[0].AddedDays);
        Assert.True(verdict.Counts);   // 100 + 5 = 105 <= 112
    }

    [Fact]
    public void The_cheapest_member_of_a_group_sets_its_days()
    {
        ObtainSource a = Route(SourceKind.Animal, requires: new[] { "building:Coop" },
            setup: new[] { new SetupStep("building:Coop", 5) });
        ObtainSource b = Route(SourceKind.Animal, requires: new[] { "building:Barn" },
            setup: new[] { new SetupStep("building:Barn", 1) });
        ObtainSource x = Route(SourceKind.Cooking, inputs: new[] { new[] { Ingredient, Other } },
            available: d => d >= 100);
        ObtainabilityModel model = ModelOf((Item, new[] { x }), (Ingredient, new[] { a }), (Other, new[] { b }));
        SaveSnapshot save = Save();

        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, save, model);
        Assert.Equal(1, verdict.Routes[0].AddedDays);
    }
}
