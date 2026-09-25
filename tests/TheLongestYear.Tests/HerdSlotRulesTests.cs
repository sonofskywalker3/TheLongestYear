using System;
using TheLongestYear.Core;
using static TheLongestYear.Core.HerdSlotKind;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class HerdSlotRulesTests
{
    [Fact]
    public void Ladder_is_the_designed_eighteen_slots_in_order()
    {
        Assert.Equal(
            new[] { Chicken, Chicken, Cow, Cow, Duck, Duck, Goat, Goat, Rabbit, Rabbit,
                    Sheep, Sheep, Pig, Pig, VoidChicken, GoldenChicken, Dinosaur, Ostrich },
            HerdSlotRules.SlotsFor(17));
        Assert.Equal(18, HerdSlotRules.LadderLength);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(17, 18)]
    [InlineData(40, 18)]
    [InlineData(-3, 1)]
    public void SlotsFor_is_one_free_slot_plus_one_per_tier(int tier, int expected)
        => Assert.Equal(expected, HerdSlotRules.SlotsFor(tier).Count);

    [Fact]
    public void The_free_slot_is_a_chicken()
        => Assert.Equal(Chicken, HerdSlotRules.SlotsFor(0)[0]);

    [Theory]
    [InlineData(0, Chicken)]
    [InlineData(2, Cow)]
    [InlineData(14, VoidChicken)]
    [InlineData(17, Ostrich)]
    public void KindAt_reads_the_ladder(int slotIndex, HerdSlotKind expected)
        => Assert.Equal(expected, HerdSlotRules.KindAt(slotIndex));

    [Theory]
    [InlineData(Chicken, "White Chicken", true)]
    [InlineData(Chicken, "Brown Chicken", true)]
    [InlineData(Chicken, "Blue Chicken", true)]
    [InlineData(Chicken, "Void Chicken", false)]
    [InlineData(Chicken, "Golden Chicken", false)]
    [InlineData(Chicken, "Duck", false)]
    [InlineData(Cow, "White Cow", true)]
    [InlineData(Cow, "Brown Cow", true)]
    [InlineData(Cow, "Goat", false)]
    [InlineData(VoidChicken, "Void Chicken", true)]
    [InlineData(VoidChicken, "White Chicken", false)]
    [InlineData(GoldenChicken, "Golden Chicken", true)]
    [InlineData(Duck, "Duck", true)]
    [InlineData(Goat, "Goat", true)]
    [InlineData(Rabbit, "Rabbit", true)]
    [InlineData(Sheep, "Sheep", true)]
    [InlineData(Pig, "Pig", true)]
    [InlineData(Dinosaur, "Dinosaur", true)]
    [InlineData(Ostrich, "Ostrich", true)]
    [InlineData(Ostrich, null, false)]
    [InlineData(Pig, "pig", false)]
    public void Accepts_only_the_slot_kinds_types(HerdSlotKind kind, string? type, bool expected)
        => Assert.Equal(expected, HerdSlotRules.Accepts(kind, type));

    [Theory]
    [InlineData(Chicken, "Coop")]
    [InlineData(VoidChicken, "Coop")]
    [InlineData(GoldenChicken, "Coop")]
    [InlineData(Duck, "Big Coop")]
    [InlineData(Dinosaur, "Big Coop")]
    [InlineData(Rabbit, "Deluxe Coop")]
    [InlineData(Cow, "Barn")]
    [InlineData(Ostrich, "Barn")]
    [InlineData(Goat, "Big Barn")]
    [InlineData(Sheep, "Deluxe Barn")]
    [InlineData(Pig, "Deluxe Barn")]
    public void RequiredHousing_matches_the_start_with_keeps(HerdSlotKind kind, string housing)
        => Assert.Equal(housing, HerdSlotRules.RequiredHousing(kind));

    [Fact]
    public void Every_required_keep_is_a_catalog_row()
    {
        foreach (HerdSlotKind kind in Enum.GetValues<HerdSlotKind>())
            Assert.NotNull(UpgradeCatalog.TryGet(HerdSlotRules.RequiredKeepId(kind)));
    }

    [Fact]
    public void Every_kind_has_a_display_name()
    {
        foreach (HerdSlotKind kind in Enum.GetValues<HerdSlotKind>())
            Assert.False(HerdSlotRules.DisplayName(kind).StartsWith("herd-slot."), kind.ToString());
    }
}
