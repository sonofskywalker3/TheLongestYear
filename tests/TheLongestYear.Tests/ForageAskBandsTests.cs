using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Forage on the same basis x band rule as fish (Jeff, 2026-09-04). The basis is the
/// measured seasonal mean from ForageAskLimits (a ruling stands in for a measurement where one
/// exists), reachable by the slot's deadline; a Wild Seed crop whose seeds grow by the deadline is
/// farmable and takes the full 99-stack as its basis.</summary>
public class ForageAskBandsTests
{
    private const string RainbowShell = "(O)394";      // measured, never farmable
    private const string CommonMushroom = "(O)404";    // measured in Spring, Fall Wild Seeds
    private const string PurpleMushroom = "(O)422";    // ruled ceiling 5
    private const string WildHorseradish = "(O)16";    // Spring Wild Seeds
    private const string Wood = "(O)388";              // nothing bands it

    private static DifficultyProfile Profile(DifficultyStep stack)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = stack }, new GameplayConfig());

    [Fact]
    public void Coverage_follows_the_measured_table_the_rulings_and_the_wild_seed_list()
    {
        Assert.True(ForageAskBasis.Covers(RainbowShell));
        Assert.True(ForageAskBasis.Covers(PurpleMushroom));
        Assert.True(ForageAskBasis.Covers(WildHorseradish));
        Assert.False(ForageAskBasis.Covers(Wood));
    }

    [Fact]
    public void A_measured_item_takes_the_most_generous_mean_reached_by_the_deadline()
    {
        double? summer = ForageAskLimits.MeanFor(Season.Summer, RainbowShell);
        Assert.NotNull(summer);
        Assert.Equal(summer, ForageAskBasis.BasisByDeadline(RainbowShell, Season.Summer));
        Assert.True(ForageAskBasis.BasisByDeadline(RainbowShell, Season.Winter) >= summer);
    }

    [Fact]
    public void A_ruling_is_a_ceiling_so_its_basis_is_the_ceiling_over_eighty_percent()
    {
        Assert.Equal(5 / AskBands.Ceiling, ForageAskBasis.BasisByDeadline(PurpleMushroom, Season.Spring)!.Value, 6);
    }

    [Fact]
    public void A_wild_seed_crop_is_farmable_once_its_seeds_grow_and_measured_before_that()
    {
        Assert.Equal(ForageAskBasis.FarmableBasis, ForageAskBasis.BasisByDeadline(CommonMushroom, Season.Fall));
        Assert.Equal(ForageAskBasis.FarmableBasis, ForageAskBasis.BasisByDeadline(CommonMushroom, null));
        Assert.Equal(ForageAskLimits.MeanFor(Season.Spring, CommonMushroom), ForageAskBasis.BasisByDeadline(CommonMushroom, Season.Spring));
        Assert.Equal(ForageAskBasis.FarmableBasis, ForageAskBasis.BasisByDeadline(WildHorseradish, Season.Spring));
    }

    private static BundleSpec Spec(params (string Id, int Stack, int Quality)[] slots)
        => new("Crafts Room", 2, "Spring Foraging", "Spring Foraging", "O 495 30", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, s.Quality)).ToList());

    [Fact]
    public void Ninety_common_mushrooms_on_a_spring_deadline_can_no_longer_happen()
    {
        int ceiling = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;
        for (int seed = 0; seed < 100; seed++)
        {
            BundleSpec banded = QuantityAskPass.Apply(Spec((CommonMushroom, 1, 0)), Profile(DifficultyStep.Extreme), _ => Season.Spring, new Random(seed));
            Assert.InRange(banded.Slots[0].Stack, 1, ceiling);
        }
    }

    [Theory]
    [InlineData(DifficultyStep.Normal, 20, 50)]
    [InlineData(DifficultyStep.Extreme, 65, 80)]
    public void A_farmable_forage_rolls_its_band_of_a_full_stack(DifficultyStep step, int low, int high)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            BundleSpec banded = QuantityAskPass.Apply(Spec((CommonMushroom, 1, 0)), Profile(step), _ => Season.Fall, new Random(seed));
            Assert.InRange(banded.Slots[0].Stack, low, high);
        }
    }

    [Fact]
    public void Rainbow_shell_on_extreme_never_exceeds_eighty_percent_of_its_summer_mean()
    {
        int ceiling = ForageAskLimits.MeasuredMaxAsk(Season.Summer, RainbowShell)!.Value;
        for (int seed = 0; seed < 100; seed++)
        {
            BundleSpec banded = QuantityAskPass.Apply(Spec((RainbowShell, 1, 0)), Profile(DifficultyStep.Extreme), _ => Season.Summer, new Random(seed));
            Assert.InRange(banded.Slots[0].Stack, 1, ceiling);
        }
    }

    [Fact]
    public void The_stack_multiplier_skips_a_banded_forage_slot()
    {
        BundleSpec scaled = StackScaling.Apply(Spec((RainbowShell, 4, 0), (Wood, 1, 0)), Profile(DifficultyStep.Extreme));
        Assert.Equal(4, scaled.Slots[0].Stack);
        Assert.Equal(2, scaled.Slots[1].Stack);
    }

    [Fact]
    public void The_filler_no_longer_rolls_the_old_forty_to_ninety_nine_big_ask()
    {
        var pools = new ItemPools { Forage = Enumerable.Range(0, 10).Select(i => new PoolItem($"(O){300 + i}", 40, 3, new[] { Season.Spring }, new[] { "Forest" })).ToList() };
        var tuning = new BundleGenerationTuning { LargeQuantityForageChance = 1.0 };
        var spec = new BundleSpec("Crafts Room", 0, "Spring Foraging", "Spring Foraging", "O 495 30", 0, 4,
            Enumerable.Range(0, 4).Select(i => new BundleSlotSpec($"(O)v{i}", 1, 0)).ToList());
        BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, tuning, new Random(9));
        Assert.All(filled.Slots, s => Assert.Equal(1, s.Stack));
    }
}
