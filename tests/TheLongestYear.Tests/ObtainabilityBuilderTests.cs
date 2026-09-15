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

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void A_chain_resolves_shop_seed_to_crop_to_pickles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.False(build.HitPassCap);
        Assert.True(build.Passes >= 2);
        Assert.Equal("1-4", build.Model.Table("(O)24", ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Crop } }).ToString());
        Assert.Equal("1-6", build.Model.Table("(O)342", ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Machine } }).ToString());   // greenhouse parsnip reaches week 5, so pickles reach week 6
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void A_pond_chain_settles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.Equal("9-16", build.Model.Table("(O)812", ObtainFilter.Any with { Kinds = new[] { SourceKind.FishPond } }).ToString());
        Assert.Equal("1-16", build.Model.Table("(O)447", ObtainFilter.Any with { Kinds = new[] { SourceKind.Machine } }).ToString());   // treasure-chest roe is every week
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
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
        Assert.Equal("5-8", build.Model.Table("(O)1", ObtainFilter.Any).ToString());
        Assert.Equal("5-8", build.Model.Table("(O)2", ObtainFilter.Any).ToString());
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
}
