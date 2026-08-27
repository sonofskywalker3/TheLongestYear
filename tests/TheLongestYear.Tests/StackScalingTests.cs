using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class StackScalingTests
{
    private static DifficultyProfile Profile(DifficultyStep step)
        => DifficultyResolver.Resolve(
            new DifficultySettings { StackSize = step }, new GameplayConfig());

    private static BundleSpec Spec(string room, params (string Id, int Stack)[] slots)
        => new(room, 1, "Test", "Test", "O 12 1", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, 0)).ToList());

    [Theory]
    [InlineData(1, 0.75, 1)]     // Easy never rounds a single ask down to nothing
    [InlineData(1, 1.5, 2)]
    [InlineData(5, 1.5, 8)]      // Quality Crops: 7.5 away from zero
    [InlineData(20, 1.5, 30)]
    [InlineData(99, 1.5, 99)]    // already at the cap
    [InlineData(100, 1.5, 99)]   // a big vanilla ask lands on the cap
    [InlineData(500, 2.0, 99)]   // Sticky's Sap x500
    [InlineData(4, 0.75, 3)]
    public void The_Scalar_Rule(int stack, double factor, int expected)
        => Assert.Equal(expected, StackScaling.ScaleStack(stack, factor));

    [Fact]
    public void A_Factor_Of_One_Is_Identity()
        => Assert.Equal(37, StackScaling.ScaleStack(37, 1.0));

    [Fact]
    public void Normal_Returns_The_Same_Spec_Instance()
    {
        var spec = Spec("Pantry", ("(O)24", 1), ("(O)188", 5));

        Assert.Same(spec, StackScaling.Apply(spec, Profile(DifficultyStep.Normal)));
    }

    /// <summary>The whole point of the change: a bundle the engine kept verbatim from vanilla must
    /// scale too, not just the ones it re-rolled.</summary>
    [Fact]
    public void Every_Slot_Scales_Including_Big_Vanilla_Asks()
    {
        var scaled = StackScaling.Apply(
            Spec("Crafts Room", ("(O)92", 500), ("(O)24", 1), ("(O)188", 20)),
            Profile(DifficultyStep.Hard));

        Assert.Equal(new[] { 99, 2, 30 }, scaled.Slots.Select(s => s.Stack));
    }

    [Fact]
    public void Item_Ids_And_Quality_Are_Never_Changed()
    {
        var spec = new BundleSpec("Pantry", 1, "Q", "Q", "O 12 1", 0, 1,
            new List<BundleSlotSpec> { new("(O)24", 5, 2) });

        var scaled = StackScaling.Apply(spec, Profile(DifficultyStep.Hard));

        Assert.Equal("(O)24", scaled.Slots.Single().ItemId);
        Assert.Equal(2, scaled.Slots.Single().Quality);
        Assert.Equal(8, scaled.Slots.Single().Stack);
    }

    /// <summary>A Vault ask is a sum of gold, not a quantity of an item; VaultAmountMultiplier
    /// owns that number and a difficulty step must never move it.</summary>
    [Fact]
    public void A_Vault_Room_Bundle_Is_Untouched()
    {
        var vault = new BundleSpec("Vault", 34, "2,500g", "2,500g", "", 0, 1,
            new List<BundleSlotSpec> { new("-1", 2500, 2500) });

        Assert.Same(vault, StackScaling.Apply(vault, Profile(DifficultyStep.Extreme)));
    }

    [Fact]
    public void A_Money_Slot_In_Any_Room_Is_Untouched()
    {
        var money = new BundleSpec("Pantry", 5, "m", "m", "", 0, 1,
            new List<BundleSlotSpec> { new("-1", 2500, 0), new("(O)24", 2, 0) });

        Assert.Same(money, StackScaling.Apply(money, Profile(DifficultyStep.Extreme)));
    }

    /// <summary>Easy leaves single-item asks alone (0.75 rounds back to 1), so a spec made only of
    /// them comes back unchanged rather than needlessly re-allocated.</summary>
    [Fact]
    public void A_Spec_Whose_Slots_All_Land_On_Their_Existing_Value_Is_Unchanged()
    {
        var spec = Spec("Pantry", ("(O)24", 1), ("(O)188", 1));

        Assert.Same(spec, StackScaling.Apply(spec, Profile(DifficultyStep.Easy)));
    }

    [Fact]
    public void An_Empty_Spec_Is_Handled()
    {
        var empty = new BundleSpec("Pantry", 1, "E", "E", "O 12 1", 0, 1,
            new List<BundleSlotSpec>());

        Assert.Same(empty, StackScaling.Apply(empty, Profile(DifficultyStep.Extreme)));
    }

    /// <summary>The Engine path and the Vanilla post-pass must agree exactly, which is why they
    /// share one scalar rule rather than each carrying their own arithmetic.</summary>
    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void Both_Board_Paths_Produce_The_Same_Stack(DifficultyStep step)
    {
        DifficultyProfile profile = Profile(step);
        const int authored = 5;

        int engine = StackScaling.Apply(
            Spec("Pantry", ("(O)24", authored)), profile).Slots.Single().Stack;

        var vanilla = VanillaBoardDifficultyPass.Apply(
            new Dictionary<string, string> { ["Pantry/9"] = $"C/O 12 1/24 {authored} 0/1/1//C" },
            profile, new BundleGenerationTuning(), 7);
        int vanillaStack = BundleParsing.Parse("Pantry/9", vanilla["Pantry/9"])
            .Ingredients.Single().Stack;

        Assert.Equal(engine, vanillaStack);
    }
}
