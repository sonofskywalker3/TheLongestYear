using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class EffortTiersTests
{
    [Theory]
    [InlineData(0, EffortTier.Easy)] [InlineData(2, EffortTier.Easy)] [InlineData(3, EffortTier.Medium)]
    [InlineData(5, EffortTier.Medium)] [InlineData(6, EffortTier.Hard)] [InlineData(8, EffortTier.Hard)]
    [InlineData(9, EffortTier.Extreme)] [InlineData(12, EffortTier.Extreme)]
    public void Tiers_are_absolute(int effort, EffortTier tier) => Assert.Equal(tier, EffortTiers.Tier(effort));

    [Fact]
    public void The_harder_of_two_easy_items_is_still_askable_in_spring()
    {
        var rules = new GoalSamplingRules(Season.Spring, 0, id => id == "(O)80" ? 1 : 3);
        var weights = GoalWeighting.For(new[] { "(O)80", "(O)334" }, rules, _ => Rarity.Common);
        Assert.All(weights, w => Assert.True(w.Weight > 0));
    }

    [Theory] [InlineData(Rarity.Common, EffortTier.Easy)] [InlineData(Rarity.Uncommon, EffortTier.Medium)] [InlineData(Rarity.Rare, EffortTier.Hard)] [InlineData(Rarity.VeryRare, EffortTier.Extreme)]
    public void Price_buckets_map_to_tiers(Rarity r, EffortTier t) => Assert.Equal(t, EffortTiers.FromRarity(r));

    [Theory]
    [InlineData(Season.Spring, EffortTier.Easy, 8)] [InlineData(Season.Spring, EffortTier.Extreme, 0)]
    [InlineData(Season.Summer, EffortTier.Hard, 2)] [InlineData(Season.Fall, EffortTier.Medium, 4)]
    [InlineData(Season.Winter, EffortTier.Extreme, 8)] [InlineData(Season.Winter, EffortTier.Easy, 1)]
    public void Weights_follow_the_spec_table(Season s, EffortTier t, int w) => Assert.Equal(w, EffortWeights.For(s, t));
}

public class EffortComposerTests
{
    private static RawObjectEntry Obj(int category, string name, string type = "Basic") => new(type, category, 10, false, new string[0], name);

    [Fact]
    public void Composer_tries_domains_in_order_and_recurses_through_inputs()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["398"] = Obj(-79, "Grape"), ["348"] = Obj(-26, "Wine") },
            Crops = new List<RawCropGrowth> { new("(O)398", 10, true, true) },
            MachineRules = new List<RawMachineRule> { new("(BC)12", null, new[] { "category_fruit" }, new[] { "(O)348" }, 10000, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "Farming 8" },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.Equal(3, composer.Derive("(O)398")!.Effort);
        Assert.Equal(3 + 3 + 2, composer.Derive("(O)348")!.Effort);
    }

    [Fact]
    public void Season_derived_effort_wins_and_unclaimed_ids_are_null()
    {
        var seasonDerived = new Dictionary<string, ItemAvailability> { ["(O)128"] = new(Season.Summer, 4, "fish") };
        var composer = new EffortComposer(new EffortData(), seasonDerived, false);
        Assert.Equal(4, composer.EffortOf("(O)128"));
        Assert.Null(composer.Derive("(O)999"));
    }

    [Fact]
    public void Builder_reports_effort_only_ids_and_keeps_the_price_bucket_for_unclaimed_ones()
    {
        var pools = new ItemPools();
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["767"] = Obj(-28, "Bat Wing"), ["999"] = Obj(0, "Modded Thing") },
            MonsterDrops = new List<RawMonsterDrop> { new("Bat", "(O)767", 0.9) },
        };
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);
        // Bat Wing plus the guild reward hats and weapons the composer always places.
        Assert.Equal(1 + AvailabilityWeeks.GuildRewardWeeks.Count, model.DerivedEffortCount);
        Assert.Equal(EffortSource.Derived, model.For("(O)767").Source);
        Assert.Equal(EffortSource.Price, model.For("(O)999").Source);
        Assert.Contains("(O)999", model.UnrecognisedIds);
    }
}
