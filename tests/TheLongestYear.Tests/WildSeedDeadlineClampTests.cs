using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's ruling, 2026-09-04 (Nexus post, nyxnyx2234: "a bundle asked me to gather 90
/// common mushrooms on my first spring"): the Wild Seed exemption from the measured ceilings is
/// only honest when the item's Wild Seeds can actually grow before the bundle is due. Common
/// Mushroom is a FALL Wild Seed crop; a Spring deadline gets the Spring ceiling like any other
/// forage.</summary>
public class WildSeedDeadlineClampTests
{
    private const string CommonMushroom = "(O)404";   // Fall Wild Seeds
    private const string WildHorseradish = "(O)16";   // Spring Wild Seeds
    private const string RainbowShell = "(O)394";     // never growable

    [Theory]
    [InlineData(CommonMushroom, Season.Spring, false)]
    [InlineData(CommonMushroom, Season.Summer, false)]
    [InlineData(CommonMushroom, Season.Fall, true)]
    [InlineData(CommonMushroom, Season.Winter, true)]
    [InlineData(WildHorseradish, Season.Spring, true)]
    [InlineData(RainbowShell, Season.Winter, false)]
    public void Wild_seeds_only_count_once_their_season_has_arrived(string id, Season deadline, bool growable)
        => Assert.Equal(growable, ForageAskLimits.IsWildSeedGrowableBy(id, deadline));

    [Fact]
    public void A_spring_deadline_clamps_common_mushroom_to_the_spring_ceiling()
    {
        int springCeiling = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;
        Assert.True(springCeiling < 90);
        Assert.Equal(springCeiling, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Spring));
    }

    [Fact]
    public void A_later_deadline_takes_the_most_generous_season_reached_so_far()
    {
        int spring = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;
        int summer = ForageAskLimits.MeasuredMaxAsk(Season.Summer, CommonMushroom)!.Value;
        Assert.Equal(Math.Max(spring, summer), ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Summer));
    }

    [Fact]
    public void Once_the_seeds_can_grow_the_ask_is_uncapped_again()
    {
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Fall));
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(WildHorseradish, 90, Season.Spring));
    }

    [Fact]
    public void No_deadline_keeps_the_old_any_season_behaviour()
    {
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, null));
        Assert.Equal(ForageAskLimits.ClampAnySeason(RainbowShell, 90), ForageAskLimits.ClampForDeadline(RainbowShell, 90, null));
    }

    private static PoolItem Item(string id, params Season[] seasons)
        => new(id, 40, 3, seasons, new[] { "Forest" });

    private static BundleSpec Spec(string name, int slots)
        => new("Crafts Room", 0, name, name, "O 495 30", 0, slots,
            Enumerable.Range(0, slots).Select(i => new BundleSlotSpec($"(O)v{i}", 1, 0)).ToList());

    [Fact]
    public void A_spring_foraging_bundle_never_asks_for_90_common_mushrooms()
    {
        // Common Mushroom does spawn in Spring (mines, Secret Woods), so it sits in the Spring
        // pool; the big-ask roll used to land on it uncapped because Fall Wild Seeds grow it.
        var pools = new ItemPools { Forage = new[] { Item(CommonMushroom, Season.Spring, Season.Fall) } };
        var tuning = new BundleGenerationTuning { LargeQuantityForageChance = 1.0 };
        int ceiling = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;

        for (int seed = 0; seed < 20; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(Spec("Spring Foraging", 1),
                new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, tuning, new Random(seed));
            Assert.True(filled.Slots[0].Stack <= ceiling, $"seed {seed}: asked {filled.Slots[0].Stack}");
        }
    }

    [Fact]
    public void A_season_less_forage_bundle_reads_the_deadline_the_classifier_will_give_the_item()
    {
        // Five ingredients spread across the four checkpoints; the mushroom is the trivial one
        // (effort 1), so BundleDeadlines puts it on Spring. Everything else is due later.
        string[] others = { "(O)o1", "(O)o2", "(O)o3", "(O)o4" };
        var pools = new ItemPools
        {
            Forage = new[] { Item(CommonMushroom, Season.Spring, Season.Fall) }
                .Concat(others.Select(o => Item(o, Season.Spring))).ToList(),
        };
        var derived = new Dictionary<string, ItemAvailability>
        {
            [CommonMushroom] = new(Season.Spring, 1, "test", EarliestWeek: 1, HardWeek: 1),
        };
        foreach (string o in others)
            derived[o] = new(Season.Spring, 5, "test", EarliestWeek: 1, HardWeek: 1);
        var model = new ItemAvailabilityModel(derived, mode: WeekModes.For(DifficultyStep.Normal), step: DifficultyStep.Normal);
        var tuning = new BundleGenerationTuning { LargeQuantityForageChance = 1.0 };
        int ceiling = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;

        var ids = new List<string> { CommonMushroom }.Concat(others).ToList();
        Assert.Equal(Season.Spring, BundleDeadlines.For(ids, model)[CommonMushroom]);

        for (int seed = 0; seed < 30; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(Spec("Wild Medicine", 5),
                new DomainMatch(PoolDomain.SeasonalForage, null), pools, tuning, new Random(seed), availability: model);
            BundleSlotSpec mushroom = filled.Slots.Single(s => s.ItemId == CommonMushroom);
            Assert.True(mushroom.Stack <= ceiling, $"seed {seed}: asked {mushroom.Stack}");
        }
    }
}
