using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-08-28-obtainable-board, section 3: mine fish, legendaries and year-2
/// crops join the pools at weight 1.</summary>
public class PoolAdditionsTests
{
    private static ItemPools BuildPoolsWithObjects(
        params (string id, string name, bool excludeFromRandomSale, string[] tags)[] entries)
    {
        var objects = entries.ToDictionary(
            e => e.id,
            e => new RawObjectEntry("Basic", -75, 50, e.excludeFromRandomSale, e.tags));
        return ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects,
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
    }

    [Fact]
    public void Mine_fish_and_legendaries_join_the_fish_pool_at_weight_1()
    {
        ItemPools pools = BuildPoolsWithObjects(
            ("158", "Stonefish", excludeFromRandomSale: true, tags: new string[0]),
            ("163", "Legend", excludeFromRandomSale: false, tags: new[] { "fish_legendary" }),
            ("128", "Pufferfish", excludeFromRandomSale: false, tags: new string[0]));
        Assert.Equal(1, pools.Fish.Single(p => p.ItemId == "(O)158").Weight);
        Assert.Equal(1, pools.Fish.Single(p => p.ItemId == "(O)163").Weight);
        Assert.Equal(new[] { "UndergroundMine" }, pools.Fish.Single(p => p.ItemId == "(O)158").Locations);
        Assert.Equal(new[] { Season.Spring }, pools.Fish.Single(p => p.ItemId == "(O)163").Seasons);
    }

    [Fact]
    public void Legendary_with_a_real_spawn_row_still_gets_weight_1()
    {
        // Vanilla actually gives the legendaries a Data/Locations row (that's how the game
        // itself spawns them), and Vets bypasses their ExcludeFromRandomSale flag via
        // PoolAdditions.VetExceptions -- so the main spawn-row loop reaches them BEFORE the
        // additions loop does. The addition must still force the weight down to 1 instead of
        // leaving the item at the ordinary VanillaItemWeight, while keeping the data row's own
        // seasons/locations (which can differ from the curated PoolAdditions.Fish fallback).
        var objects = new Dictionary<string, RawObjectEntry>
        {
            ["163"] = new RawObjectEntry("Fish", -4, 1000, true, new string[0]),
        };
        var fishSpawns = new List<RawSpawnEntry>
        {
            new RawSpawnEntry("(O)163", Season.Spring, null, "Mountain"),
        };
        ItemPools pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects,
            new List<RawSpawnEntry>(), fishSpawns,
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
        PoolItem legend = pools.Fish.Single(p => p.ItemId == "(O)163");
        Assert.Equal(1, legend.Weight);
        Assert.Equal(new[] { Season.Spring }, legend.Seasons);
        Assert.Equal(new[] { "Mountain" }, legend.Locations);
    }

    [Fact]
    public void Missing_addition_ids_never_reach_the_pool()
    {
        // "128" (Pufferfish) is not a PoolAddition id and carries no spawn row, so it must not
        // appear even though it exists in Data/Objects.
        ItemPools pools = BuildPoolsWithObjects(
            ("128", "Pufferfish", excludeFromRandomSale: false, tags: new string[0]));
        Assert.DoesNotContain(pools.Fish, p => p.ItemId == "(O)128");
    }

    [Fact]
    public void Addition_ids_absent_from_objects_are_skipped()
    {
        // None of the PoolAdditions ids are supplied here, so the fish pool stays empty.
        ItemPools pools = BuildPoolsWithObjects();
        Assert.Empty(pools.Fish);
    }

    [Fact]
    public void Year_two_crops_are_excluded_only_on_easy()
    {
        Assert.Contains("(O)266", YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
        Assert.Empty(YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Normal));
    }

    [Theory]
    [InlineData("(O)163", 4)]
    [InlineData("(O)682", 7)]
    [InlineData("(O)775", 13)]
    public void Legendaries_have_pacing_weeks(string id, int week)
    {
        var item = new PoolItem(id, 5000, 1,
            PoolAdditions.Fish.Single(a => a.ItemId == id).Seasons,
            PoolAdditions.Fish.Single(a => a.ItemId == id).Locations);
        Assert.Equal(week, FishAvailability.Derive(item, null).Week);
    }
}
