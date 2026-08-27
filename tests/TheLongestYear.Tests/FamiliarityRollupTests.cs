using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FamiliarityRollupTests
{
    [Fact]
    public void Talk_gift_and_heart_event_score_1_3_10()
    {
        var meta = new MetaState();
        int added = FamiliarityRollup.Apply(meta, new[]
        {
            new VillagerDaySignals("Pierre", Talked: true, Gifts: 2, HeartEvents: 1),
            new VillagerDaySignals("Haley", Talked: false, Gifts: 0, HeartEvents: 0),
        });
        Assert.Equal(17, added);
        Assert.Equal(17, meta.VillagerFamiliarity["Pierre"]);
        Assert.False(meta.VillagerFamiliarity.ContainsKey("Haley"));   // nothing happened, no entry
    }

    [Fact]
    public void Days_accumulate_on_the_same_villager()
    {
        var meta = new MetaState();
        for (int day = 0; day < 5; day++)
            FamiliarityRollup.Apply(meta, new[] { new VillagerDaySignals("Pierre", true, 0, 0) });
        Assert.Equal(5, meta.VillagerFamiliarity["Pierre"]);
    }
}
