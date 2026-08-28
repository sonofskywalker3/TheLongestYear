using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class FarmRuleWeekTests
{
    private static readonly Func<string, int?> NoEffort = _ => 1;

    [Fact]
    public void Artifact_in_town_is_week_1_and_desert_only_is_week_9()
    {
        Assert.Equal(1, ArtifactAvailability.Derive("(O)100", new List<RawArtifactSpot> { new("Town", "(O)100", 0.1) })!.EarliestWeek);
        Assert.Equal(9, ArtifactAvailability.Derive("(O)100", new List<RawArtifactSpot> { new("Desert", "(O)100", 0.1) })!.EarliestWeek);
    }

    [Fact]
    public void Animal_products_follow_the_building_tier()
    {
        var buildings = new List<RawBuilding> { new("Coop", null), new("Big Coop", "Coop"), new("Deluxe Coop", "Big Coop") };
        var animals = new List<RawFarmAnimal>
        {
            new("Chicken", "Coop", 800, 1, new[] { "(O)176" }, new[] { "(O)174" }),
            new("Duck", "Big Coop", 1200, 2, new[] { "(O)442" }, new[] { "(O)444" }),
            new("Rabbit", "Deluxe Coop", 8000, 4, new[] { "(O)440" }, new[] { "(O)446" }),
        };
        Assert.Equal(2, AnimalProductAvailability.Derive("(O)176", animals, buildings)!.EarliestWeek); // Egg
        Assert.Equal(2, AnimalProductAvailability.Derive("(O)174", animals, buildings)!.EarliestWeek); // Large Egg, same building week
        Assert.Equal(5, AnimalProductAvailability.Derive("(O)442", animals, buildings)!.EarliestWeek); // Duck Egg
        Assert.Equal(9, AnimalProductAvailability.Derive("(O)446", animals, buildings)!.EarliestWeek); // Rabbit's Foot
    }

    [Theory]
    [InlineData("default", 0)] [InlineData("s Farming 3", 3)] [InlineData("s Farming 8", 8)] [InlineData("null", 10)] [InlineData("f Robin 6", 10)]
    public void Machine_unlock_level(string unlock, int level) => Assert.Equal(level, ArtisanAvailability.MachineUnlockLevel(unlock));

    [Fact]
    public void Artisan_good_is_the_later_of_machine_and_input()
    {
        var data = new EffortData
        {
            MachineRules = new List<RawMachineRule> { new("(BC)12", "(O)254", Array.Empty<string>(), new[] { "(O)348" }, 10000, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "s Farming 8" },
        };
        // Keg is week 7; Melon is week 5; Melon Wine is week 7.
        ItemEffort e = ArtisanAvailability.Derive("(O)348", data, NoEffort, id => id == "(O)254" ? 5 : null)!;
        Assert.Equal(7, e.EarliestWeek);
        // Keg with a week-9 input is week 9.
        ItemEffort late = ArtisanAvailability.Derive("(O)348", data, NoEffort, _ => 9)!;
        Assert.Equal(9, late.EarliestWeek);
        // An input nothing placed leaves the good unplaced.
        Assert.Null(ArtisanAvailability.Derive("(O)348", data, NoEffort, _ => null)!.EarliestWeek);
    }

    [Fact]
    public void Dish_needs_the_kitchen_and_its_ingredients()
    {
        var data = new EffortData
        {
            CookingRecipes = new List<RawCookingRecipe> { new("Fried Egg", new[] { "176" }, "(O)194", "default") },
            Objects = new Dictionary<string, RawObjectEntry>(),
        };
        Assert.Equal(5, CookedDishAvailability.Derive("(O)194", data, NoEffort, hasKitchen: false, weekOf: _ => 2)!.EarliestWeek);
        Assert.Equal(5, CookedDishAvailability.Derive("(O)194", data, NoEffort, hasKitchen: true, weekOf: _ => 2)!.EarliestWeek);  // keep_kitchen never moves the week
        Assert.Null(CookedDishAvailability.Derive("(O)194", data, NoEffort, hasKitchen: false, weekOf: _ => null)!.EarliestWeek);
    }

    [Fact]
    public void Pond_product_is_the_fish_plus_a_season()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["128"] = new RawObjectEntry("Fish", -4, 200, false, new[] { "fish_ocean" }, "Pufferfish") },
            FishPonds = new List<RawFishPondRule> { new(new[] { "fish_ocean" }, new List<RawFishPondProduct> { new("(O)812", 1) }) },
        };
        ItemEffort e = FishPondAvailability.Derive("(O)812", data, NoEffort, _ => 5)!;
        Assert.Equal(9, e.EarliestWeek);
    }
}
