using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The load-time board repair draws the same replacement on every load of one loop, and its
/// swaps reach the stored board of record (0.19.9).</summary>
public class BoardRepairStabilityTests
{
    private const ulong FarmerId = 0x1234_5678_9ABC_DEF0UL;

    [Fact]
    public void Seed_is_the_same_for_the_same_farmer_and_seed_loop()
    {
        Assert.Equal(BoardRepairStability.Seed(FarmerId, 3), BoardRepairStability.Seed(FarmerId, 3));
        Assert.NotEqual(BoardRepairStability.Seed(FarmerId, 3), BoardRepairStability.Seed(FarmerId, 4));
    }

    [Fact]
    public void Seed_is_not_the_engine_generation_seed()
        => Assert.NotEqual(BundleEngineSeed.For(FarmerId, 3), BoardRepairStability.Seed(FarmerId, 3));

    [Fact]
    public void Scan_order_ignores_insertion_order()
    {
        var a = new Dictionary<string, string> { ["Pantry/2"] = "", ["Crafts Room/13"] = "", ["Fish Tank/6"] = "" };
        var b = new Dictionary<string, string> { ["Fish Tank/6"] = "", ["Pantry/2"] = "", ["Crafts Room/13"] = "" };
        Assert.Equal(BoardRepairStability.ScanOrder(a.Keys), BoardRepairStability.ScanOrder(b.Keys));
        Assert.Equal(new[] { "Crafts Room/13", "Fish Tank/6", "Pantry/2" }, BoardRepairStability.ScanOrder(b.Keys));
    }

    [Fact]
    public void Repaired_ingredients_reach_the_stored_board_and_the_next_restore_is_a_no_op()
    {
        var stored = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Pantry/2"] = "Fall Crops/BO 10 1/Cornucopia_Zucchini 1 0 270 1 0/2/4//Fall Crops",
        };
        const string repaired = "300 1 0 270 1 0";

        Assert.True(BoardRepairStability.MirrorIngredients(stored, "Pantry/2", repaired));
        Assert.Equal("Fall Crops/BO 10 1/300 1 0 270 1 0/2/4//Fall Crops", stored["Pantry/2"]);

        // The live board after the repair: same ingredients, so a reload's restore writes nothing.
        var live = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Pantry/2"] = "Fall Crops/BO 10 1/300 1 0 270 1 0/2/4//Fall Crops",
        };
        Assert.Empty(TechBoardOfRecord.Updates(true, true, stored, live));
    }

    [Fact]
    public void Mirror_skips_a_missing_board_or_key()
    {
        Assert.False(BoardRepairStability.MirrorIngredients(null, "Pantry/2", "300 1 0"));
        var stored = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0/0/1" };
        Assert.False(BoardRepairStability.MirrorIngredients(stored, "Pantry/2", "300 1 0"));
        Assert.Equal("Spring Crops/O 465 20/24 1 0/0/1", stored["Pantry/0"]);
        var shortValue = new Dictionary<string, string> { ["Pantry/2"] = "Fall Crops/BO 10 1" };
        Assert.False(BoardRepairStability.MirrorIngredients(shortValue, "Pantry/2", "300 1 0"));
    }
}
