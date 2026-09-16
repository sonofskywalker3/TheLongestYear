using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The whole-model check behind tly_sabotage travelcheck, run over a synthetic model: the
/// fairness rule must answer the same with the mine travel in the model as without it.</summary>
public class MineTravelCheckTests
{
    private const string GoldOre = "(O)384";      // mines:floor 80, and a Fall shop row
    private const string GoldBar = "(O)336";
    private const string DinoEgg = "(O)107";      // a not-sold animal's produce: owned-only
    private const string DinoMayo = "(O)807";
    private const string TvDish = "(O)900";
    private const string Mix = "(O)901";
    private const int TvEpisode = 9;

    private static ObtainabilityInputs Inputs() => new()
    {
        Objects = new Dictionary<string, ObjInfo>
        {
            [GoldOre] = new ObjInfo(GoldOre, "Gold Ore", -15, 25, new[] { "id_o_384" }, false),
            [GoldBar] = new ObjInfo(GoldBar, "Gold Bar", -15, 250, new[] { "id_o_336" }, false),
            [DinoEgg] = new ObjInfo(DinoEgg, "Dinosaur Egg", -5, 350, new[] { "id_o_107" }, false),
            [DinoMayo] = new ObjInfo(DinoMayo, "Dinosaur Mayonnaise", -26, 800, new string[0], false),
            [TvDish] = new ObjInfo(TvDish, "Gold Dish", -7, 100, new string[0], false),
            [Mix] = new ObjInfo(Mix, "Gold Egg Mix", -7, 100, new string[0], false),
        },
        // A second route for the ore (Fall only) and an island one.
        Shops = new[]
        {
            new ShopRow("Blacksmith", GoldOre, "SEASON Fall", false),
            new ShopRow("IslandTrade", GoldOre, null, false),
        },
        Machines = new[]
        {
            new MachineRow("(BC)13", null, new[] { "id_o_384" }, null, new[] { new MachineOutput(GoldBar, null, null) }, 0, 1),
            new MachineRow("(BC)24", null, new[] { "id_o_107" }, null, new[] { new MachineOutput(DinoMayo, null, null) }, 0, 1),
        },
        Recipes = new[]
        {
            new RecipeRow("Gold Dish", new[] { GoldBar }, TvDish, "none", true),
            new RecipeRow("Gold Egg Mix", new[] { GoldOre, DinoEgg }, Mix, "default", false),
        },
        CookingChannel = new Dictionary<string, int> { ["Gold Dish"] = TvEpisode },
        Animals = new[] { new AnimalRow("Dinosaur", "Coop", -1, new[] { new AnimalProduce(DinoEgg, null, 0) }, Array.Empty<AnimalProduce>()) },
        Buildings = new Dictionary<string, int> { ["Coop"] = 3 },
    };

    /// <summary>A farm that owns both machines and the dinosaur and knows the crafting recipe, so the
    /// made routes are judged below Extreme too.</summary>
    private static readonly SaveSnapshot Farm = SaveSnapshot.Empty with
    {
        MachinesOwned = new HashSet<string> { "(BC)13", "(BC)24" },
        AnimalsOwned = new HashSet<string> { "Dinosaur" },
        Buildings = new HashSet<string> { "Coop" },
        RecipesKnown = new HashSet<string> { "Gold Egg Mix" },
    };

    private static readonly ObtainabilityModel WithTravel = ObtainabilityBuilder.Build(Inputs()).Model;
    private static readonly ObtainabilityModel WithoutTravel = ObtainabilityBuilder.Build(Inputs(), mineTravel: false).Model;

    [Fact]
    public void The_fixture_has_every_shape_the_check_is_for()
    {
        Assert.Contains(WithTravel.Sources(GoldOre), s => s.Kind == SourceKind.MineNode && s.UndelayedLands != null);
        Assert.Contains(WithTravel.Sources(GoldOre), s => s.Kind == SourceKind.Shop);
        Assert.Contains(WithTravel.Sources(GoldBar), s => s.Kind == SourceKind.Machine && s.UndelayedLands != null);
        Assert.Contains(WithTravel.Sources(TvDish), s => s.Conditions.Requires.Any(r => r.StartsWith("unlock:Queen of Sauce")));
        Assert.Contains(WithTravel.Sources(Mix), s => s.Conditions.OwnedOnly);
        Assert.Contains(WithTravel.Sources(DinoMayo), s => s.Conditions.OwnedOnly);
        // The travel really is in one model and not the other.
        Assert.NotEqual(WithTravel.Table(GoldBar, ObtainFilter.DependableOnly), WithoutTravel.Table(GoldBar, ObtainFilter.DependableOnly));
        Assert.All(WithoutTravel.ItemIds.SelectMany(WithoutTravel.Sources), s => Assert.Null(s.UndelayedLands));
    }

    [Fact]
    public void The_fairness_rule_answers_the_same_with_and_without_the_travel()
    {
        foreach (SaveSnapshot baseSave in new[] { SaveSnapshot.Empty, Farm })
        {
            TravelCheckResult result = MineTravelCheck.Compare(WithTravel, WithoutTravel, baseSave);
            int expected = WithTravel.Count * MineTravelCheck.HitDays.Count * MineTravelCheck.Saves().Count
                * Enum.GetValues<DifficultyStep>().Length * 2;
            Assert.Equal(expected, result.Comparisons);
            Assert.Empty(result.Mismatches);
        }
        // The farm really does reach the made routes at every level: the dish counts on Normal.
        Assert.True(FairnessRule.Counts(TvDish, MineTravelCheck.HitDays[0], FairnessRule.TamperDeadline, DifficultyStep.Normal, Farm, WithTravel));
        Assert.True(FairnessRule.Counts(Mix, MineTravelCheck.HitDays[0], FairnessRule.TamperDeadline, DifficultyStep.Normal, Farm, WithTravel));
    }

    [Fact]
    public void The_check_catches_a_model_that_judges_on_its_delayed_tables()
    {
        // Control: throw the undelayed tables away and the answers must differ somewhere.
        var stripped = new ObtainabilityModel(WithTravel.ItemIds.ToDictionary(
            id => id,
            id => (IReadOnlyList<ObtainSource>)WithTravel.Sources(id).Select(s => s with { UndelayedLands = null }).ToList()));
        TravelCheckResult result = MineTravelCheck.Compare(stripped, WithoutTravel, Farm);
        Assert.Contains(result.Mismatches, m => m.ItemId == GoldBar);
        Assert.Contains(result.Mismatches, m => m.ItemId == TvDish && m.Level == DifficultyStep.Normal);
        Assert.Contains(result.Mismatches, m => m.ItemId == Mix && m.Level == DifficultyStep.Normal);
    }
}
