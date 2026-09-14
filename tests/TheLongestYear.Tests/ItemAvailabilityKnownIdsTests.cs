using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class ItemAvailabilityKnownIdsTests
{
    [Fact]
    public void Known_ids_cover_derived_effort_and_accepted_overrides_but_not_rejected_ones()
    {
        var derived = new Dictionary<string, ItemAvailability> { ["(O)1"] = new ItemAvailability(Season.Spring, 3, "fish", EarliestWeek: 2) };
        var effort = new Dictionary<string, ItemEffort> { ["(O)2"] = new ItemEffort(4, "crop", EarliestWeek: 5) };
        var seasonPins = new Dictionary<string, Season> { ["(O)3"] = Season.Fall, ["(O)1"] = Season.Spring };
        var weekPins = new Dictionary<string, int> { ["(O)4"] = 9, ["(O)2"] = 1 };   // (O)2 at week 1 is earlier than its rule: rejected
        var model = new ItemAvailabilityModel(derived, seasonPins, null, effort, weekPins);
        Assert.Equal(new[] { "(O)1", "(O)2", "(O)3", "(O)4" }, model.KnownIds);
    }
}
