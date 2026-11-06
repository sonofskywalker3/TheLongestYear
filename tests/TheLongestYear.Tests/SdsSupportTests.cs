using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Seven Deadly Sins support (2026-11-05). A player lost the Wolf Staff after the first
/// loop: the mod records the pickup as a seen-event id, the rewind put every seen id back, and the
/// staff itself went with the inventory.</summary>
public class SdsSupportTests
{
    [Fact]
    public void The_wolf_staff_pickup_flag_is_not_put_back_by_the_rewind()
        => Assert.Contains("ricehit.SevenDeadlySinsCP_Pickup_SDS_WolfStaff", SdsUnlocks.RewoundEventIds);

    [Fact]
    public void The_scenes_that_open_the_seasonal_store_and_the_hotel_rewind()
    {
        Assert.Contains("19215319", SdsUnlocks.RewoundEventIds);
        Assert.Contains("19215323", SdsUnlocks.RewoundEventIds);
        // The whole chain, or a later scene would wait on a first one that never replays.
        foreach (string id in SdsUnlocks.BooChain)
            Assert.Contains(id, SdsUnlocks.RewoundEventIds);
    }

    [Fact]
    public void The_travel_station_forgets_its_destinations()
    {
        Assert.Equal(7, SdsUnlocks.StationScenes.Count);
        foreach (string id in SdsUnlocks.StationScenes)
            Assert.Contains(id, SdsUnlocks.RewoundEventIds);
    }
}
