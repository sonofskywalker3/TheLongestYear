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

    /// <summary>Every artifact waits for week 3, whatever route finds it, so geodes, dig spots and
    /// monster drops have time to turn up (Jeff, 2026-09-29; a streamer's week-1 board asked for
    /// Elvish Jewelry).</summary>
    [Fact]
    public void Artifacts_are_floored_at_week_3_on_every_route()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry>
            {
                ["104"] = Obj(0, "Elvish Jewelry", "Arch"),
                ["96"] = Obj(0, "Dwarf Scroll I", "Arch"),
                ["767"] = Obj(-28, "Bat Wing"),
            },
            ArtifactSpots = new List<RawArtifactSpot> { new("Forest", "(O)104", 0.05) },
            MonsterDrops = new List<RawMonsterDrop> { new("Bat", "(O)767", 0.9), new("Green Slime", "(O)96", 0.5) },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);

        Assert.Equal(3, composer.Derive("(O)104")!.EarliestWeek);
        Assert.Equal(3, composer.Derive("(O)96")!.EarliestWeek);
        Assert.True(composer.Derive("(O)767")!.EarliestWeek < 3);
    }

    private const string Grape = "(O)398", Wine = "(O)348", Brandy = "(O)9001", Berry = "(O)9002", DriedBerry = "(O)9003", BerryCharm = "(O)9004", Loop = "(O)9005", Back = "(O)9006", Liqueur = "(O)9007";
    /// <summary>Later than the rules' own week for Grape and for every machine here, so the model accepts it as a ruling.</summary>
    private static int RuledWeek => MadeGoods().For(Liqueur).Week + 2;

    /// <summary>Grape (a bat drop here, so the rules place it) is kegged into Wine, Wine is distilled into Brandy, and Berry (which no
    /// rule places) is dried into Dried Berry and crafted into a Berry Charm. Loop and Back are each made
    /// from the other. Every machine is a default recipe that runs under a day.</summary>
    private static ItemAvailabilityModel MadeGoods(Dictionary<string, int>? weekOverrides = null, Dictionary<string, Season>? seasonPins = null)
    {
        static RawMachineRule Makes(string machine, string input, string output) => new(machine, input, new string[0], new[] { output }, 10, -1);
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry>
            {
                ["398"] = Obj(-79, "Grape"), ["348"] = Obj(-26, "Wine"), ["9001"] = Obj(-26, "Brandy"),
                ["9002"] = Obj(-79, "Berry"), ["9003"] = Obj(-26, "Dried Berry"), ["9004"] = Obj(0, "Berry Charm"),
                ["9005"] = Obj(-26, "Loop"), ["9006"] = Obj(-26, "Back"), ["9007"] = Obj(-26, "Liqueur"),
            },
            MonsterDrops = new List<RawMonsterDrop> { new("Bat", Grape, 0.9) },
            MachineRules = new List<RawMachineRule> { Makes("(BC)12", Grape, Wine), Makes("(BC)13", Wine, Brandy), Makes("(BC)13", Brandy, Liqueur), Makes("(BC)14", Berry, DriedBerry), Makes("(BC)12", Back, Loop), Makes("(BC)12", Loop, Back) },
            CraftingRecipes = new[] { new CraftingRecipe("Berry Charm", BerryCharm, new[] { new RecipeIngredient(Berry, 1) }, "default") },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "default", ["(BC)13"] = "default", ["(BC)14"] = "default" },
        };
        return ItemAvailabilityBuilder.Build(new ItemPools(), seasonOverrides: seasonPins, effortData: data, weekOverrides: weekOverrides);
    }

    [Fact]
    public void A_week_ruling_reaches_everything_made_from_the_ruled_item()
    {
        Assert.True(MadeGoods().IsPlaced(Grape));
        ItemAvailabilityModel ruled = MadeGoods(new Dictionary<string, int> { [Grape] = RuledWeek });
        Assert.Equal(RuledWeek, ruled.For(Grape).Week);
        Assert.Equal(RuledWeek, ruled.For(Wine).Week);
        Assert.Equal(RuledWeek, ruled.For(Brandy).Week);
    }

    [Fact]
    public void A_week_ruling_that_places_an_item_places_what_is_made_from_it()
    {
        ItemAvailabilityModel plain = MadeGoods();
        Assert.False(plain.IsPlaced(DriedBerry));
        Assert.False(plain.IsPlaced(BerryCharm));
        ItemAvailabilityModel ruled = MadeGoods(new Dictionary<string, int> { [Berry] = RuledWeek });
        Assert.Equal(RuledWeek, ruled.For(DriedBerry).Week);
        Assert.True(ruled.IsPlaced(BerryCharm));
        Assert.True(ruled.For(BerryCharm).Week >= RuledWeek);
    }

    /// <summary>The model refuses an id's week ruling when its season pin is earlier than the rules' week.</summary>
    [Fact]
    public void A_week_ruling_the_season_pin_voids_moves_nothing_made_from_the_item()
    {
        int brandyWeek = MadeGoods().For(Brandy).Week;
        Assert.True(brandyWeek > AvailabilityWeeks.FirstWeekOf(Season.Spring));
        ItemAvailabilityModel ruled = MadeGoods(
            new Dictionary<string, int> { [Brandy] = RuledWeek }, new Dictionary<string, Season> { [Brandy] = Season.Spring });
        Assert.Equal(brandyWeek, ruled.For(Brandy).Week);
        Assert.Equal(MadeGoods().For(Liqueur).Week, ruled.For(Liqueur).Week);
    }

    /// <summary>Loop's own week is not settled while Back is being dated from it, so its ruling cannot date Back.</summary>
    [Fact]
    public void A_ruled_item_made_from_its_own_product_does_not_date_that_product()
        => Assert.False(MadeGoods(new Dictionary<string, int> { [Loop] = RuledWeek }).IsPlaced(Back));

    [Fact]
    public void A_refused_week_ruling_moves_nothing_made_from_the_item()
    {
        int Earlier = RuledWeek - 1;
        ItemAvailabilityModel ruled = MadeGoods(new Dictionary<string, int> { [Grape] = RuledWeek, [Wine] = Earlier });
        Assert.Contains(Wine, ruled.RejectedSeasonOverrides);
        Assert.Equal(RuledWeek, ruled.For(Wine).Week);
        Assert.Equal(RuledWeek, ruled.For(Brandy).Week);
    }
}
