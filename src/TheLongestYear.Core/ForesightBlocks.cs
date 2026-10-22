namespace TheLongestYear.Core;

/// <summary>
/// Which foresight blocks the Junimo Shrine's Plan tab shows: the Weather Sage forecast and the
/// Cart Whisperer stock.
///
/// Both are rolled from the live loop: the weather from <c>Game1.uniqueIDForThisGame</c> and
/// today's date, the cart from the Traveler shop and the current board. While a loop reset is
/// queued or mid-way, those are still the failing loop's values (the reset re-seeds
/// <c>uniqueIDForThisGame</c> from the clock, rolls a new run seed and writes a new board only
/// once it runs), so any forecast drawn then would describe days that will never happen. Both
/// blocks stay hidden until the reset lands.
/// </summary>
/// <param name="Weather">Show the weather forecast row.</param>
/// <param name="Cart">Show the Traveling Cart stock row.</param>
public readonly record struct ForesightBlocks(bool Weather, bool Cart)
{
    /// <param name="weatherTier">highest kept Weather Sage tier (0 = none)</param>
    /// <param name="cartOwned">the Cart Whisperer upgrade is owned and its catalog is available</param>
    /// <param name="resetPending">a fail, win or restart reset is queued or mid-way</param>
    public static ForesightBlocks For(int weatherTier, bool cartOwned, bool resetPending)
        => resetPending
            ? new ForesightBlocks(Weather: false, Cart: false)
            : new ForesightBlocks(Weather: weatherTier > 0, Cart: cartOwned);
}
