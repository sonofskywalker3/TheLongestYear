using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class FishingTrashTests
{
    [Theory]
    [InlineData("(O)168")] [InlineData("(O)169")] [InlineData("(O)170")] [InlineData("(O)171")] [InlineData("(O)172")] [InlineData("(O)167")]
    public void Trash_is_week_1(string id) => Assert.Equal(1, FishingTrashAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Trash_beats_the_fish_pond_route()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["718"] = new RawObjectEntry("Fish", -4, 30, false, new string[0], "Cockle") },
            FishPonds = new List<RawFishPondRule>
            {
                new(new[] { "item_cockle" }, new[] { new RawFishPondProduct("(O)168", 0) }),
            },
        };
        var derived = new Dictionary<string, ItemAvailability>
        {
            ["(O)718"] = new ItemAvailability(Season.Spring, 0, "cockle", EffortSource.Derived, 1, Season.Spring),
        };
        var composer = new EffortComposer(data, derived, hasKitchen: false);
        Assert.Equal(1, composer.Derive("(O)168")!.EarliestWeek);
    }

    [Fact]
    public void A_forage_route_beats_a_trap_row_for_clam()
    {
        var pools = new ItemPools { TrapFishIds = new HashSet<string> { "(O)372" } };
        var data = new EffortData
        {
            ForageSpawns = new List<RawSpawnEntry> { new RawSpawnEntry("(O)372", null, null, "Beach") },
        };
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);
        Assert.Equal(1, model.For("(O)372").Week);
    }
}
