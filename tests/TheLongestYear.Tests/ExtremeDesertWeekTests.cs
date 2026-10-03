using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-02 (from elaineofshalott): on Extreme the bus counts as fixed from Spring
/// week 3. Only desert-derived dates move, each item keeping its own real time on top (crop growth,
/// the crop's own season). Hard keeps the desert at week 6; Easy and Normal are unchanged. The Skull
/// Cavern also needs the Skull Key (mine floor 120, week 4 at 30 floors a week), so on Extreme it
/// opens the week after: week 5.</summary>
public class ExtremeDesertWeekTests
{
    private const string Rhubarb = "(O)252";
    private const string Starfruit = "(O)268";
    private const string Beet = "(O)284";
    private const string CactusFruit = "(O)90";
    private const string Sandfish = "(O)164";
    private const string IridiumOre = "(O)386";
    private const string Parsnip = "(O)24";

    private static RawObjectEntry Obj(string name) => new("Basic", -75, 10, false, Array.Empty<string>(), name);

    /// <summary>The growth facts are Data/Crops 1.6 (dumped 2026-10-02): Rhubarb Spring 13 days,
    /// Starfruit Summer 13, Beet Fall 6, Cactus all seasons 12 regrowing; Cactus Fruit is Desert
    /// forage with no season.</summary>
    private static ItemAvailabilityModel Build(DifficultyStep step)
    {
        var pools = new ItemPools
        {
            Fish = new[] { new PoolItem(Sandfish, 75, 3, Array.Empty<Season>(), new[] { "Desert" }) },
            Metals = new[] { new PoolItem(IridiumOre, 100, 3, Array.Empty<Season>(), Array.Empty<string>()) },
        };
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry>
            {
                ["252"] = Obj("Rhubarb"), ["268"] = Obj("Starfruit"), ["284"] = Obj("Beet"),
                ["90"] = Obj("Cactus Fruit"), ["24"] = Obj("Parsnip"),
                ["188"] = Obj("Green Bean"), ["190"] = Obj("Cauliflower"), ["192"] = Obj("Potato"),
            },
            Crops = new List<RawCropGrowth>
            {
                new(Rhubarb, 13, false, false, new[] { Season.Spring }),
                new(Starfruit, 13, false, false, new[] { Season.Summer }),
                new(Beet, 6, false, false, new[] { Season.Fall }),
                new(CactusFruit, 12, true, false, new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter }),
                new(Parsnip, 4, false, false, new[] { Season.Spring }),
                new("(O)188", 10, true, true, new[] { Season.Spring }),
                new("(O)190", 12, false, false, new[] { Season.Spring }),
                new("(O)192", 6, false, false, new[] { Season.Spring }),
            },
            ForageSpawns = new List<RawSpawnEntry> { new(CactusFruit, null, null, "Desert") },
        };
        return ItemAvailabilityBuilder.Build(pools, effortData: data, mode: WeekModes.For(step), step: step);
    }

    [Fact]
    public void Extreme_desert_week_is_spring_week_3_and_hard_stays_6()
    {
        Assert.Equal(3, AvailabilityWeeks.DesertHardWeekFor(WeekMode.HardAll));
        Assert.Equal(6, AvailabilityWeeks.DesertHardWeekFor(WeekMode.HardGates));
        Assert.Equal(6, AvailabilityWeeks.DesertHardWeekFor(WeekMode.Pacing));
    }

    [Fact]
    public void Skull_cavern_waits_for_the_skull_key_on_extreme()
    {
        Assert.Equal(4, AvailabilityWeeks.MineFloorWeek(AvailabilityWeeks.SkullKeyFloor));
        Assert.Equal(5, AvailabilityWeeks.SkullCavernHardWeekFor(WeekMode.HardAll));
        Assert.Equal(6, AvailabilityWeeks.SkullCavernHardWeekFor(WeekMode.HardGates));
        Assert.Equal(5, AvailabilityWeeks.MineAreaHardWeek(MineAreas.SkullCavern, WeekMode.HardAll));
        Assert.Equal(6, AvailabilityWeeks.MineAreaHardWeek(MineAreas.SkullCavern, WeekMode.HardGates));
    }

    [Fact]
    public void Location_gating_answers_by_mode()
    {
        Assert.Equal(3, LocationGating.HardWeekFor("Desert", WeekMode.HardAll));
        Assert.Equal(5, LocationGating.HardWeekFor("SkullCave", WeekMode.HardAll));
        Assert.Equal(6, LocationGating.HardWeekFor("Desert", WeekMode.HardGates));
        Assert.Equal(6, LocationGating.HardWeekFor("SkullCave", WeekMode.HardGates));
        Assert.Equal(7, LocationGating.HardWeekFor("Sewer", WeekMode.HardAll));
        Assert.Equal(3, LocationGating.HardWeekForAny(new[] { "Desert", "Sewer" }, WeekMode.HardAll));
        Assert.Equal(9, LocationGating.WeekFor("Desert"));
    }

    [Theory]
    [InlineData(Rhubarb, 4, Season.Spring)]       // seeds week 3, planted Spring 15, 13 days
    [InlineData(Starfruit, 6, Season.Summer)]     // planted Summer 1, 13 days
    [InlineData(Beet, 9, Season.Fall)]            // planted Fall 1, 6 days
    [InlineData(CactusFruit, 3, Season.Spring)]   // desert forage from week 3
    [InlineData(Sandfish, 3, Season.Spring)]      // desert fish from week 3
    [InlineData(IridiumOre, 5, Season.Summer)]    // Skull Cavern, the week after floor 120
    [InlineData(Parsnip, 1, Season.Spring)]       // not desert: unchanged
    public void Extreme_dates_desert_items_from_week_3(string id, int week, Season gate)
    {
        ItemAvailability a = Build(DifficultyStep.Extreme).For(id);
        Assert.Equal(week, a.Week);
        Assert.Equal(week, a.GoalWeek);
        Assert.Equal(gate, a.Gate);
    }

    [Theory]
    [InlineData(Rhubarb, 11, Season.Fall)]
    [InlineData(Starfruit, 11, Season.Fall)]
    [InlineData(Beet, 10, Season.Fall)]
    [InlineData(CactusFruit, 6, Season.Summer)]
    [InlineData(Sandfish, 6, Season.Summer)]
    [InlineData(IridiumOre, 6, Season.Summer)]
    public void Hard_gates_are_unchanged(string id, int week, Season gate)
    {
        ItemAvailability a = Build(DifficultyStep.Hard).For(id);
        Assert.Equal(week, a.Week);
        Assert.Equal(gate, a.Gate);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    public void Easy_and_normal_are_unchanged(DifficultyStep step)
    {
        ItemAvailabilityModel model = Build(step);
        Assert.Equal(11, model.For(Rhubarb).Week);
        Assert.Equal(Season.Fall, model.For(Rhubarb).Gate);
        Assert.Equal(11, model.For(Starfruit).Week);
        Assert.Equal(10, model.For(Beet).Week);
        Assert.Equal(9, model.For(CactusFruit).Week);
        Assert.Equal(9, model.For(Sandfish).Week);
        Assert.Equal(9, model.For(IridiumOre).Week);
        // The hard week a Pacing model carries is still Hard's.
        Assert.Equal(6, model.For(CactusFruit).HardWeek);
    }

    private static readonly BundleGenerationTuning Tuning = new();

    private static bool SpringCropsDrawsRhubarb(DifficultyStep step)
    {
        Season[] spring = { Season.Spring };
        var pools = new ItemPools
        {
            Crops = new[]
            {
                new PoolItem(Parsnip, 35, 3, spring, Array.Empty<string>()),
                new PoolItem("(O)188", 35, 3, spring, Array.Empty<string>()),
                new PoolItem("(O)190", 35, 3, spring, Array.Empty<string>()),
                new PoolItem("(O)192", 35, 3, spring, Array.Empty<string>()),
                new PoolItem(Rhubarb, 220, 100, spring, Array.Empty<string>()),
            },
        };
        ItemAvailabilityModel model = Build(step);
        var spec = new BundleSpec("Pantry", 0, "Spring Crops", "Spring Crops", "O 495 30", 0, 4,
            Enumerable.Range(0, 4).Select(i => new BundleSlotSpec((900 + i).ToString(), 1, 0)).ToList());
        for (int seed = 0; seed < 20; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring),
                pools, Tuning, new Random(seed), availability: model);
            if (filled.Slots.Any(s => s.ItemId == Rhubarb)) return true;
        }
        return false;
    }

    [Fact]
    public void Spring_crops_can_draw_rhubarb_on_extreme() => Assert.True(SpringCropsDrawsRhubarb(DifficultyStep.Extreme));

    [Fact]
    public void Spring_crops_still_leaves_rhubarb_out_on_hard() => Assert.False(SpringCropsDrawsRhubarb(DifficultyStep.Hard));

    [Fact]
    public void Spring_crops_with_rhubarb_on_extreme_needs_no_reach_ramp()
    {
        var slots = new List<BundleSlot> { new(0, Parsnip), new(1, Rhubarb) };
        Assert.Null(BundleClassifier.SeasonalReachRamp(Season.Spring, 2, slots, 2, Build(DifficultyStep.Extreme)));
        Assert.NotNull(BundleClassifier.SeasonalReachRamp(Season.Spring, 2, slots, 2, Build(DifficultyStep.Hard)));
    }

    [Fact]
    public void Stretch_rule_stays_off_on_extreme()
        => Assert.False(StretchRule.Applies(Build(DifficultyStep.Extreme)));
}
