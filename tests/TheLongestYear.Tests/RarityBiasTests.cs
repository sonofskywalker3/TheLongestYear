using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class RarityBiasTests
{
    private const int BaseWeight = 3;

    private static PoolItem Item(string id, int price, params Season[] seasons)
        => new(id, price, BaseWeight, seasons.ToList(), new List<string>());

    private static ItemPools CropsOf(params PoolItem[] items)
        => new() { Crops = items.ToList() };

    [Fact]
    public void A_Bias_Of_One_Returns_The_Same_Instance()
    {
        var pools = CropsOf(Item("(O)24", 35));

        Assert.Same(pools, RarityBias.Apply(pools, 1.0, new RarityThresholds()));
    }

    [Fact]
    public void Hard_Bias_Raises_A_Rare_Items_Weight_Above_A_Common_Items()
    {
        var biased = RarityBias.Apply(
            CropsOf(Item("(O)cheap", 10), Item("(O)dear", 5000)), 1.6, new RarityThresholds());

        int cheap = biased.Crops.Single(p => p.ItemId == "(O)cheap").Weight;
        int dear = biased.Crops.Single(p => p.ItemId == "(O)dear").Weight;

        Assert.True(dear > cheap, $"expected dear ({dear}) > cheap ({cheap})");
    }

    [Fact]
    public void Easy_Bias_Lowers_A_Rare_Items_Weight_But_Never_Below_One()
    {
        var biased = RarityBias.Apply(
            CropsOf(Item("(O)cheap", 10), Item("(O)dear", 5000)), 0.5, new RarityThresholds());

        Assert.All(biased.Crops, p => Assert.True(p.Weight >= 1, $"{p.ItemId} weight {p.Weight}"));
        Assert.True(biased.Crops.Single(p => p.ItemId == "(O)dear").Weight
                  < biased.Crops.Single(p => p.ItemId == "(O)cheap").Weight);
    }

    /// <summary>Score 1 is the baseline exponent, so the cheapest, always-available items keep
    /// exactly the weight the pool builder gave them at every bias.</summary>
    [Fact]
    public void The_Easiest_Items_Are_Never_Moved()
    {
        var biased = RarityBias.Apply(CropsOf(Item("(O)cheap", 10)), 2.4, new RarityThresholds());

        Assert.Equal(BaseWeight, biased.Crops.Single().Weight);
    }

    /// <summary>A Fall or Winter spawn is +1 hardness on top of price, so two items at the same
    /// price separate once the bias is on.</summary>
    [Fact]
    public void A_Late_Season_Item_Is_Weighted_Above_An_Identically_Priced_Spring_One()
    {
        var biased = RarityBias.Apply(
            CropsOf(Item("(O)spring", 5000, Season.Spring), Item("(O)fall", 5000, Season.Fall)),
            2.4, new RarityThresholds());

        Assert.True(biased.Crops.Single(p => p.ItemId == "(O)fall").Weight
                  > biased.Crops.Single(p => p.ItemId == "(O)spring").Weight);
    }

    /// <summary>Artisan goods need a keg or a press, which ItemHardness scores as +2. The domain
    /// mapping has to be right or that bonus never fires.</summary>
    [Fact]
    public void Artisan_Goods_Carry_The_Station_Bonus()
    {
        var pools = new ItemPools
        {
            Crops = new List<PoolItem> { Item("(O)same", 200) },
            ArtisanGoods = new List<PoolItem> { Item("(O)same", 200) },
        };
        var biased = RarityBias.Apply(pools, 2.0, new RarityThresholds());

        Assert.True(biased.ArtisanGoods.Single().Weight > biased.Crops.Single().Weight);
    }

    [Fact]
    public void Eligibility_And_Obtainability_Data_Survive_The_Rebuild()
    {
        var pools = new ItemPools
        {
            Crops = new List<PoolItem> { Item("(O)24", 35) },
            QualityEligibleIds = new HashSet<string> { "(O)24" },
            DerivedSeasonPins = new Dictionary<string, Season> { ["(O)24"] = Season.Fall },
        };
        var biased = RarityBias.Apply(pools, 2.4, new RarityThresholds());

        Assert.Contains("(O)24", biased.QualityEligibleIds!);
        Assert.Equal(Season.Fall, biased.DerivedSeasonPins["(O)24"]);
    }

    [Fact]
    public void Item_Identity_Is_Never_Changed()
    {
        var biased = RarityBias.Apply(
            CropsOf(Item("(O)24", 35, Season.Spring)), 2.4, new RarityThresholds());

        PoolItem item = biased.Crops.Single();
        Assert.Equal("(O)24", item.ItemId);
        Assert.Equal(35, item.Price);
        Assert.Equal(new[] { Season.Spring }, item.Seasons);
    }

    [Fact]
    public void Empty_Pools_Are_Handled()
    {
        var biased = RarityBias.Apply(new ItemPools(), 2.4, new RarityThresholds());

        Assert.Empty(biased.Crops);
        Assert.Empty(biased.Fish);
    }

    /// <summary>Nothing the input carried may go missing, whatever gets added to ItemPools later.
    /// Apply is written as `pools with { ... }` precisely so an unlisted property carries by
    /// construction; this walks EVERY public property by reflection so a new one added without a
    /// line in Apply can never silently come back empty when the rarity modifier is on.</summary>
    [Fact]
    public void Every_Populated_Property_Survives_Apply()
    {
        var item = Item("(O)24", 35, Season.Spring);
        var one = new List<PoolItem> { item };
        var pools = new ItemPools
        {
            Crops = one, Fish = one, CrabPot = one, Forage = one, MonsterDrops = one,
            Metals = one, ArtisanGoods = one, Artifacts = one, Books = one, Saplings = one,
            GeodeMinerals = one, Cooking = one, TapperGoods = one, WinterOnly = one,
            DerivedSeasonPins = new Dictionary<string, Season> { ["(O)24"] = Season.Fall },
            QualityEligibleIds = new HashSet<string> { "(O)24" },
            TrapFishIds = new HashSet<string> { "(O)715" },
            FruitTreeFruitIds = new HashSet<string> { "(O)634" },
            ExcludedIds = new HashSet<string> { "(O)266" },
            FishRows = new Dictionary<string, RawFishEntry>
            {
                ["128"] = RawFishEntry.Parse("128", "Pufferfish/80/mixed/1/36/1200 1600/summer/sunny/690/1/3"),
            },
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>> { [ItemKind.Gem] = one },
            ColourTags = new Dictionary<string, IReadOnlyList<PoolItem>> { ["color_red"] = one },
        };

        ItemPools biased = RarityBias.Apply(pools, 2.4, new RarityThresholds());

        foreach (System.Reflection.PropertyInfo property in typeof(ItemPools).GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            object? before = property.GetValue(pools);
            object? after = property.GetValue(biased);
            Assert.True(before != null, $"{property.Name} was not populated by this test");
            Assert.True(after != null, $"{property.Name} came back null");
            Assert.True(Count(before) > 0, $"{property.Name} was not populated by this test");
            Assert.Equal(Count(before), Count(after));
        }
    }

    private static int Count(object? value)
    {
        int n = 0;
        foreach (object? _ in (System.Collections.IEnumerable)value!)
            n++;
        return n;
    }
}
