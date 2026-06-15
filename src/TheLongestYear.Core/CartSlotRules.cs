using System;

namespace TheLongestYear.Core;

/// <summary>
/// Pure mapping from the player's highest owned cart-slot upgrade tier to how many items the
/// Traveling Cart should display. Tier N (cart_slot_N) == N visible slots; with no upgrades the
/// cart shows a single item. Caps at the vanilla full-stock size (10).
/// </summary>
public static class CartSlotRules
{
    public const int MinSlots = 1;
    public const int MaxSlots = 10;

    /// <summary>Total visible cart slots for a player whose highest owned cart_slot tier is
    /// <paramref name="highestOwnedTier"/> (0 when none owned).</summary>
    public static int VisibleSlots(int highestOwnedTier)
        => Math.Clamp(highestOwnedTier <= 0 ? MinSlots : highestOwnedTier, MinSlots, MaxSlots);
}
