using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class YearTwoCropsTests
{
    // Artichoke is deliberately absent from every exclusion below. Vanilla's Fall Mixed Seeds
    // roll (Crop.getRandomLowGradeCropForThisSeason, Next(487, 491)) already includes Artichoke
    // Seeds 489 at 25% per seed, in any year, so a year-1 player can grow it without buying
    // anything. Only the shop listing is YEAR 2 gated.
    [Fact]
    public void No_upgrades_excludes_garlic_and_red_cabbage_but_not_artichoke_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy);
        Assert.Equal(new[] { "(O)248", "(O)266" }, ex.OrderBy(x => x, System.StringComparer.Ordinal));
        Assert.DoesNotContain(YearTwoCrops.Artichoke, ex);
    }

    [Fact]
    public void Garlic_cultivation_frees_only_garlic_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.GarlicUpgrade, DifficultyStep.Easy);
        Assert.DoesNotContain("(O)248", ex);
        Assert.Contains("(O)266", ex);
    }

    [Fact]
    public void Red_cabbage_cultivation_frees_only_red_cabbage_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.RedCabbageUpgrade, DifficultyStep.Easy);
        Assert.Contains("(O)248", ex);
        Assert.DoesNotContain("(O)266", ex);
    }

    [Fact]
    public void Pierre_upgrade_frees_everything_on_easy()
        => Assert.Empty(YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.PierreUpgrade, DifficultyStep.Easy));

    [Fact]
    public void Year_two_crops_are_excluded_only_on_easy()
    {
        Assert.Contains("(O)266", YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
        Assert.Empty(YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Normal));
    }

    [Fact]
    public void Both_cultivation_upgrades_exist_cost_the_same_and_undercut_pierre()
    {
        UpgradeDefinition garlic = UpgradeCatalog.TryGet(YearTwoCrops.GarlicUpgrade)!;
        UpgradeDefinition redCabbage = UpgradeCatalog.TryGet(YearTwoCrops.RedCabbageUpgrade)!;
        UpgradeDefinition pierre = UpgradeCatalog.TryGet(YearTwoCrops.PierreUpgrade)!;

        Assert.Equal(garlic.Cost, redCabbage.Cost);
        // Both together must stay cheaper than the guaranteed shop route, or the random one is
        // pointless for anyone who wants more than a single crop.
        Assert.True(garlic.Cost + redCabbage.Cost < pierre.Cost);
        Assert.Equal(UpgradeCategory.Obtainability, garlic.Category);
    }

    [Fact]
    public void There_is_no_artichoke_cultivation_upgrade()
        => Assert.Null(UpgradeCatalog.TryGet("cult_artichoke"));

    [Fact]
    public void Extra_excluded_ids_keep_crops_out_of_the_pools_on_easy()
    {
        var pools = ItemPoolBuilder.Build(
            new[] { new RawCropEntry("248", new[] { Season.Spring }), new RawCropEntry("24", new[] { Season.Spring }) },
            new Dictionary<string, RawObjectEntry>
            {
                ["248"] = new("Basic", -75, 60, false, new List<string>()),
                ["24"] = new("Basic", -75, 35, false, new List<string>()),
            },
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning(),
            extraExcludedIds: YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
        Assert.Equal(new[] { "(O)24" }, pools.Crops.Select(p => p.ItemId));
        Assert.DoesNotContain("(O)248", pools.QualityEligibleIds!);
    }

    // Jeff, 2026-10-21: without the Pierre buy the three crops only come from Mixed Seeds, so a
    // count ask is capped by the Stack Size step (a Fall board asked 49 Artichokes, JazzDaxx on Nexus).
    public static IEnumerable<object[]> CropsBySteps()
    {
        var caps = new (DifficultyStep Step, int Cap)[]
        {
            (DifficultyStep.Easy, 2), (DifficultyStep.Normal, 3), (DifficultyStep.Hard, 4), (DifficultyStep.Extreme, 5),
        };
        foreach (string id in new[] { YearTwoCrops.Garlic, YearTwoCrops.RedCabbage, YearTwoCrops.Artichoke })
            foreach ((DifficultyStep step, int cap) in caps)
                yield return new object[] { id, step, cap };
    }

    private static readonly Func<string, bool> NoUpgrades = _ => false;
    private static readonly Func<string, bool> PierreOwned = u => u == YearTwoCrops.PierreUpgrade;
    private static readonly Func<string, bool> CultivationsOnly = u => u != YearTwoCrops.PierreUpgrade;

    private static DifficultyProfile Profile(DifficultyStep step)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = step }, new GameplayConfig());

    [Theory]
    [MemberData(nameof(CropsBySteps))]
    public void Year_two_crop_ask_is_capped_by_step_without_pierre_and_unchanged_with_it(string id, DifficultyStep step, int cap)
    {
        DifficultyProfile profile = Profile(step);
        Assert.Equal(cap, profile.YearTwoCropAskCap);
        Assert.Equal(cap, profile.EffectiveYearTwoCropAskCap());
        // The basis is the generated shop-seed row either way; only the final count is held.
        Assert.Equal(99, QuantityAskPass.BasisByDeadline(id, Season.Fall));
        for (int seed = 0; seed < 50; seed++)
        {
            int roll = AskBands.Roll(99, profile, new Random(seed));
            Assert.Equal(roll, Roll(id, profile, seed, 0, PierreOwned));
            Assert.Equal(roll, Roll(id, profile, seed, 0, null));   // no save in hand: nothing capped
            Assert.Equal(Math.Min(roll, cap), Roll(id, profile, seed, 0, NoUpgrades));
            // A cultivation upgrade adds a Mixed Seeds route, not a shop: still capped.
            Assert.Equal(Math.Min(roll, cap), Roll(id, profile, seed, 0, CultivationsOnly));
            // Gold keeps the crop gold factor, then the same count cap.
            int gold = AskBands.ForGold(roll, AskBands.CropGoldFactor);
            Assert.Equal(gold, Roll(id, profile, seed, 2, PierreOwned));
            Assert.Equal(Math.Min(gold, cap), Roll(id, profile, seed, 2, NoUpgrades));
        }
    }

    [Theory]
    [MemberData(nameof(CropsBySteps))]
    public void A_roll_under_the_cap_stands(string id, DifficultyStep step, int cap)
    {
        DifficultyProfile profile = Profile(step);
        Assert.Equal(1, YearTwoCrops.ClampAsk(id, 1, profile.EffectiveYearTwoCropAskCap(), NoUpgrades, null));
        Assert.Equal(cap, YearTwoCrops.ClampAsk(id, 40, profile.EffectiveYearTwoCropAskCap(), NoUpgrades, null));
        Assert.Equal(40, YearTwoCrops.ClampAsk(id, 40, profile.EffectiveYearTwoCropAskCap(), PierreOwned, null));
    }

    [Fact]
    public void An_older_profile_stamp_takes_the_cap_from_its_stack_size_step()
    {
        DifficultyProfile old = Profile(DifficultyStep.Hard);
        old.YearTwoCropAskCap = null;
        Assert.Equal(YearTwoCrops.AskCapHard, old.EffectiveYearTwoCropAskCap());
    }

    private const string ArtichokeDip = "(O)605";
    private const string Milk = "(O)184";
    private const string TwoArtichokeDish = "(O)9101";
    private const string EitherWayDish = "(O)9102";
    private const string Parsnip = "(O)24";

    private static EffortData DishData() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            ["274"] = DishObj(-75, "Artichoke"), ["184"] = DishObj(-6, "Milk"), ["24"] = DishObj(-75, "Parsnip"),
            ["605"] = DishObj(-7, "Artichoke Dip"), ["9101"] = DishObj(-7, "Double Dip"), ["9102"] = DishObj(-7, "Either Way"),
        },
        CookingRecipes = new List<RawCookingRecipe>
        {
            new("Artichoke Dip", new[] { YearTwoCrops.Artichoke, Milk }, ArtichokeDip, "default"),
            new("Double Dip", new[] { YearTwoCrops.Artichoke, Milk }, TwoArtichokeDish, "default", new[] { 2, 1 }),
            // A second recipe that needs none of the three crops leaves the dish uncapped.
            new("Either Way A", new[] { YearTwoCrops.Artichoke }, EitherWayDish, "default"),
            new("Either Way B", new[] { Parsnip }, EitherWayDish, "default"),
        },
    };

    private static RawObjectEntry DishObj(int category, string name)
        => new("Basic", category, 50, false, new List<string>(), name);

    [Fact]
    public void Dishes_that_need_a_year_two_crop_are_listed_only_without_pierre()
    {
        ItemAvailabilityModel without = ItemAvailabilityBuilder.Build(new ItemPools(), effortData: DishData(), hasUpgrade: NoUpgrades);
        Assert.Equal(1, without.YearTwoCropDishUnits[ArtichokeDip]);
        Assert.Equal(2, without.YearTwoCropDishUnits[TwoArtichokeDish]);
        Assert.False(without.YearTwoCropDishUnits.ContainsKey(EitherWayDish));
        Assert.Empty(ItemAvailabilityBuilder.Build(new ItemPools(), effortData: DishData(), hasUpgrade: PierreOwned).YearTwoCropDishUnits);
        Assert.Empty(ItemAvailabilityBuilder.Build(new ItemPools(), effortData: DishData()).YearTwoCropDishUnits);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 2)]
    [InlineData(DifficultyStep.Normal, 3)]
    [InlineData(DifficultyStep.Hard, 4)]
    [InlineData(DifficultyStep.Extreme, 5)]
    public void A_dish_ask_follows_the_crop_cap_over_the_crops_it_takes(DifficultyStep step, int cap)
    {
        DifficultyProfile profile = Profile(step);
        // The dish rows are fixed at 12 a week so every step rolls above its cap; the units come
        // from the builder, as they do on a save.
        var bases = new Dictionary<string, double[]>
        {
            [ArtichokeDip] = new double[] { 12, 12, 12, 12 },
            [TwoArtichokeDish] = new double[] { 12, 12, 12, 12 },
            [EitherWayDish] = new double[] { 12, 12, 12, 12 },
        };
        var without = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>())
        {
            DishBases = bases,
            YearTwoCropDishUnits = ItemAvailabilityBuilder.Build(new ItemPools(), effortData: DishData(), hasUpgrade: NoUpgrades).YearTwoCropDishUnits,
        };
        var with = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>())
        {
            DishBases = bases,
            YearTwoCropDishUnits = ItemAvailabilityBuilder.Build(new ItemPools(), effortData: DishData(), hasUpgrade: PierreOwned).YearTwoCropDishUnits,
        };
        Assert.True(QuantityAskPass.Covers(ArtichokeDip, without));
        bool sawCapBind = false;
        for (int seed = 0; seed < 50; seed++)
        {
            int owned = DishRoll(ArtichokeDip, profile, seed, with);
            int capped = DishRoll(ArtichokeDip, profile, seed, without);
            Assert.Equal(Math.Min(owned, cap), capped);
            sawCapBind |= owned > cap;
            Assert.Equal(Math.Min(DishRoll(TwoArtichokeDish, profile, seed, with), Math.Max(1, cap / 2)),
                DishRoll(TwoArtichokeDish, profile, seed, without));
            Assert.Equal(DishRoll(EitherWayDish, profile, seed, with), DishRoll(EitherWayDish, profile, seed, without));
        }
        Assert.True(sawCapBind);
    }

    private static int DishRoll(string id, DifficultyProfile profile, int seed, ItemAvailabilityModel model)
        => QuantityAskPass.Apply(
            new BundleSpec("Pantry", 5, "Test", "Test", "O 495 30", 0, 1, new List<BundleSlotSpec> { new(id, 1, 0) }),
            profile, _ => null, new Random(seed), model).Slots[0].Stack;

    private static int Roll(string id, DifficultyProfile profile, int seed, int quality, Func<string, bool>? hasUpgrade)
        => QuantityAskPass.Apply(
            new BundleSpec("Pantry", 5, "Test", "Test", "O 495 30", 0, 1, new List<BundleSlotSpec> { new(id, 1, quality) }),
            profile, _ => null, new Random(seed), hasUpgrade: hasUpgrade).Slots[0].Stack;
}
