using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Which foresight blocks the Junimo Shrine's Plan tab shows: Weather Sage's forecast
/// and the Cart Whisperer stock, never while a loop reset is queued or mid-way.</summary>
public class ForesightBlocksTests
{
    [Fact]
    public void Weather_shows_once_a_weather_sage_tier_is_kept()
    {
        Assert.False(ForesightBlocks.For(weatherTier: 0, cartOwned: false, resetPending: false).Weather);
        Assert.True(ForesightBlocks.For(weatherTier: 3, cartOwned: false, resetPending: false).Weather);
    }

    [Fact]
    public void Cart_shows_only_when_the_cart_upgrade_is_owned()
    {
        Assert.False(ForesightBlocks.For(weatherTier: 6, cartOwned: false, resetPending: false).Cart);
        Assert.True(ForesightBlocks.For(weatherTier: 0, cartOwned: true, resetPending: false).Cart);
    }

    [Fact]
    public void Nothing_shows_while_a_reset_is_pending_because_the_next_loop_is_not_rolled_yet()
    {
        ForesightBlocks blocks = ForesightBlocks.For(weatherTier: 6, cartOwned: true, resetPending: true);
        Assert.False(blocks.Weather);
        Assert.False(blocks.Cart);
    }
}
