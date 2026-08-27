using Xunit;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class CartSlotRulesTests
{
    [Fact]
    public void No_upgrades_owned_shows_one_slot()
        => Assert.Equal(1, CartSlotRules.VisibleSlots(0));

    [Fact]
    public void Tier_maps_to_total_slot_count()
    {
        Assert.Equal(2, CartSlotRules.VisibleSlots(2));   // cart_slot_2 owned -> 2 visible
        Assert.Equal(7, CartSlotRules.VisibleSlots(7));
    }

    [Fact]
    public void Caps_at_ten()
        => Assert.Equal(10, CartSlotRules.VisibleSlots(10));

    [Fact]
    public void Never_below_one()
        => Assert.Equal(1, CartSlotRules.VisibleSlots(-3));
}

public class CartSlotStartingSlotsTests
{
    [Fact]
    public void The_Default_Is_Unchanged()
        => Assert.Equal(1, CartSlotRules.VisibleSlots(0));

    [Fact]
    public void No_Upgrades_Shows_The_Configured_Starting_Slots()
        => Assert.Equal(3, CartSlotRules.VisibleSlots(0, startingSlots: 3));

    /// <summary>Hard and Extreme both floor at zero, so the cart shows nothing until Cart Stall I
    /// is bought. The GMCM tooltip has to say so, or it reads as a broken cart.</summary>
    [Fact]
    public void Zero_Starting_Slots_Means_An_Empty_Cart()
        => Assert.Equal(0, CartSlotRules.VisibleSlots(0, startingSlots: 0));

    [Fact]
    public void An_Owned_Tier_Still_Wins_Over_A_Lower_Starting_Floor()
        => Assert.Equal(5, CartSlotRules.VisibleSlots(5, startingSlots: 0));

    /// <summary>Buying Cart Stall I on an Easy run must never SHRINK the cart.</summary>
    [Fact]
    public void An_Owned_Tier_Below_The_Starting_Floor_Does_Not_Shrink_The_Cart()
        => Assert.Equal(3, CartSlotRules.VisibleSlots(2, startingSlots: 3));

    [Fact]
    public void The_Cap_Still_Holds()
        => Assert.Equal(CartSlotRules.MaxSlots,
                        CartSlotRules.VisibleSlots(99, startingSlots: 99));

    [Fact]
    public void A_Negative_Starting_Floor_Is_Clamped_To_Zero()
        => Assert.Equal(0, CartSlotRules.VisibleSlots(0, startingSlots: -5));
}
