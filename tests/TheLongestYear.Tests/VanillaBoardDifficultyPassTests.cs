using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class VanillaBoardDifficultyPassTests
{
    private const string ArtisanKey = "Pantry/5";

    // name / reward / ingredients / color / numberOfSlots / sprite / displayName
    private const string ArtisanXFourYSix =
        "Artisan/O 12 1/348 1 0 424 1 0 426 1 0 428 1 0 344 1 0 807 1 0/1/4//Artisan";

    private static DifficultyProfile P(DifficultySettings settings)
        => DifficultyResolver.Resolve(settings, new GameplayConfig());

    private static IDictionary<string, string> Board(string key, string value)
        => new Dictionary<string, string> { [key] = value };

    private static ParsedBundle Out(IDictionary<string, string> result, string key)
        => BundleParsing.Parse(key, result[key]);

    [Fact]
    public void All_Normal_Returns_The_Same_Instance()
    {
        var data = Board(ArtisanKey, ArtisanXFourYSix);

        Assert.Same(data, VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings()), new BundleGenerationTuning(), 123));
    }

    /// <summary>Item rarity cannot apply to a vanilla board, so it must not drag the pass into
    /// running and rewriting a board that should have been left alone.</summary>
    [Fact]
    public void Item_Rarity_Alone_Does_Not_Trigger_The_Pass()
    {
        var data = Board(ArtisanKey, ArtisanXFourYSix);

        Assert.Same(data, VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { ItemRarity = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 123));
    }

    [Fact]
    public void Hard_Stacks_Scale_And_Item_Ids_Are_Untouched()
    {
        var data = Board("Pantry/6", "Fodder/O 12 1/262 10 0 178 10 0 613 3 0/2/3//Fodder");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { StackSize = DifficultyStep.Hard }),
            new BundleGenerationTuning(), 123), "Pantry/6");

        Assert.Equal(new[] { "262", "178", "613" }, parsed.Ingredients.Select(i => i.ItemRef));
        Assert.Equal(new[] { 15, 15, 5 }, parsed.Ingredients.Select(i => i.Stack));   // 10*1.5, 3*1.5=4.5
    }

    [Fact]
    public void Stacks_Are_Capped_At_Ninety_Nine()
    {
        var data = Board("Crafts Room/1", "Construction/O 12 1/388 99 0/1/1//Construction");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { StackSize = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7), "Crafts Room/1");

        Assert.Equal(99, parsed.Ingredients.Single().Stack);
    }

    [Fact]
    public void Easy_Stacks_Never_Fall_Below_One()
    {
        var data = Board("Crafts Room/1", "Construction/O 12 1/388 1 0/1/1//Construction");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { StackSize = DifficultyStep.Easy }),
            new BundleGenerationTuning(), 7), "Crafts Room/1");

        Assert.Equal(1, parsed.Ingredients.Single().Stack);
    }

    [Fact]
    public void Money_Bundles_Are_Never_Touched()
    {
        const string vault = "2,500g/O 12 1/-1 2500 2500/4/1//2,500g";
        var data = Board("Vault/34", vault);

        var result = VanillaBoardDifficultyPass.Apply(
            data,
            P(new DifficultySettings
            {
                StackSize = DifficultyStep.Extreme,
                RequiredSlots = DifficultyStep.Extreme,
                QualityAsks = DifficultyStep.Extreme,
            }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(vault, result["Vault/34"]);
    }

    [Fact]
    public void Extreme_Required_Slots_Demands_Every_Shown_Ingredient()
    {
        var result = VanillaBoardDifficultyPass.Apply(
            Board(ArtisanKey, ArtisanXFourYSix),
            P(new DifficultySettings { RequiredSlots = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(6, Out(result, ArtisanKey).NumberOfSlots);
    }

    [Fact]
    public void Hard_Required_Slots_Adds_One()
    {
        var result = VanillaBoardDifficultyPass.Apply(
            Board(ArtisanKey, ArtisanXFourYSix),
            P(new DifficultySettings { RequiredSlots = DifficultyStep.Hard }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(5, Out(result, ArtisanKey).NumberOfSlots);
    }

    [Fact]
    public void Easy_Required_Slots_Removes_One_But_Never_Reaches_Zero()
    {
        var result = VanillaBoardDifficultyPass.Apply(
            Board("Crafts Room/1", "Construction/O 12 1/388 99 0/1/1//Construction"),
            P(new DifficultySettings { RequiredSlots = DifficultyStep.Easy }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(1, Out(result, "Crafts Room/1").NumberOfSlots);
    }

    /// <summary>Nexus 1122358. Seaweed is in the built-in never-quality set: a star on it would be
    /// an impossible slot, and no difficulty step is allowed to create one.</summary>
    [Fact]
    public void Quality_Is_Never_Added_To_A_Built_In_Ineligible_Item()
    {
        var data = Board("Fish Tank/4", "Specialty Fish/O 12 1/152 1 0/1/1//Specialty Fish");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7), "Fish Tank/4");

        Assert.Equal(0, parsed.Ingredients.Single().Quality);
    }

    [Fact]
    public void Quality_Is_Never_Added_To_A_Config_Excluded_Item()
    {
        var tuning = new BundleGenerationTuning();
        tuning.QualityIneligibleItemIds.Add("(O)815");
        var data = Board("Pantry/9", "Tea/O 12 1/815 1 0/1/1//Tea");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
            tuning, 7), "Pantry/9");

        Assert.Equal(0, parsed.Ingredients.Single().Quality);
    }

    [Fact]
    public void Quality_Is_Never_Added_To_An_Item_Outside_The_Eligible_Set()
    {
        var data = Board("Pantry/9", "Crops/O 12 1/24 1 0/1/1//Crops");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7,
            qualityEligibleIds: new HashSet<string>()), "Pantry/9");

        Assert.Equal(0, parsed.Ingredients.Single().Quality);
    }

    /// <summary>A category slot ("-5" = any animal product) has no known item until the player
    /// picks one, so a minimum-quality ask on it is meaningless.</summary>
    [Fact]
    public void Quality_Is_Never_Added_To_A_Category_Slot()
    {
        var data = Board("Pantry/9", "Animal/O 12 1/-5 1 0/1/1//Animal");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7), "Pantry/9");

        Assert.Equal(0, parsed.Ingredients.Single().Quality);
    }

    [Fact]
    public void Extreme_Adds_Quality_To_Some_Eligible_Plain_Slots()
    {
        string slots = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"{200 + i} 1 0"));
        var data = Board("Pantry/7", $"Big/O 12 1/{slots}/3/60//Big");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7), "Pantry/7");

        Assert.Contains(parsed.Ingredients, i => i.Quality > 0);
        Assert.Contains(parsed.Ingredients, i => i.Quality == 0);
    }

    [Fact]
    public void Easy_Strips_Some_Existing_Quality_Stars()
    {
        string slots = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"{200 + i} 1 2"));
        var data = Board("Pantry/7", $"Quality Crops/O 12 1/{slots}/3/60//Quality Crops");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Easy }),
            new BundleGenerationTuning(), 7), "Pantry/7");

        int stillGold = parsed.Ingredients.Count(i => i.Quality == QualityGold);
        Assert.InRange(stillGold, 1, 59);
    }

    private const int QualityGold = 2;

    /// <summary>Hard adds stars, it never removes one vanilla authored.</summary>
    [Fact]
    public void Hard_Never_Downgrades_An_Existing_Star()
    {
        var data = Board("Pantry/7", "Quality Crops/O 12 1/24 5 2 190 5 2 254 5 2/3/3//Quality Crops");

        var parsed = Out(VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { QualityAsks = DifficultyStep.Hard }),
            new BundleGenerationTuning(), 7), "Pantry/7");

        Assert.All(parsed.Ingredients, i => Assert.Equal(QualityGold, i.Quality));
    }

    [Fact]
    public void The_Pass_Is_Deterministic_For_A_Given_Seed()
    {
        var settings = new DifficultySettings
        {
            StackSize = DifficultyStep.Hard,
            QualityAsks = DifficultyStep.Hard,
        };

        var a = VanillaBoardDifficultyPass.Apply(
            Board(ArtisanKey, ArtisanXFourYSix), P(settings), new BundleGenerationTuning(), 4242);
        var b = VanillaBoardDifficultyPass.Apply(
            Board(ArtisanKey, ArtisanXFourYSix), P(settings), new BundleGenerationTuning(), 4242);

        Assert.Equal(a[ArtisanKey], b[ArtisanKey]);
    }

    /// <summary>The RNG stream is salted per key, so a bundle's result must not depend on which
    /// other bundles happen to share the board or on dictionary insertion order.</summary>
    [Fact]
    public void A_Bundles_Result_Does_Not_Depend_On_Its_Neighbours()
    {
        var settings = new DifficultySettings { QualityAsks = DifficultyStep.Extreme };

        var alone = VanillaBoardDifficultyPass.Apply(
            Board(ArtisanKey, ArtisanXFourYSix), P(settings), new BundleGenerationTuning(), 99);

        var crowded = VanillaBoardDifficultyPass.Apply(
            new Dictionary<string, string>
            {
                ["Zoo/1"] = "Zoo/O 12 1/24 1 0/1/1//Zoo",
                [ArtisanKey] = ArtisanXFourYSix,
                ["Aardvark/1"] = "Aardvark/O 12 1/24 1 0/1/1//Aardvark",
            },
            P(settings), new BundleGenerationTuning(), 99);

        Assert.Equal(alone[ArtisanKey], crowded[ArtisanKey]);
    }

    /// <summary>The sprite field is written empty by BundleDataWriter and must survive: a round
    /// trip through BundleSpec would silently drop it.</summary>
    [Fact]
    public void Every_Untouched_Field_Survives_Verbatim()
    {
        const string key = "Pantry/8";
        const string value = "Name/O 12 1/24 1 0/6/1/13/Display Name";

        var result = VanillaBoardDifficultyPass.Apply(
            Board(key, value), P(new DifficultySettings { StackSize = DifficultyStep.Hard }),
            new BundleGenerationTuning(), 7);

        string[] before = value.Split('/');
        string[] after = result[key].Split('/');

        Assert.Equal(before.Length, after.Length);
        Assert.Equal(before[0], after[0]);   // name
        Assert.Equal(before[1], after[1]);   // reward
        Assert.Equal(before[3], after[3]);   // color
        Assert.Equal(before[5], after[5]);   // sprite
        Assert.Equal(before[6], after[6]);   // display name
    }

    [Fact]
    public void A_Malformed_Entry_Is_Left_Exactly_As_Found()
    {
        const string junk = "not-a-bundle";
        var result = VanillaBoardDifficultyPass.Apply(
            Board("Pantry/9", junk),
            P(new DifficultySettings { StackSize = DifficultyStep.Extreme }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(junk, result["Pantry/9"]);
    }

    [Fact]
    public void Every_Key_Survives_The_Pass()
    {
        var data = new Dictionary<string, string>
        {
            ["Pantry/5"] = ArtisanXFourYSix,
            ["Vault/34"] = "2,500g/O 12 1/-1 2500 2500/4/1//2,500g",
            ["Crafts Room/1"] = "Construction/O 12 1/388 99 0/1/1//Construction",
        };

        var result = VanillaBoardDifficultyPass.Apply(
            data, P(new DifficultySettings { StackSize = DifficultyStep.Hard }),
            new BundleGenerationTuning(), 7);

        Assert.Equal(data.Keys.OrderBy(k => k), result.Keys.OrderBy(k => k));
    }
}
