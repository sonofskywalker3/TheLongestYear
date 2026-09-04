using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus 1122358 / 1122423 (2026-08-24): engine bundles rolled Ginger Island /
/// Qi-gated items (Qi Fruit, Pineapple, Taro Root, ...) and quality asks on algae, which
/// can never carry quality. Default tuning must vet all of it out.</summary>
public class GatedItemVettingTests
{
    internal static Dictionary<string, RawObjectEntry> Objects(params (string id, RawObjectEntry e)[] entries)
        => entries.ToDictionary(x => x.id, x => x.e);

    internal static RawObjectEntry Obj(int category = -75, int price = 50, string type = "Basic",
        bool excludeFromRandomSale = false, params string[] tags)
        => new(type, category, price, excludeFromRandomSale, tags);

    [Fact]
    public void DefaultTuning_ExcludesIslandAndQiCrops_FromCropPool()
    {
        var all = new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter };
        var pools = ItemPoolBuilder.Build(
            new[]
            {
                new RawCropEntry("889", all),                      // Qi Fruit
                new RawCropEntry("832", new[] { Season.Summer }),  // Pineapple
                new RawCropEntry("830", new[] { Season.Summer }),  // Taro Root
                new RawCropEntry("24", new[] { Season.Spring }),   // Parsnip (control)
            },
            Objects(("889", Obj()), ("832", Obj()), ("830", Obj()), ("24", Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
        Assert.Equal(new[] { "(O)24" }, pools.Crops.Select(p => p.ItemId));
    }

    [Fact]
    public void DefaultTuning_ExcludesRadioactive_FromMetalsPool_AndIslandDishes_FromCookingPool()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(
                ("909", Obj(category: -15)), ("910", Obj(category: -15)), // Radioactive Ore/Bar
                ("848", Obj(category: -15)),                              // Cinder Shard
                ("335", Obj(category: -15)),                              // Iron Bar (control)
                ("903", Obj(category: -7)), ("904", Obj(category: -7)),   // Ginger Ale, Banana Pudding
                ("905", Obj(category: -7)), ("906", Obj(category: -7)),   // Mango Sticky Rice, Poi
                ("907", Obj(category: -7)),                               // Tropical Curry
                ("194", Obj(category: -7))),                              // Fried Egg (control)
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
        Assert.Equal(new[] { "(O)335" }, pools.Metals.Select(p => p.ItemId));
        Assert.Equal(new[] { "(O)194" }, pools.Cooking.Select(p => p.ItemId));
    }

    [Fact]
    public void DefaultTuning_ExcludesGoldenCoconutIslandDrops_FromGeodeMinerals()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(
                ("831", Obj(category: -74)),  // Taro Tuber (Golden Coconut drop)
                ("833", Obj(category: -74)),  // Pineapple Seeds (Golden Coconut drop)
                ("820", Obj(type: "Arch")),   // Fossilized Skull (Golden Coconut drop)
                ("829", Obj()),               // Ginger (Golden Coconut drop)
                ("852", Obj()),               // Dragon Tooth (Golden Coconut drop)
                ("86", Obj())),               // Earth Crystal (control; also in defaults)
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(),
            new[]
            {
                new RawGeodeDropEntry("831"), new RawGeodeDropEntry("833"),
                new RawGeodeDropEntry("820"), new RawGeodeDropEntry("829"),
                new RawGeodeDropEntry("852"), new RawGeodeDropEntry("86"),
            },
            new BundleGenerationTuning());
        Assert.DoesNotContain(pools.GeodeMinerals, p => p.ItemId is "(O)831" or "(O)833" or "(O)820" or "(O)829" or "(O)852");
        Assert.Contains(pools.GeodeMinerals, p => p.ItemId == "(O)86");
    }

    [Fact]
    public void DefaultMarkers_ExcludeBugLand_MutantBugLairIsPostCcContent()
    {
        var markers = new BundleGenerationTuning().ExcludedLocationMarkers;
        Assert.True(ItemPoolBuilder.IsExcludedLocation("BugLand", markers));
        Assert.True(ItemPoolBuilder.IsExcludedLocation("WitchSwamp", markers)); // Void Salmon: Witch's Swamp is behind the post-CC Dark Talisman quest (0.12.18)
        Assert.Contains("(O)795", ItemPoolBuilder.BuiltInExcludedItemIds);
    }

    /// <summary>Golden Egg (Nexus 1127469, gazumbrado, 2026-09-02): Golden Chickens need
    /// Perfection, so it is post-CC by definition, yet Data/Objects does not flag it out of
    /// random sale the way it flags Void Egg and Ostrich Egg. The built-in list has to name it.</summary>
    [Fact]
    public void GoldenEgg_IsPerfectionLocked_SoTheBuiltInListExcludesIt()
    {
        Assert.Contains("(O)928", ItemPoolBuilder.BuiltInExcludedItemIds);
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("928", Obj(category: -5)), ("176", Obj(category: -5))),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), System.Array.Empty<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
        IReadOnlyList<PoolItem> eggs = pools.ByKind[ItemKind.Egg];
        Assert.DoesNotContain(eggs, p => p.ItemId == "(O)928");
        Assert.Contains(eggs, p => p.ItemId == "(O)176");
    }

    /// <summary>Non-habitat location keys (player report, 2026-08-28: an ocean fish in the Lake
    /// Fish bundle). Data/Locations carries three keys that are not places anyone fishes:
    /// "Temp" (the Festival of Ice contest map, whose season-less rows mix river and ocean
    /// fish), "fishingGame" (the Fair minigame) and "Default" (the trash / Joja Cola table).
    /// Treating them as habitats leaked Red Mullet into Lake Fish and Bream/Pike into Ocean
    /// Fish, marked river fish catchable year-round, and put Trash in the Fish pool. Exact
    /// match only: a modded "Temple" or "DefaultFarm" map is a real place.</summary>
    [Theory]
    [InlineData("Temp", true)]
    [InlineData("fishingGame", true)]
    [InlineData("FishingGame", true)]
    [InlineData("Default", true)]
    [InlineData("Custom_Temple", false)]
    [InlineData("DefaultFarm", false)]
    [InlineData("Beach", false)]
    public void NonHabitatLocationKeys_AreExcluded_ExactMatchOnly(string key, bool excluded)
    {
        var emptied = new List<string>(); // must hold even when a saved config wiped the marker list
        Assert.Equal(excluded, ItemPoolBuilder.IsExcludedLocation(key, emptied));
    }

    /// <summary>Regression for the config-override trap found on the live install
    /// (2026-08-24): SMAPI's ReadConfig replaces serialized LIST defaults wholesale, and
    /// the machine's config.json carried `ExcludedItemIds: []` — so exclusions that lived
    /// only in tuning defaults silently vanished. The island/Qi vetting must hold even
    /// when every tuning list has been emptied by a saved config.</summary>
    [Fact]
    public void Vetting_SurvivesConfigOverride_EmptiedTuningLists()
    {
        var overridden = new BundleGenerationTuning
        {
            ExcludedItemIds = new List<string>(),
            ExcludedLocationMarkers = new List<string>(),
            QualityIneligibleItemIds = new List<string>(),
        };
        var pools = ItemPoolBuilder.Build(
            new[]
            {
                new RawCropEntry("889", new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter }),
                new RawCropEntry("24", new[] { Season.Spring }),
            },
            Objects(("889", Obj()), ("24", Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(), overridden);
        Assert.Equal(new[] { "(O)24" }, pools.Crops.Select(p => p.ItemId));
        Assert.True(ItemPoolBuilder.IsExcludedLocation("BugLand", overridden.ExcludedLocationMarkers));

        // Quality-ineligibility also holds with an emptied config list.
        overridden.GoldQualityChance = 1.0;
        var fishPools = new ItemPools
        {
            Fish = new[]
            {
                new PoolItem("(O)153", 15, 3, Array.Empty<Season>(), Array.Empty<string>()),
                new PoolItem("(O)144", 100, 3, Array.Empty<Season>(), Array.Empty<string>()),
            },
        };
        var spec2 = new BundleSpec("Fish Tank", 0, "F", "F", "O 495 30", 0, 2,
            new List<BundleSlotSpec> { new("901", 1, 0), new("902", 1, 0) });
        var filled = BundleSlotFiller.Fill(
            spec2, new DomainMatch(PoolDomain.Fish, null), fishPools, overridden, new Random(3));
        foreach (BundleSlotSpec slot in filled.Slots)
            Assert.Equal(slot.ItemId == "(O)153" ? 0 : 2, slot.Quality);
    }

    [Fact]
    public void SlotFiller_NeverAsksQualityOnAlgae_QualityIneligibleIds()
    {
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0 };
        var pool = new[]
        {
            new PoolItem("(O)152", 20, 3, Array.Empty<Season>(), Array.Empty<string>()), // Seaweed
            new PoolItem("(O)153", 15, 3, Array.Empty<Season>(), Array.Empty<string>()), // Green Algae
            new PoolItem("(O)157", 25, 3, Array.Empty<Season>(), Array.Empty<string>()), // White Algae
            new PoolItem("(O)144", 100, 3, Array.Empty<Season>(), Array.Empty<string>()), // Pike
        };
        var slots = Enumerable.Range(0, 4).Select(i => new BundleSlotSpec((900 + i).ToString(), 1, 0)).ToList();
        var spec = new BundleSpec("Fish Tank", 0, "Some Fish", "Some Fish", "O 495 30", 0, 4, slots);

        var filled = BundleSlotFiller.Fill(
            spec, new DomainMatch(PoolDomain.Fish, null),
            new ItemPools { Fish = pool }, tuning, new Random(3));

        Assert.NotSame(spec, filled);
        foreach (BundleSlotSpec slot in filled.Slots)
        {
            if (slot.ItemId is "(O)152" or "(O)153" or "(O)157")
                Assert.Equal(0, slot.Quality);
            else
                Assert.Equal(2, slot.Quality); // gold chance 1.0 → eligible items always gold
        }
    }
}
