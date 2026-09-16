using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;
using static TheLongestYear.Tests.FairnessFixtures;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, section 1: every cell of the "which routes count" table,
/// against a fake model built from hand-made sources.</summary>
public class FairnessRuleTests
{
    private static bool Counts(DifficultyStep level, SaveSnapshot save, params ObtainSource[] sources)
        => FairnessRule.Counts(Item, Hit, Deadline, level, save, Model(sources));

    [Fact]
    public void No_model_means_everything_counts()
        => Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(), null));

    [Fact]
    public void No_source_at_all_never_counts()
        => Assert.False(FairnessRule.Counts("(O)1", Hit, Deadline, DifficultyStep.Extreme, Save(), Model()));

    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Hard, false)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void A_chance_route_counts_only_on_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(SourceKind.Cart, Reliability.Chance)));

    [Theory]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void An_unresolved_route_counts_only_on_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(unresolved: true)));

    [Fact]
    public void An_island_route_never_counts()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(island: true)));

    [Fact]
    public void A_year_two_route_that_is_not_tv_never_counts()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(SourceKind.Shop, yearTwo: true)));

    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Hard, true)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void A_year_two_queen_of_sauce_route_counts_on_hard_and_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(SourceKind.Cooking, yearTwo: true,
            requires: new[] { "recipe:Bruschetta", "unlock:Queen of Sauce episode 31 (Sunday of week 31)" })));

    [Fact]
    public void A_route_landing_after_the_deadline_does_not_count()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(available: d => d >= 113)));

    [Fact]
    public void The_route_starts_the_day_after_the_hit()
    {
        Assert.False(FairnessRule.Counts(Item, Hit, Hit, DifficultyStep.Normal, Save(), Model(Route(available: d => d == Hit))));
        Assert.True(FairnessRule.Counts(Item, Hit, Hit + 1, DifficultyStep.Normal, Save(), Model(Route(available: d => d == Hit + 1))));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    [InlineData(DifficultyStep.Hard)]
    public void A_missing_recipe_with_a_friendship_unlock_rules_the_route_out(DifficultyStep level)
    {
        ObtainSource route = Route(SourceKind.Cooking, requires: new[] { "recipe:Cheese Cauliflower", "unlock:f Pam 3" });
        Assert.False(Counts(level, Save(), route));
        Assert.True(Counts(level, Save(recipes: new[] { "Cheese Cauliflower" }), route));
    }

    [Fact]
    public void A_missing_recipe_the_shop_or_the_tv_teaches_is_priced_by_the_table_not_ruled_out()
    {
        Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:Omelet", "unlock:shop" })));
        Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:Omelet", "unlock:Queen of Sauce episode 4 (Sunday of week 4)" })));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    [InlineData(DifficultyStep.Hard)]
    public void A_crafting_requirement_counts_only_when_the_recipe_is_known(DifficultyStep level)
    {
        // SpawnSources writes "crafting:Crab Pot" and MadeSources "crafting:Tapper"; before this
        // branch existed neither prefix was parsed, so both were silently met.
        ObtainSource route = Route(SourceKind.CrabPot, requires: new[] { "crafting:Crab Pot" });
        Assert.False(Counts(level, Save(), route));
        Assert.True(Counts(level, Save(recipes: new[] { "Crab Pot" }), route));
    }

    [Fact]
    public void Extreme_ignores_a_crafting_requirement_like_every_other_condition()
        => Assert.True(Counts(DifficultyStep.Extreme, Save(), Route(SourceKind.CrabPot, requires: new[] { "crafting:Crab Pot" })));

    [Fact]
    public void Extreme_ignores_conditions()
        => Assert.True(Counts(DifficultyStep.Extreme, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:X", "unlock:f Pam 3", "machine:(BC)12", "mail:ccPantry" })));

    [Fact]
    public void A_missing_machine_rules_out_on_easy_and_costs_a_day_on_normal_when_craftable()
    {
        ObtainSource route = Route(SourceKind.Machine, requires: new[] { "machine:(BC)12" }, available: d => d >= Hit + 1);
        Assert.False(Counts(DifficultyStep.Easy, Save(), route));
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(craftable: new[] { "(BC)12" }), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(machines: new[] { "(BC)12" }), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(craftable: new[] { "(BC)12" }), Model(route));
        Assert.Equal(SabotageTuning.MachineCraftDays, verdict.Routes[0].AddedDays);
    }

    [Fact]
    public void A_missing_building_rules_out_on_easy_and_adds_its_days_on_normal()
    {
        // Real model shape (MadeSources.Animals/AnimalSetup): Requires holds only "building:Coop";
        // the animal itself and its build-days are Setup steps, not Requires.
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Coop" },
            setup: new[] { new SetupStep("building:Coop", 3), new SetupStep("animal:Chicken", 1) }, available: d => d >= 110);
        Assert.False(Counts(DifficultyStep.Easy, Save(), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(buildings: new[] { "Coop" }, animals: new[] { "Chicken" }), route));
        // Normal: lands 110 + 3 + 1 = 114, past Winter 28.
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        // With the coop but no chicken: 110 + 1 = 111.
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }), route));
    }

    [Fact]
    public void A_purchasable_animal_still_costs_a_setup_day_on_normal_even_though_it_is_not_in_requires()
    {
        // Regression for fix round 1 finding 2: MadeSources.Animals never puts a purchasable animal
        // in Requires at all, only in Setup, so the day must be priced off Setup.
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Coop" },
            setup: new[] { new SetupStep("building:Coop", 3), new SetupStep("animal:Chicken", 1) }, available: d => d >= 110);
        Assert.False(Counts(DifficultyStep.Easy, Save(buildings: new[] { "Coop" }), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(buildings: new[] { "Coop" }), Model(route));
        Assert.True(verdict.Counts);
        Assert.Equal(1, verdict.Routes[0].AddedDays);
    }

    [Fact]
    public void An_animal_that_is_not_sold_rules_out_unless_owned()
    {
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Barn", "animal:Ostrich (not sold)" },
            setup: new[] { new SetupStep("building:Barn", 3), new SetupStep("animal:Ostrich", 1) });
        Assert.False(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }, animals: new[] { "Ostrich" }), route));
    }

    [Fact]
    public void An_owned_only_route_counts_when_the_animal_is_owned_and_not_otherwise()
    {
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Barn", "animal:Dinosaur (not sold)" },
            setup: new[] { new SetupStep("building:Barn", 3), new SetupStep("animal:Dinosaur", 13) });
        route = route with { Conditions = route.Conditions with { OwnedOnly = true } };
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }, animals: new[] { "Dinosaur" }), route));
        Assert.False(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }), route));
    }

    [Fact]
    public void Friendship_days_are_added_on_normal_when_the_animal_is_not_there_yet()
    {
        // Real model shape: the friendship step names the animal id, which can contain a space
        // ("White Chicken"), so the count must split at the LAST space (fix round 1 finding 1).
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Coop" },
            setup: new[] { new SetupStep("building:Coop", 3), new SetupStep("animal:White Chicken", 1), new SetupStep("friendship:White Chicken 200", 14) },
            available: d => d >= 100);
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }, animals: new[] { "White Chicken" },
            friendship: new Dictionary<string, int> { ["White Chicken"] = 500 }), route));               // 100
        Assert.False(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }, animals: new[] { "White Chicken" },
            friendship: new Dictionary<string, int> { ["White Chicken"] = 0 }), route));                 // 100 + 14 = 114
        Assert.False(Counts(DifficultyStep.Easy, Save(buildings: new[] { "Coop" }, animals: new[] { "White Chicken" },
            friendship: new Dictionary<string, int> { ["White Chicken"] = 0 }), route));
    }

    [Fact]
    public void A_missing_skill_rules_out_on_easy_and_adds_the_table_days_on_normal()
    {
        ObtainSource route = Route(SourceKind.Fish, skill: "Fishing", skillLevel: 6, available: d => d >= 100);
        Assert.False(Counts(DifficultyStep.Easy, Save(fishing: 3), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(fishing: 6), route));
        // Normal: 100 + (10 - 3) = 107, in time.
        Assert.True(Counts(DifficultyStep.Normal, Save(fishing: 3), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(fishing: 3), Model(route));
        Assert.Equal(7, verdict.Routes[0].AddedDays);
        // Normal from level 0: 100 + 10 = 110, in time; from level 0 with a later landing, out.
        Assert.False(Counts(DifficultyStep.Normal, Save(fishing: 0), Route(SourceKind.Fish, skill: "Fishing", skillLevel: 6, available: d => d >= 105)));
    }

    [Fact]
    public void A_mine_floor_not_reached_rules_out_on_easy_and_costs_a_day_per_ten_floors_on_normal()
    {
        ObtainSource route = Route(SourceKind.MineNode, requires: new[] { "mines:floor 80" }, available: d => d >= 100);
        Assert.False(Counts(DifficultyStep.Easy, Save(floor: 40), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(floor: 80), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 40), Model(route));
        Assert.Equal(4, verdict.Routes[0].AddedDays);
        Assert.True(verdict.Counts);
        // Hard adds days the same as Normal for a condition this table names.
        FairnessVerdict hard = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Hard, Save(floor: 40), Model(route));
        Assert.Equal(4, hard.Routes[0].AddedDays);
        Assert.True(hard.Counts);
        Assert.Equal(12, FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 0),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 120" }))).Routes[0].AddedDays);
        Assert.Equal(1, FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 39),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 40" }))).Routes[0].AddedDays);
    }

    [Fact]
    public void Skull_cavern_is_a_condition_never_a_wait()
    {
        ObtainSource route = Route(SourceKind.MonsterDrop, requires: new[] { "location:SkullCave" });
        Assert.False(Counts(DifficultyStep.Easy, Save(mining: 5), route));                                   // desert shut
        Assert.False(Counts(DifficultyStep.Easy, Save(mail: new[] { "ccVault" }, mining: 1), route));         // no staircase
        Assert.True(Counts(DifficultyStep.Easy, Save(mail: new[] { "ccVault" }, mining: 2), route));
        Assert.False(Counts(DifficultyStep.Normal, Save(mining: 10), route));                                 // the bus is never priced
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(mail: new[] { "ccVault" }, mining: 0), Model(route));
        Assert.True(verdict.Counts);
        Assert.Equal(2, verdict.Routes[0].AddedDays);   // Mining 2 from 0 = 2 days
    }

    [Fact]
    public void The_desert_needs_the_bus()
    {
        ObtainSource route = Route(SourceKind.Shop, requires: new[] { "location:Desert" });
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(mail: new[] { "ccVault" }), route));
    }

    [Theory]
    [InlineData("shop:DesertFestival_Vincent")]
    [InlineData("shop:DesertTrade")]
    [InlineData("shop:Sandy")]
    [InlineData("shop:Casino")]
    public void A_desert_shop_needs_the_bus(string shop)
    {
        var model = Model(Route(kind: SourceKind.Shop, requires: new[] { shop }));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
        SaveSnapshot bus = SaveSnapshot.Empty with { MailFlags = new HashSet<string> { "ccVault" } };
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, bus, model));
    }

    [Fact]
    public void A_town_shop_needs_nothing()
    {
        var model = Model(Route(kind: SourceKind.Shop, requires: new[] { "shop:SeedShop" }));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
    }

    [Fact]
    public void The_raccoon_shop_needs_the_raccoons_moved_in()
    {
        var model = Model(Route(kind: SourceKind.Shop, requires: new[] { "shop:Raccoon" }));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
        SaveSnapshot movedIn = SaveSnapshot.Empty with { MailFlags = new HashSet<string> { "raccoonMovedIn" } };
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, movedIn, model));
    }

    [Fact]
    public void A_mail_flag_is_met_or_not()
    {
        ObtainSource route = Route(SourceKind.GreenhouseCrop, requires: new[] { "item:(O)472", "mail:ccPantry" });
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(mail: new[] { "ccPantry" }), route));
    }

    [Fact]
    public void Conditions_the_table_does_not_name_count_as_met()
        => Assert.True(Counts(DifficultyStep.Easy, Save(), Route(requires: new[] { "item:(O)472", "guild:Slimes 1000 kills", "location:Beach", "tapper on tree 1, 7 days" })));

    [Fact]
    public void A_pond_route_never_counts_below_extreme()
    {
        string[] requires = { "building:Fish Pond", "pond population 3" };
        SaveSnapshot withPond = Save(buildings: new[] { "Fish Pond" });
        Assert.False(Counts(DifficultyStep.Easy, withPond, Route(SourceKind.FishPond, requires: requires)));
        Assert.False(Counts(DifficultyStep.Normal, withPond, Route(SourceKind.FishPond, requires: requires)));
        Assert.True(Counts(DifficultyStep.Extreme, withPond, Route(SourceKind.FishPond, requires: requires)));
    }

    [Fact]
    public void One_counting_route_is_enough()
        => Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cart, Reliability.Chance), Route(SourceKind.Forage)));

    [Theory]
    [InlineData(60, DifficultyStep.Easy, 84)]      // Fall 4: the end of Fall
    [InlineData(60, DifficultyStep.Normal, 112)]
    [InlineData(90, DifficultyStep.Easy, 112)]     // Winter 6: the end of Winter
    [InlineData(90, DifficultyStep.Hard, 112)]
    public void Reversion_deadline_by_level(int hitDay, DifficultyStep level, int deadline)
        => Assert.Equal(deadline, FairnessRule.ReversionDeadline(hitDay, level));

    [Fact]
    public void Tamper_deadline_is_winter_28()
        => Assert.Equal(112, FairnessRule.TamperDeadline);

    [Fact]
    public void Everyday_world_requirements_count_as_met()
    {
        var model = Model(Route(requires: new[] { "weeds:any" }));
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
    }

    [Fact]
    public void A_tide_pool_route_needs_the_bridge()
    {
        var model = Model(Route(requires: new[] { "mail:beachBridgeFixed" }));
        Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
        SaveSnapshot fixedBridge = SaveSnapshot.Empty with { MailFlags = new HashSet<string> { "beachBridgeFixed" } };
        Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, fixedBridge, model));
    }

    [Fact]
    public void Explain_names_every_route_and_the_verdict()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 40),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 80" }), Route(SourceKind.Cart, Reliability.Chance)));
        string text = FairnessRule.Explain(verdict);
        Assert.Contains("counts", text);
        Assert.Contains("+4 day", text);
        Assert.Contains("chance route", text);
    }
}
