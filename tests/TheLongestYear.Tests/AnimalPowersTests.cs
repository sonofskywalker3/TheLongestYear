using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Pure rules of the animal powers (spec 2026-10-09 and its Rulings).</summary>
[Collection("i18n")]
public class AnimalPowersTests
{
    private static Func<string, bool> Owning(params string[] ids)
    {
        var set = new HashSet<string>(ids);
        return set.Contains;
    }

    // --- Catalog rows ---------------------------------------------------------------

    [Theory]
    [InlineData(AnimalPowers.MorningRounds, 600, null, null, null)]
    [InlineData("animal_warm_welcome_1", 200, null, null, null)]
    [InlineData("animal_warm_welcome_2", 450, "animal_warm_welcome_1", null, null)]
    [InlineData("animal_warm_welcome_3", 800, "animal_warm_welcome_2", null, null)]
    [InlineData(AnimalPowers.QuickGrowth, 350, null, null, null)]
    [InlineData(AnimalPowers.FastHatch, 200, null, null, null)]
    [InlineData(AnimalPowers.SnugBarn, 250, null, null, null)]
    [InlineData(AnimalPowers.BusyCoop, 600, null, null, null)]
    [InlineData(AnimalPowers.BusyBarn, 600, null, null, null)]
    [InlineData(AnimalPowers.FineFeathers, 250, null, "species:Duck", null)]
    [InlineData(AnimalPowers.LuckyRabbits, 450, null, "species:Rabbit", null)]
    [InlineData(AnimalPowers.TruffleNose, 400, null, "species:Pig", null)]
    [InlineData(AnimalPowers.SwiftHorse, 300, "early_horse", null, null)]
    [InlineData(AnimalPowers.HorseFlute, 350, "early_horse", null, null)]
    [InlineData(AnimalPowers.LoyalPet, 150, null, null, "pet:1")]
    public void Power_rows_have_the_ruled_price_and_gate(string id, long cost, string? prereq, string? meta, string? reach)
    {
        UpgradeDefinition def = UpgradeCatalog.TryGet(id)!;
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Animals, def.Category);
        Assert.Equal(cost, def.Cost);
        Assert.Equal(prereq, def.PrerequisiteId);
        Assert.Equal(meta, def.MetaRequirement);
        Assert.Equal(reach, def.RunReachRequirement);
    }

    [Fact]
    public void Every_power_has_a_name_and_a_description()
    {
        foreach (string id in AnimalPowers.AllIds)
        {
            UpgradeDefinition def = UpgradeCatalog.TryGet(id)!;
            Assert.False(def.DisplayName.StartsWith("upgrade."), $"missing name for {id}");
            Assert.False(def.Description.StartsWith("upgrade."), $"missing desc for {id}");
            Assert.DoesNotContain("—", def.Description);
        }
    }

    [Fact]
    public void All_ids_lists_fifteen_rows()
        => Assert.Equal(15, AnimalPowers.AllIds.Distinct().Count());

    // --- Busy Barnyard --------------------------------------------------------------

    [Theory]
    [InlineData("Duck", AnimalPowers.BusyCoop, 1)]
    [InlineData("Rabbit", AnimalPowers.BusyCoop, 2)]
    [InlineData("Goat", AnimalPowers.BusyBarn, 1)]
    [InlineData("Sheep", AnimalPowers.BusyBarn, 1)]
    public void Produce_target_comes_from_the_matching_row(string type, string row, int target)
    {
        Assert.Equal(target, AnimalPowers.ProduceTarget(type, Owning(row)));
        string other = row == AnimalPowers.BusyCoop ? AnimalPowers.BusyBarn : AnimalPowers.BusyCoop;
        Assert.Null(AnimalPowers.ProduceTarget(type, Owning(other)));
        Assert.Null(AnimalPowers.ProduceTarget(type, Owning()));
    }

    [Theory]
    [InlineData("Cow")]
    [InlineData("White Chicken")]
    [InlineData("Pig")]
    [InlineData("Dinosaur")]
    [InlineData("Ostrich")]
    [InlineData(null)]
    public void Other_animals_keep_their_vanilla_days(string? type)
        => Assert.Null(AnimalPowers.ProduceTarget(type, Owning(AnimalPowers.BusyCoop, AnimalPowers.BusyBarn)));

    [Theory]
    [InlineData("Duck", 2, 1)]
    [InlineData("Rabbit", 4, 2)]
    [InlineData("Goat", 2, 1)]
    [InlineData("Sheep", 3, 2)]
    [InlineData("Sheep", 1, 0)]   // a mod already made sheep daily: never a negative offset
    [InlineData("Cow", 1, 0)]
    public void Produce_day_offset_is_the_gap_to_the_target(string type, int daysToProduce, int offset)
        => Assert.Equal(offset, AnimalPowers.ProduceDayOffset(type, daysToProduce,
            Owning(AnimalPowers.BusyCoop, AnimalPowers.BusyBarn)));

    [Fact]
    public void No_offset_without_the_row()
        => Assert.Equal(0, AnimalPowers.ProduceDayOffset("Rabbit", 4, Owning()));

    // --- Quick Growth ---------------------------------------------------------------

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(10, 5)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Quick_growth_halves_the_nights_rounded_up(int daysToMature, int nights)
        => Assert.Equal(nights, AnimalPowers.QuickGrowthNights(daysToMature));

    [Theory]
    [InlineData(0, 1, 3, QuickGrowthStep.AgeOneDay)]   // vanilla stepped 0 -> 1, one more day
    [InlineData(1, 2, 3, QuickGrowthStep.GrowFully)]   // vanilla stepped to DTM-1: grow it now
    [InlineData(2, 3, 3, QuickGrowthStep.None)]        // vanilla already grew it
    [InlineData(0, 0, 3, QuickGrowthStep.None)]        // unfed night: age did not move
    [InlineData(4, 5, 10, QuickGrowthStep.AgeOneDay)]
    [InlineData(7, 8, 10, QuickGrowthStep.AgeOneDay)]  // 8 -> 9 = DTM-1; vanilla grows it next night
    [InlineData(5, 5, 0, QuickGrowthStep.None)]        // adult (Dinosaur DTM 0)
    public void Quick_growth_extra_step(int ageBefore, int ageAfterVanilla, int daysToMature, QuickGrowthStep expected)
        => Assert.Equal(expected, AnimalPowers.QuickGrowthExtraStep(ageBefore, ageAfterVanilla, daysToMature));

    // --- Warm Welcome ---------------------------------------------------------------

    [Fact]
    public void Warm_welcome_floor_is_the_highest_owned_tier()
    {
        Assert.Equal(0, AnimalPowers.WarmWelcomeFloor(Owning()));
        Assert.Equal(200, AnimalPowers.WarmWelcomeFloor(Owning("animal_warm_welcome_1")));
        Assert.Equal(400, AnimalPowers.WarmWelcomeFloor(Owning("animal_warm_welcome_1", "animal_warm_welcome_2")));
        Assert.Equal(600, AnimalPowers.WarmWelcomeFloor(Owning("animal_warm_welcome_1", "animal_warm_welcome_2", "animal_warm_welcome_3")));
    }

    [Theory]
    [InlineData(0, 400, 400)]
    [InlineData(450, 400, 450)]   // floor, never a cut
    [InlineData(100, 0, 100)]
    public void Warm_welcome_raises_to_the_floor_only(int current, int floor, int expected)
        => Assert.Equal(expected, AnimalPowers.WelcomedFriendship(current, floor));

    // --- Snug Barn ------------------------------------------------------------------

    [Theory]
    [InlineData(100, 10, 110)]
    [InlineData(250, 10, 255)]
    [InlineData(0, 3, 3)]
    public void Snug_barn_adds_the_drain_clamped(int before, int drain, int after)
        => Assert.Equal(after, AnimalPowers.SnugBarnHappiness(before, drain));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Snug_barn_day_is_rain_or_winter(bool raining, bool winterOrSnow, bool stuck)
        => Assert.Equal(stuck, AnimalPowers.IsStuckIndoorsDay(raining, winterOrSnow));

    // --- Swift Horse ----------------------------------------------------------------

    [Fact]
    public void Swift_horse_adds_one_speed_point_scaled_like_vanilla()
    {
        Assert.Equal(1.0 * 0.066 * 16, AnimalPowers.SwiftHorseBonus(0.066f, 16, diagonal: false), 4);
        Assert.Equal(1.0 * 0.066 * 16 * 0.707, AnimalPowers.SwiftHorseBonus(0.066f, 16, diagonal: true), 4);
    }

    // --- Loyal Pet ------------------------------------------------------------------

    [Theory]
    [InlineData(0.2f, 0.4f)]
    [InlineData(0.5f, 0.5f)]
    public void Gift_chance_is_at_least_forty_percent(float vanilla, float expected)
        => Assert.Equal(expected, AnimalPowers.GiftChance(vanilla), 3);

    [Theory]
    [InlineData(1000, 600)]
    [InlineData(400, 400)]
    [InlineData(0, 0)]
    public void Gift_threshold_drops_to_three_hearts_never_raises(int vanilla, int expected)
        => Assert.Equal(expected, AnimalPowers.GiftThreshold(vanilla));

    // --- Horse Flute ----------------------------------------------------------------

    [Fact]
    public void Horse_flute_needs_keep_horse_and_the_row()
    {
        Assert.True(AnimalPowers.GrantsHorseFlute(Owning("early_horse", AnimalPowers.HorseFlute)));
        Assert.False(AnimalPowers.GrantsHorseFlute(Owning(AnimalPowers.HorseFlute)));
        Assert.False(AnimalPowers.GrantsHorseFlute(Owning("early_horse")));
    }

    [Fact]
    public void Baseline_grants_the_flute_only_with_keep_horse_and_the_row()
    {
        var both = new MetaState { OwnedUpgrades = { "early_horse", AnimalPowers.HorseFlute } };
        Assert.True(RunBaselineBuilder.Build(both, new RunState(), PlayerSnapshot.Empty, 500).GrantHorseFlute);
        var fluteOnly = new MetaState { OwnedUpgrades = { AnimalPowers.HorseFlute } };
        Assert.False(RunBaselineBuilder.Build(fluteOnly, new RunState(), PlayerSnapshot.Empty, 500).GrantHorseFlute);
        Assert.False(RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500).GrantHorseFlute);
    }

    // --- Fast Hatch -----------------------------------------------------------------

    [Theory]
    [InlineData(0, false, false, 1.0)]
    [InlineData(0, false, true, 0.5)]
    [InlineData(1, false, true, 0.375)]
    [InlineData(0, true, true, 0.625)]
    [InlineData(2, false, false, 0.5625)]
    [InlineData(0, true, false, 1.25)]
    public void Machine_factor_multiplies_theme_and_fast_hatch(int fastStacks, bool slow, bool incubatorHalf, double factor)
        => Assert.Equal(factor, MachineReadyTime.Factor(fastStacks, slow, incubatorHalf), 6);
}
