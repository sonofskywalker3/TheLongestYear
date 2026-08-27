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

    /// <summary>The lowest a difficulty step may push the starting floor. Zero means the cart
    /// shows nothing at all until Cart Stall I is bought.</summary>
    public const int MinStartingSlots = 0;

    /// <summary>Total visible cart slots for a player whose highest owned cart_slot tier is
    /// <paramref name="highestOwnedTier"/> (0 when none owned).</summary>
    /// <param name="startingSlots">How many items the cart shows before any Cart Stall upgrade,
    /// from the run's difficulty profile (spec 2026-08-26). Defaults to <see cref="MinSlots"/>,
    /// the shipping value, so existing callers are unaffected. An owned tier always wins over a
    /// lower floor, and the floor always wins over a lower tier: buying Cart Stall I on an Easy
    /// run must never SHRINK the cart.</param>
    public static int VisibleSlots(int highestOwnedTier, int startingSlots = MinSlots)
    {
        int floor = Math.Clamp(startingSlots, MinStartingSlots, MaxSlots);
        int fromTier = highestOwnedTier > 0 ? highestOwnedTier : floor;
        return Math.Clamp(Math.Max(fromTier, floor), MinStartingSlots, MaxSlots);
    }
}
