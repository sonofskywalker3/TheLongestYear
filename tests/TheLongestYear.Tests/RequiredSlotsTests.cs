using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class RequiredSlotsTests
{
    private static BundleSpec Spec(int required, int shown, string room = "Pantry")
    {
        var slots = Enumerable.Range(0, shown)
            .Select(i => new BundleSlotSpec($"(O){100 + i}", 1, 0))
            .ToList();
        return new BundleSpec(room, 1, "Test", "Test", "O 12 1", 0, required, slots);
    }

    private static DifficultyProfile Profile(DifficultyStep step)
        => DifficultyResolver.Resolve(
            new DifficultySettings { RequiredSlots = step }, new GameplayConfig());

    [Fact]
    public void Normal_Returns_The_Same_Instance()
    {
        var s = Spec(4, 6);

        Assert.Same(s, RequiredSlots.Apply(s, Profile(DifficultyStep.Normal)));
    }

    [Fact]
    public void Hard_Requires_One_More()
        => Assert.Equal(5, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Hard)).NumberOfSlots);

    [Fact]
    public void Easy_Requires_One_Fewer()
        => Assert.Equal(3, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Easy)).NumberOfSlots);

    [Fact]
    public void Extreme_Requires_Every_Shown_Slot()
        => Assert.Equal(6, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Extreme)).NumberOfSlots);

    [Fact]
    public void Hard_Cannot_Exceed_The_Shown_Slot_Count()
        => Assert.Equal(3, RequiredSlots.Apply(Spec(3, 3), Profile(DifficultyStep.Hard)).NumberOfSlots);

    [Fact]
    public void Easy_Never_Drops_Below_One()
        => Assert.Equal(1, RequiredSlots.Apply(Spec(1, 4), Profile(DifficultyStep.Easy)).NumberOfSlots);

    [Fact]
    public void The_Shown_Slots_Are_Never_Changed()
    {
        var before = Spec(4, 6);
        var after = RequiredSlots.Apply(before, Profile(DifficultyStep.Extreme));

        Assert.Equal(before.Slots.Count, after.Slots.Count);
        Assert.Equal(before.Slots.Select(s => s.ItemId), after.Slots.Select(s => s.ItemId));
    }

    /// <summary>A money bundle asks for a sum, not for N of M items. VaultAmountMultiplier owns
    /// that number, and a difficulty step must never quietly change what the bus repair costs.</summary>
    [Fact]
    public void A_Vault_Room_Bundle_Is_Untouched()
    {
        var vault = new BundleSpec("Vault", 34, "2,500g", "2,500g", "", 0, 1,
            new List<BundleSlotSpec> { new("-1", 2500, 2500) });

        Assert.Same(vault, RequiredSlots.Apply(vault, Profile(DifficultyStep.Extreme)));
    }

    [Fact]
    public void A_Money_Slot_In_Any_Room_Is_Untouched()
    {
        var money = new BundleSpec("Pantry", 5, "money", "money", "", 0, 1,
            new List<BundleSlotSpec> { new("-1", 2500, 2500), new("(O)24", 1, 0) });

        Assert.Same(money, RequiredSlots.Apply(money, Profile(DifficultyStep.Extreme)));
    }

    /// <summary>Extreme on a bundle that already requires everything is a no-op, and returning the
    /// same reference proves nothing needlessly re-allocated during a full board pass.</summary>
    [Fact]
    public void A_Clamp_That_Lands_On_The_Existing_Value_Returns_The_Same_Instance()
    {
        var s = Spec(6, 6);

        Assert.Same(s, RequiredSlots.Apply(s, Profile(DifficultyStep.Extreme)));
    }

    [Fact]
    public void A_Bundle_With_No_Slots_Is_Untouched()
    {
        var empty = new BundleSpec("Pantry", 1, "Empty", "Empty", "O 12 1", 0, 1,
            new List<BundleSlotSpec>());

        Assert.Same(empty, RequiredSlots.Apply(empty, Profile(DifficultyStep.Hard)));
    }
}
