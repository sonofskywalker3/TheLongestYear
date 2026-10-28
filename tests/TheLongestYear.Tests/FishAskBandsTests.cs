using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's ruling, 2026-09-04: a fish ask is basis x band. The basis is what a level-10
/// player lands on the best ten-hour day for that fish (modelled from the game's own tables, see
/// docs/design/fish-catch-rates-2026-09-04.md) times seven days; 80% of it is the hard
/// ceiling; the four steps roll inside 10-30 / 20-50 / 50-65 / 65-80% of it; a gold ask takes 75%
/// of the roll (gold is automatic from level 6 on a full cast, so the cut is a hedge for the early
/// loop, not a statistic). Legendaries and anything landed less than twice a week stay at 1.</summary>
public class FishAskBandsTests
{
    private const string SmallmouthBass = "(O)137";
    private const string Octopus = "(O)149";
    private const string Legend = "(O)163";
    private const string Walleye = "(O)140";   // rain-only, Fall and Winter
    private const string Perch = "(O)141";     // Winter only
    /// <summary>The bases Jeff approved (2026-09-04): Smallmouth Bass in Spring, Octopus in Summer.</summary>
    private const double ApprovedSpringBass = 66;
    private const double ApprovedSummerOctopus = 8;
    /// <summary>The exact catch model may sit this far from the sampled table it replaced.</summary>
    private const double ModelTolerance = 0.1;

    /// <summary>The stored catch model, as the cold build makes it from the vanilla game data.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, CatchRate>> Rates = new(() => FishCatchRates.Build(VanillaInputsFixture.Load()));

    /// <summary>A model carrying the stored catch model, as the board's availability model does.</summary>
    private static ItemAvailabilityModel Model() => new(new Dictionary<string, ItemAvailability>()) { CatchRates = Rates.Value };

    private static DifficultyProfile Profile(DifficultyStep stack)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = stack }, new GameplayConfig());

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.10, 0.30)]
    [InlineData(DifficultyStep.Normal, 0.20, 0.50)]
    [InlineData(DifficultyStep.Hard, 0.50, 0.65)]
    [InlineData(DifficultyStep.Extreme, 0.65, 0.80)]
    public void The_profile_stamps_the_band_for_the_stack_step(DifficultyStep step, double low, double high)
    {
        DifficultyProfile p = Profile(step);
        Assert.Equal(low, p.AskBandLow, 3);
        Assert.Equal(high, p.AskBandHigh, 3);
    }

    [Fact]
    public void Normal_is_the_shipping_balance_so_the_default_profile_carries_normal_band()
    {
        var p = new DifficultyProfile();
        Assert.Equal(AskBands.NormalLow, p.AskBandLow, 3);
        Assert.Equal(AskBands.NormalHigh, p.AskBandHigh, 3);
    }

    [Fact]
    public void The_catch_model_reproduces_the_numbers_jeff_approved()
    {
        Assert.InRange(FishAskBasis.Basis(Rates.Value, Season.Spring, SmallmouthBass)!.Value,
            ApprovedSpringBass * (1 - ModelTolerance), ApprovedSpringBass * (1 + ModelTolerance));
        Assert.InRange(FishAskBasis.Basis(Rates.Value, Season.Summer, Octopus)!.Value,
            ApprovedSummerOctopus * (1 - ModelTolerance), ApprovedSummerOctopus * (1 + ModelTolerance));
        Assert.Null(FishAskBasis.Basis(Rates.Value, Season.Winter, SmallmouthBass));   // not catchable then
        Assert.False(FishAskBasis.Covers(Rates.Value, Legend));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 7, 20)]
    [InlineData(DifficultyStep.Normal, 14, 33)]
    [InlineData(DifficultyStep.Hard, 33, 43)]
    [InlineData(DifficultyStep.Extreme, 43, 53)]
    public void A_spring_bass_ask_rolls_inside_its_band(DifficultyStep step, int low, int high)
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 300; seed++)
        {
            int stack = AskBands.Roll(ApprovedSpringBass, Profile(step), new Random(seed));
            Assert.InRange(stack, low, high);
            seen.Add(stack);
        }
        Assert.True(seen.Count > 3, "the band should spread, not sit on one number");
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 1, 3)]
    [InlineData(DifficultyStep.Normal, 2, 4)]
    [InlineData(DifficultyStep.Hard, 4, 6)]
    [InlineData(DifficultyStep.Extreme, 6, 7)]
    public void A_summer_octopus_ask_rolls_inside_its_band(DifficultyStep step, int low, int high)
    {
        for (int seed = 0; seed < 300; seed++)
            Assert.InRange(AskBands.Roll(ApprovedSummerOctopus, Profile(step), new Random(seed)), low, high);
    }

    [Fact]
    public void A_gold_ask_takes_three_quarters_of_the_roll_never_below_one()
    {
        Assert.Equal(40, AskBands.ForGold(53));
        Assert.Equal(5, AskBands.ForGold(7));
        Assert.Equal(1, AskBands.ForGold(1));
    }

    [Fact]
    public void The_basis_for_a_deadline_is_the_best_season_reached_by_then()
    {
        // Walleye is Fall/Winter rain-only: a Summer deadline cannot hold it at all.
        Assert.Null(FishAskBasis.BasisByDeadline(Rates.Value, Walleye, Season.Summer));
        Assert.NotNull(FishAskBasis.BasisByDeadline(Rates.Value, Walleye, Season.Fall));
        // Perch is Winter only: with no deadline (a pick-X-of-Y ramp) the whole year is open.
        Assert.NotNull(FishAskBasis.BasisByDeadline(Rates.Value, Perch, null));
        Assert.Null(FishAskBasis.BasisByDeadline(Rates.Value, Perch, Season.Fall));
        // Bass: Spring and Fall rows; a Summer deadline reads Spring's.
        Assert.Equal(FishAskBasis.Basis(Rates.Value, Season.Spring, SmallmouthBass), FishAskBasis.BasisByDeadline(Rates.Value, SmallmouthBass, Season.Summer));
    }

    private static BundleSpec Spec(params (string Id, int Stack, int Quality)[] slots)
        => new("Fish Tank", 3, "Lake Fish", "Lake Fish", "O 685 30", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, s.Quality)).ToList());

    [Fact]
    public void The_pass_bands_every_covered_fish_and_leaves_the_rest_alone()
    {
        BundleSpec spec = Spec((SmallmouthBass, 1, 0), (Octopus, 1, 2), (Legend, 1, 0), ("(O)f0", 1, 0));

        BundleSpec banded = QuantityAskPass.Apply(spec, Profile(DifficultyStep.Extreme), _ => null, new Random(4), model: Model());

        Assert.InRange(banded.Slots[0].Stack, 43, 53);
        Assert.InRange(banded.Slots[1].Stack, 4, 6);      // gold: 75% of 6..7
        Assert.Equal(1, banded.Slots[2].Stack);           // legendary
        Assert.Equal(1, banded.Slots[3].Stack);           // not a fish
        Assert.Equal(2, banded.Slots[1].Quality);         // quality is not the pass's business
    }

    [Fact]
    public void The_pass_reads_the_slot_deadline_it_is_given()
    {
        BundleSpec spec = Spec((Walleye, 1, 0));
        BundleSpec summer = QuantityAskPass.Apply(spec, Profile(DifficultyStep.Hard), _ => Season.Summer, new Random(1), model: Model());
        BundleSpec fall = QuantityAskPass.Apply(spec, Profile(DifficultyStep.Hard), _ => Season.Fall, new Random(1), model: Model());
        Assert.Equal(1, summer.Slots[0].Stack);
        Assert.True(fall.Slots[0].Stack > 1);
    }

    [Fact]
    public void The_pass_is_deterministic_and_returns_the_same_reference_when_nothing_is_covered()
    {
        BundleSpec spec = Spec((SmallmouthBass, 1, 0), ("(O)f0", 1, 0));
        Assert.Equal(
            QuantityAskPass.Apply(spec, Profile(DifficultyStep.Normal), _ => null, new Random(9), model: Model()).Slots,
            QuantityAskPass.Apply(spec, Profile(DifficultyStep.Normal), _ => null, new Random(9), model: Model()).Slots);
        BundleSpec plain = Spec(("(O)f0", 1, 0), ("(O)f1", 1, 0));
        Assert.Same(plain, QuantityAskPass.Apply(plain, Profile(DifficultyStep.Hard), _ => null, new Random(9), model: Model()));
    }

    [Fact]
    public void The_stack_multiplier_skips_a_banded_fish_so_difficulty_is_not_applied_twice()
    {
        BundleSpec spec = Spec((SmallmouthBass, 1, 0), ("(O)f0", 1, 0));
        BundleSpec banded = QuantityAskPass.Apply(spec, Profile(DifficultyStep.Extreme), _ => null, new Random(2), out IReadOnlySet<int> bandedSlots, Model());
        Assert.Equal(new[] { 0 }, bandedSlots.OrderBy(i => i));
        BundleSpec scaled = StackScaling.Apply(banded, Profile(DifficultyStep.Extreme), bandedSlots);
        Assert.Equal(banded.Slots[0].Stack, scaled.Slots[0].Stack);
        Assert.Equal(2, scaled.Slots[1].Stack);
    }

    [Fact]
    public void A_covered_item_with_no_basis_by_its_deadline_is_not_banded_and_still_meets_the_multiplier()
    {
        // Walleye has a Fall basis only; a Summer deadline finds none, so the slot is left alone by
        // the pass and the multiplier treats it like any other x1 (design review, 2026-09-04).
        BundleSpec spec = Spec((Walleye, 1, 0));
        BundleSpec banded = QuantityAskPass.Apply(spec, Profile(DifficultyStep.Extreme), _ => Season.Summer, new Random(2), out IReadOnlySet<int> bandedSlots, Model());
        Assert.Empty(bandedSlots);
        Assert.Equal(2, StackScaling.Apply(banded, Profile(DifficultyStep.Extreme), bandedSlots).Slots[0].Stack);
    }
}
