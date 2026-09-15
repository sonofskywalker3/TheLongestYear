using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityBuilderTests
{
    private static ObtainabilityInputs Farm() => new()
    {
        Objects = new Dictionary<string, ObjInfo>
        {
            ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
            ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new[] { "fish_carp" }, false),
            ["(O)812"] = new ObjInfo("(O)812", "Roe", -4, 30, new[] { "roe_item" }, false),
        },
        Shops = new[] { new ShopRow("SeedShop", "(O)472", "SEASON Spring", false) },
        Crops = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) },
        Machines = new[]
        {
            new MachineRow("(BC)15", null, new[] { "category_vegetable" }, null, new[] { new MachineOutput("FLAVORED_ITEM Pickle DROP_IN_ID", null, null) }, 4000, -1),
            new MachineRow("(BC)Preserves", null, new[] { "roe_item" }, null, new[] { new MachineOutput("(O)447", null, null) }, 0, 3),
        },
        LocationFish = new[] { new LocationSpawn("Forest", "(O)142", Season.Fall, null, 1.0, 0, false, 0) },
        FishRows = new Dictionary<string, FishRow> { ["(O)142"] = new FishRow("(O)142", false, "both", 0, "600 2600") },
        Ponds = new[] { new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 1, 1.0, null) }) },
    };

    [Fact]
    public void A_chain_resolves_shop_seed_to_crop_to_pickles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.False(build.HitPassCap);
        Assert.True(build.Passes >= 2);
        DayTable parsnip = build.Model.Table("(O)24", ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Crop } });
        Assert.Equal(5, parsnip.Lands(1));
        Assert.Null(parsnip.Lands(25));
        // 4000 minutes = 3 days: greenhouse parsnip bought Spring 28 lands Summer 4 (day 32), pickles day 35.
        Assert.Equal(8, build.Model.Lands("(O)342", 1, ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Machine } }));
        Assert.Equal(35, build.Model.Lands("(O)342", 28, ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Machine } }));
    }

    [Fact]
    public void A_pond_chain_settles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        // Carp lands in Fall (day 57), population 1, spawn time default 1: the roe lands that same day.
        Assert.Equal(57, build.Model.Lands("(O)812", 1, ObtainFilter.Any with { Kinds = new[] { SourceKind.FishPond } }));
        // Roe lands day 57 (dependably, from the pond), then 3 days through the Preserves Jar to Aged
        // Roe; DependableOnly excludes the separate, much earlier treasure-chest Aged Roe chance source.
        Assert.Equal(60, build.Model.Lands("(O)447", 1, ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Machine } }));
    }

    [Fact]
    public void A_true_dependency_cycle_settles_without_looping()
    {
        var inputs = new ObtainabilityInputs
        {
            Objects = new Dictionary<string, ObjInfo>
            {
                ["(O)1"] = new ObjInfo("(O)1", "A", -2, 1, new[] { "tag_a" }, false),
                ["(O)2"] = new ObjInfo("(O)2", "B", -2, 1, new[] { "tag_b" }, false),
            },
            Forage = new[] { new LocationSpawn("Town", "(O)1", null, "SEASON Summer", 1.0, 0, false, 0) },
            Machines = new[]
            {
                new MachineRow("(BC)A", null, new[] { "tag_a" }, null, new[] { new MachineOutput("(O)2", null, null) }, 0, 0),
                new MachineRow("(BC)B", null, new[] { "tag_b" }, null, new[] { new MachineOutput("(O)1", null, null) }, 0, 0),
            },
        };
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        Assert.False(build.HitPassCap);
        // (O)1 forages starting Summer 1 (day 29); both zero-day machines pass that day through unchanged.
        Assert.Equal(29, build.Model.Lands("(O)1", 1, ObtainFilter.Any));
        Assert.Equal(29, build.Model.Lands("(O)2", 1, ObtainFilter.Any));
    }

    [Fact]
    public void Unresolved_queries_go_to_diagnostics_not_the_model()
    {
        var inputs = new ObtainabilityInputs
        {
            Forage = new[] { new LocationSpawn("Beach", "LOCATION_FISH Beach BOBBER_X", null, null, 1.0, 0, false, 0) },
        };
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        Assert.Contains(build.Unresolved, u => u.Contains("LOCATION_FISH Beach BOBBER_X"));
        Assert.DoesNotContain(build.Model.ItemIds, id => id.StartsWith(ItemQueries.UnresolvedPrefix));
    }

    [Fact]
    public void Direct_code_sources_are_always_present()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(new ObtainabilityInputs());
        Assert.False(build.Model.Table("(O)378", ObtainFilter.DependableOnly).IsEmpty);   // copper ore node
        Assert.False(build.Model.Table("(O)168", ObtainFilter.Any).IsEmpty);              // fishing trash
    }

    [Fact]
    public void Location_fish_delegation_copies_the_named_locations_rows()
    {
        var rows = new List<LocationSpawn>
        {
            new("Forest", "(O)142", Season.Fall, null, 1.0, 0, false, 0),
            new("Forest", "LOCATION_FISH Town BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
            new("Town", "(O)138", Season.Summer, null, 1.0, 0, false, 0),
            new("Farm_Riverland", "LOCATION_FISH Forest BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
            new("Beach", "LOCATION_FISH Nowhere BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
        };
        var expanded = SpawnSources.ExpandLocationFish(rows);
        Assert.DoesNotContain(expanded, r => r.ItemId.StartsWith("LOCATION_FISH"));
        Assert.Contains(expanded, r => r.Location == "Farm_Riverland" && r.ItemId == "(O)142" && r.Season == Season.Fall);
        Assert.Contains(expanded, r => r.Location == "Farm_Riverland" && r.ItemId == "(O)138");   // through Forest's own delegation to Town
        Assert.Contains(expanded, r => r.Location == "Forest" && r.ItemId == "(O)138");
        Assert.DoesNotContain(expanded, r => r.Location == "Beach");
    }

    [Fact]
    public void A_read_failure_names_its_sections()
    {
        var ex = new ObtainabilityReadException(new[] { "Data/Crops", "Data/Shops" });
        Assert.Equal(2, ex.FailedSections.Count);
        Assert.Contains("Data/Crops", ex.Message);
    }
}
