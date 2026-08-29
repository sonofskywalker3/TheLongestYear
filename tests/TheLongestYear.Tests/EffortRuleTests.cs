using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ArtifactAvailabilityTests
{
    [Fact]
    public void Dinosaur_egg_is_the_minimum_over_its_spots()
    {
        var spots = new List<RawArtifactSpot>
        {
            new("Mountain", "(O)107", 0.005),   // 1 + 2
            new("SkullCave", "(O)107", 0.02),   // 7 + 1
        };
        ItemEffort? result = ArtifactAvailability.Derive("(O)107", spots);
        Assert.Equal(3, result!.Effort);
    }

    [Theory]
    [InlineData("Farm", 1)] [InlineData("Default", 1)] [InlineData("BusStop", 1)]
    [InlineData("UndergroundMine", 2)] [InlineData("Desert", 4)] [InlineData("SkullCave", 7)] [InlineData("IslandNorth", 7)]
    public void Reach_effort_by_location(string location, int effort)
        => Assert.Equal(effort, ArtifactAvailability.ReachEffort(location));

    [Theory]
    [InlineData(0.5, 0)] [InlineData(0.1, 0)] [InlineData(0.05, 1)] [InlineData(0.01, 2)] [InlineData(0.001, 3)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, ArtifactAvailability.ChanceStep(chance));

    [Fact]
    public void Unclaimed_item_is_null()
        => Assert.Null(ArtifactAvailability.Derive("(O)24", new List<RawArtifactSpot>()));
}

public class ItemQueryIdsTests
{
    [Fact]
    public void Plain_and_bare_ids_are_qualified()
    {
        Assert.Equal(new[] { "(O)107" }, ItemQueryIds.Expand("(O)107"));
        Assert.Equal(new[] { "(O)107" }, ItemQueryIds.Expand("107"));
    }

    [Fact]
    public void Random_items_ranges_expand()
        => Assert.Equal(new[] { "(O)96", "(O)97", "(O)98" }, ItemQueryIds.Expand("RANDOM_ITEMS (O) 96 98"));

    [Fact]
    public void Flavored_items_map_to_their_base_item()
    {
        Assert.Equal(new[] { "(O)348" }, ItemQueryIds.Expand("FLAVORED_ITEM Wine DROP_IN_ID"));
        Assert.Equal(new[] { "(O)DriedFruit" }, ItemQueryIds.Expand("FLAVORED_ITEM DriedFruit DROP_IN_ID"));
    }

    [Fact]
    public void Dried_mushroom_query_maps_to_the_plural_id()
        => Assert.Equal(new[] { "(O)DriedMushrooms" }, ItemQueryIds.Expand("FLAVORED_ITEM DriedMushroom DROP_IN_ID"));

    [Fact]
    public void Unknown_queries_and_blanks_expand_to_nothing()
    {
        Assert.Empty(ItemQueryIds.Expand("LOST_BOOK_OR_ITEM (O)770"));
        Assert.Empty(ItemQueryIds.Expand(null));
        Assert.Empty(ItemQueryIds.Expand("(W)4"));
    }
}

public class AnimalProductAvailabilityTests
{
    private static readonly List<RawBuilding> Buildings = new()
    {
        new("Coop", null), new("Big Coop", "Coop"), new("Deluxe Coop", "Big Coop"),
        new("Barn", null), new("Big Barn", "Barn"), new("Deluxe Barn", "Big Barn"),
    };

    private static readonly List<RawFarmAnimal> Animals = new()
    {
        new("White Chicken", "Coop", 800, 1, new[] { "(O)176" }, new[] { "(O)174" }),
        new("Pig", "Deluxe Barn", 16000, 1, new[] { "(O)430" }, new string[0]),
        new("Ostrich", "Barn", -1, 7, new[] { "(O)289" }, new string[0]),
    };

    [Fact]
    public void Egg_is_one() => Assert.Equal(1, AnimalProductAvailability.Derive("(O)176", Animals, Buildings)!.Effort);

    [Fact]
    public void Large_egg_adds_the_deluxe_step() => Assert.Equal(2, AnimalProductAvailability.Derive("(O)174", Animals, Buildings)!.Effort);

    [Fact]
    public void Truffle_is_deluxe_barn_three_plus_pig_price_two()
        => Assert.Equal(5, AnimalProductAvailability.Derive("(O)430", Animals, Buildings)!.Effort);

    [Fact]
    public void An_animal_that_is_not_for_sale_adds_the_incubator_step()
        => Assert.Equal(5, AnimalProductAvailability.Derive("(O)289", Animals, Buildings)!.Effort);

    [Fact]
    public void Unclaimed_item_is_null() => Assert.Null(AnimalProductAvailability.Derive("(O)24", Animals, Buildings));

    [Theory]
    [InlineData(800, 0)] [InlineData(999, 0)] [InlineData(1000, 1)] [InlineData(3999, 1)] [InlineData(4000, 2)]
    public void Price_steps(int price, int step) => Assert.Equal(step, AnimalProductAvailability.PriceStep(price));
}

public class ArtisanAvailabilityTests
{
    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            ["398"] = Obj(-79, "Grape"), ["613"] = Obj(-79, "Apple"), ["262"] = Obj(-75, "Wheat"), ["184"] = Obj(-6, "Milk"),
        },
        MachineRules = new List<RawMachineRule>
        {
            new("(BC)12", null, new[] { "category_fruit" }, new[] { "(O)348" }, 10000, -1),
            new("(BC)12", "(O)262", new string[0], new[] { "(O)346" }, 1750, -1),
            new("(BC)16", "(O)184", new string[0], new[] { "(O)424" }, 200, -1),
            new("(BC)10", null, new string[0], new[] { "(O)340" }, -1, 4),
        },
        MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "Farming 2", ["(BC)16"] = "Farming 6", ["(BC)10"] = "Farming 3" },
    };

    private static int? Effort(string id) => id switch { "(O)398" => 3, "(O)613" => 4, "(O)262" => 2, "(O)184" => 1, _ => null };

    [Fact]
    public void Wine_is_the_cheapest_fruit_plus_machine_plus_two_for_a_week_in_the_keg()
        => Assert.Equal(3 + 1 + 2, ArtisanAvailability.Derive("(O)348", Data(), Effort)!.Effort);

    [Fact]
    public void Cheese_is_milk_plus_a_level_six_press_and_no_time_step()
        => Assert.Equal(1 + 2 + 0, ArtisanAvailability.Derive("(O)424", Data(), Effort)!.Effort);

    [Fact]
    public void Honey_has_no_input_and_a_four_day_wait()
        => Assert.Equal(0 + 1 + 2, ArtisanAvailability.Derive("(O)340", Data(), Effort)!.Effort);

    [Fact]
    public void Unclaimed_output_is_null() => Assert.Null(ArtisanAvailability.Derive("(O)24", Data(), Effort));

    [Theory]
    [InlineData(null, 3)] [InlineData("null", 3)] [InlineData("default", 1)] [InlineData("Farming 2", 1)]
    [InlineData("s Farming 3", 1)] [InlineData("Farming 4", 2)] [InlineData("Farming 7", 2)] [InlineData("Farming 8", 3)] [InlineData("f Robin 6", 3)]
    public void Machine_unlock_effort(string? unlock, int effort) => Assert.Equal(effort, ArtisanAvailability.MachineUnlockEffort(unlock));

    [Theory]
    [InlineData(200, -1, 0)] [InlineData(1750, -1, 1)] [InlineData(10000, -1, 2)] [InlineData(-1, 4, 2)] [InlineData(-1, 1, 1)] [InlineData(-1, 14, 2)]
    public void Time_step(int minutes, int days, int step) => Assert.Equal(step, ArtisanAvailability.TimeStep(minutes, days));

    [Fact]
    public void A_bought_recipe_takes_its_price_week_and_a_friendship_recipe_its_hearts()
    {
        var data = new EffortData
        {
            MachineUnlocks = new Dictionary<string, string> { ["(BC)FishSmoker"] = "null", ["(BC)39"] = "f Krobus 3", ["(BC)12"] = "s Farming 8" },
            RecipePrices = new Dictionary<string, int> { ["(BC)FishSmoker"] = 10000 },
        };
        Assert.Equal(5, ArtisanAvailability.MachineWeek("(BC)FishSmoker", "null", data));   // 10,000g
        Assert.Equal(5, ArtisanAvailability.MachineWeek("(BC)39", "f Krobus 3", data));     // Krobus from the Sewer week
        Assert.Equal(7, ArtisanAvailability.MachineWeek("(BC)12", "s Farming 8", data));
        Assert.Equal(9, ArtisanAvailability.MachineWeek("(BC)182", "null", data));          // special-order mail, no price
    }

    [Fact]
    public void The_dehydrator_takes_the_earlier_of_pierre_and_the_cave()
    {
        var data = new EffortData
        {
            MachineUnlocks = new Dictionary<string, string> { ["(BC)Dehydrator"] = "null" },
            RecipePrices = new Dictionary<string, int> { ["(BC)Dehydrator"] = 5000 },
        };
        Assert.Equal(3, ArtisanAvailability.MachineWeek("(BC)Dehydrator", "null", data));
    }

    [Fact]
    public void Run_time_adds_whole_weeks()
    {
        // Wine: keg, Farming 8 (week 7), 10,000 minutes is under a week: still 7. Cask (14 days): plus 2.
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["398"] = new RawObjectEntry("Basic", -79, 80, false, new string[0], "Grape") },
            MachineRules = new List<RawMachineRule>
            {
                new("(BC)12", "(O)398", new string[0], new[] { "(O)348" }, 10000, -1),
                new("(BC)163", "(O)348", new string[0], new[] { "(O)AgedWine" }, -1, 14),
            },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "s Farming 8", ["(BC)163"] = "null" },
        };
        int? weekOf(string id) => id == "(O)398" ? 6 : id == "(O)348" ? 7 : null;
        int? effortOf(string id) => 1;
        Assert.Equal(7, ArtisanAvailability.Derive("(O)348", data, effortOf, weekOf)!.EarliestWeek);
        Assert.Equal(11, ArtisanAvailability.Derive("(O)AgedWine", data, effortOf, weekOf)!.EarliestWeek);   // cask week 9 + 2
    }
}

public class FishPondAvailabilityTests
{
    private static RawObjectEntry Fish(string name) => new("Fish", -4, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry> { ["145"] = Fish("Sunfish"), ["698"] = Fish("Sturgeon") },
        FishPonds = new List<RawFishPondRule>
        {
            new(new[] { "item_sturgeon" }, new[] { new RawFishPondProduct("(O)812", 1), new RawFishPondProduct("(O)814", 7) }),
            new(new[] { "category_fish" }, new[] { new RawFishPondProduct("(O)812", 1) }),
        },
    };

    private static int? Effort(string id) => id switch { "(O)145" => 2, "(O)698" => 9, _ => null };

    [Fact]
    public void Roe_takes_the_cheapest_fish_any_pond_accepts() => Assert.Equal(2 + 2, FishPondAvailability.Derive("(O)812", Data(), Effort)!.Effort);

    [Fact]
    public void A_population_gate_adds_a_step_per_three_fish() => Assert.Equal(9 + 2 + 2, FishPondAvailability.Derive("(O)814", Data(), Effort)!.Effort);

    [Theory] [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(4, 1)] [InlineData(5, 2)] [InlineData(7, 2)] [InlineData(10, 3)]
    public void Population_steps(int population, int steps) => Assert.Equal(steps, FishPondAvailability.PopulationSteps(population));

    [Fact]
    public void Unclaimed_is_null() => Assert.Null(FishPondAvailability.Derive("(O)24", Data(), Effort));
}

public class CookedDishAvailabilityTests
{
    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry> { ["184"] = Obj(-6, "Milk"), ["186"] = Obj(-6, "Large Milk"), ["24"] = Obj(-75, "Parsnip") },
        CookingRecipes = new List<RawCookingRecipe>
        {
            new("Fried Egg", new[] { "(O)176" }, "(O)194", "default"),
            new("Omelet", new[] { "(O)176", "-6" }, "(O)195", "null"),
            new("Parsnip Soup", new[] { "(O)24", "-6" }, "(O)199", "f Caroline 3"),
            new("Mystery", new[] { "(O)9999" }, "(O)200", "default"),
        },
    };

    private static int? Effort(string id) => id switch { "(O)176" => 1, "(O)184" => 1, "(O)186" => 2, "(O)24" => 2, _ => null };

    [Fact]
    public void Default_recipe_is_its_hardest_ingredient_plus_the_kitchen()
        => Assert.Equal(1 + 0 + 1, CookedDishAvailability.Derive("(O)194", Data(), Effort, hasKitchen: false)!.Effort);

    [Fact]
    public void A_kept_kitchen_drops_the_kitchen_cost()
        => Assert.Equal(1, CookedDishAvailability.Derive("(O)194", Data(), Effort, hasKitchen: true)!.Effort);

    [Fact]
    public void Category_ingredients_use_the_cheapest_member_and_tv_recipes_add_one()
        => Assert.Equal(1 + 1 + 1, CookedDishAvailability.Derive("(O)195", Data(), Effort, false)!.Effort);

    [Fact]
    public void Friendship_recipes_add_two()
        => Assert.Equal(2 + 2 + 1, CookedDishAvailability.Derive("(O)199", Data(), Effort, false)!.Effort);

    [Fact]
    public void An_unrecognised_ingredient_makes_the_dish_extreme()
    {
        ItemEffort? r = CookedDishAvailability.Derive("(O)200", Data(), Effort, false);
        Assert.Equal(CookedDishAvailability.ExtremeEffort, r!.Effort);
        Assert.Contains("(O)9999", r.Basis);
    }

    [Theory] [InlineData("default", 0)] [InlineData("null", 1)] [InlineData("s Cooking 3", 1)] [InlineData("s Farming 6", 2)] [InlineData("f Gus 7", 2)] [InlineData("e 1", 3)]
    public void Unlock_effort(string unlock, int effort) => Assert.Equal(effort, CookedDishAvailability.UnlockEffort(unlock));
}

public class CropForageAvailabilityTests
{
    [Theory]
    [InlineData("(O)24", 4, false, false, 1)]
    [InlineData("(O)400", 8, true, false, 3)]
    [InlineData("(O)304", 11, true, true, 3)]
    [InlineData("(O)254", 13, false, false, 3)]
    public void Crops_score_growth_and_regrowth(string id, int days, bool regrows, bool trellis, int effort)
    {
        var crops = new List<RawCropGrowth> { new(id, days, regrows, trellis) };
        Assert.Equal(effort, CropForageAvailability.DeriveCrop(id, crops)!.Effort);
    }

    [Fact]
    public void Forage_in_many_places_is_one_and_secret_woods_only_is_three()
    {
        var spawns = new List<RawSpawnEntry>
        {
            new("(O)16", Season.Spring, null, "Forest"), new("(O)16", Season.Spring, null, "Mountain"),
            new("(O)257", Season.Spring, null, "Woods"),
        };
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)16", spawns)!.Effort);
        Assert.Equal(3, CropForageAvailability.DeriveForage("(O)257", spawns)!.Effort);
        Assert.Null(CropForageAvailability.DeriveForage("(O)24", spawns));
    }
}
