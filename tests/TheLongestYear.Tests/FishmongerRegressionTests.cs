using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The regression fixture for the exact case that prompted source reachability (Nexus
/// posts 2026-09-10, pitytheviolins): The Fishmonger (Nexus 16326) asks the Community Center for
/// items only Constance sells, in a shop on Ginger Island, inside a map named
/// "VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside" whose only door leads to IslandSouth. That
/// name contains no substring any location marker would catch; only walking doors finds it.
///
/// Ten crops, whose seeds Constance alone sells. Eleven cooked dishes, all taught only by her, all
/// carrying the literal unlock field "none": six need a modded ingredient (the INGREDIENT rule
/// catches those), five are cooked from entirely vanilla ingredients (only the LEARNABILITY rule
/// catches those, which is why this fixture exists). Twenty-one island fish are also in the pack,
/// already handled by the pre-existing location marker and deliberately left out of every id list
/// this fixture feeds into SourceReachability: the fixture would be worthless if it could pass with
/// the learnability rule deleted, and a fish accidentally caught by this rule would hide exactly
/// that kind of false confidence.
///
/// Data source: <c>Fixtures/fishmonger_sources.json</c>, transcribed from the extracted pack
/// (Nexus 16326). JSON over hardcoded constants here, matching <c>CccbClassificationTests</c>'
/// pattern, because this is a "read from the real extracted pack" fixture the same way that one
/// is, and keeping the data in a file makes it obvious at a glance that nothing here was invented
/// for the test.</summary>
public class FishmongerRegressionTests
{
    private const string Farm = "Farm";
    private const string Town = "Town";
    private const string Beach = "Beach";
    private const string IslandSouth = "IslandSouth";
    private const string IdPrefix = "(O)VoidWitchCult.CP.TheFishmongerNPC_";

    private sealed record FixtureData(
        [property: JsonPropertyName("shopId")] string ShopId,
        [property: JsonPropertyName("shopLocation")] string ShopLocation,
        [property: JsonPropertyName("warpOut")] string WarpOut,
        [property: JsonPropertyName("seeds")] string[] Seeds,
        [property: JsonPropertyName("crops")] string[] Crops,
        [property: JsonPropertyName("dishesWithModdedIngredients")] string[] DishesWithModdedIngredients,
        [property: JsonPropertyName("dishesAllVanillaIngredients")] string[] DishesAllVanillaIngredients,
        [property: JsonPropertyName("islandFish")] string[] IslandFish)
    {
        public IEnumerable<string> AllDishes => DishesWithModdedIngredients.Concat(DishesAllVanillaIngredients);
    }

    // Real ingredient lists from the extracted pack, category refs included (Task 5's review gap:
    // no earlier fixture had a category ref in it). A category ref must never condemn a dish, so
    // these five drop ONLY because their recipe cannot be learned anywhere reachable.
    private static readonly Dictionary<string, string[]> VanillaIngredients = new(StringComparer.Ordinal)
    {
        ["bakedredsnappercurry"] = new[] { "(O)150", "(O)192", "(O)260" },
        ["crispyfishandchips"] = new[] { "-4", "(O)192", "(O)247" },
        ["mouthwateringfishburger"] = new[] { "-5", "(O)216", "(O)256" },
        ["fishcroquettesaioli"] = new[] { "-4", "(O)246", "(O)247", "(O)248" },
        ["crispysalmonschnitzel"] = new[] { "(O)139", "(O)247", "(O)216" },
    };

    private static string Id(string rawId) => IdPrefix + rawId;

    private static FixtureData LoadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fishmonger_sources.json");
        return JsonSerializer.Deserialize<FixtureData>(File.ReadAllText(path))!;
    }

    private static SourceReachability BuildFromFixture(out FixtureData fixture)
    {
        fixture = LoadFixture();

        // The real warp graph, not a hand-written unreachable set: Farm -> Town -> Beach ->
        // IslandSouth -> the Fishmonger's shop map. GI_Inside's name matches no marker; it only
        // drops out of reach because its only door goes through IslandSouth, which does.
        var allLocations = new[] { Farm, Town, Beach, IslandSouth, fixture.ShopLocation };
        var links = new[]
        {
            new RawLocationLink(Farm, Town),
            new RawLocationLink(Town, Beach),
            new RawLocationLink(Beach, IslandSouth),
            new RawLocationLink(IslandSouth, fixture.ShopLocation),
        };
        var tuning = new BundleGenerationTuning();
        IReadOnlySet<string> unreachableLocations = ReachabilityGraph.UnreachableLocations(
            links, allLocations,
            name => ItemPoolBuilder.IsExcludedLocation(name, tuning.ExcludedLocationMarkers));

        // Sanity on the walk itself: both the marked location and the shop map it alone leads to
        // must fall out, or the rest of this fixture would be testing nothing.
        Assert.Contains(IslandSouth, unreachableLocations);
        Assert.Contains(fixture.ShopLocation, unreachableLocations);

        var shopListings = new List<RawShopListing>();
        foreach (string seed in fixture.Seeds)
            shopListings.Add(new RawShopListing(Id(seed), fixture.ShopId));
        foreach (string dish in fixture.AllDishes)
            shopListings.Add(new RawShopListing(Id(dish), fixture.ShopId, IsRecipe: true));

        var shopPlacements = new[] { new RawShopPlacement(fixture.ShopId, fixture.ShopLocation) };

        var crops = new List<RawCropEntry>();
        for (int i = 0; i < fixture.Crops.Length; i++)
            crops.Add(new RawCropEntry(Id(fixture.Crops[i]), new[] { Season.Fall }, null, Id(fixture.Seeds[i])));

        // Every Fishmonger recipe carries the literal unlock field "none": Constance is the only
        // teacher, and she is behind the door this fixture cuts off.
        var recipes = new List<RawRecipeEntry>();
        string[] moddedCrops = fixture.Crops;
        for (int i = 0; i < fixture.DishesWithModdedIngredients.Length; i++)
        {
            string dish = fixture.DishesWithModdedIngredients[i];
            string moddedIngredient = Id(moddedCrops[i % moddedCrops.Length]);
            recipes.Add(new RawRecipeEntry(Id(dish), new[] { moddedIngredient }, "none"));
        }
        foreach (string dish in fixture.DishesAllVanillaIngredients)
            recipes.Add(new RawRecipeEntry(Id(dish), VanillaIngredients[dish], "none"));

        var noSpawns = new HashSet<string>(StringComparer.Ordinal);

        return new SourceReachability(
            unreachableLocations, shopListings, shopPlacements, crops, recipes, noSpawns);
    }

    [Fact]
    public void All_ten_crops_and_all_eleven_dishes_are_unreachable()
    {
        SourceReachability rule = BuildFromFixture(out FixtureData fixture);

        foreach (string crop in fixture.Crops)
            Assert.True(rule.IsUnreachable(Id(crop)), $"{crop} should be unreachable");
        foreach (string dish in fixture.AllDishes)
            Assert.True(rule.IsUnreachable(Id(dish)), $"{dish} should be unreachable");
    }

    [Fact]
    public void The_five_all_vanilla_dishes_drop_via_learnability_not_ingredients()
    {
        SourceReachability rule = BuildFromFixture(out FixtureData fixture);

        foreach (string dish in fixture.DishesAllVanillaIngredients)
        {
            string id = Id(dish);
            Assert.True(rule.IsUnreachable(id), $"{dish} should be unreachable");
            Assert.Contains("recipe", rule.Reasons[id], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Vanilla_ingredients_used_by_those_dishes_stay_reachable()
    {
        SourceReachability rule = BuildFromFixture(out _);
        foreach (string id in new[] { "(O)150", "(O)192", "(O)260", "(O)246", "(O)247", "(O)216" })
            Assert.False(rule.IsUnreachable(id), $"{id} is vanilla and must stay allowed");
    }
}
