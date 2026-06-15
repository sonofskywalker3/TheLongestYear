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
