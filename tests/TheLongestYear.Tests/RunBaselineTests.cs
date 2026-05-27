using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunBaselineTests
{
    [Fact]
    public void Defaults_match_vanilla_baseline()
    {
        var b = new RunBaseline();
        Assert.Equal(500, b.StartingGold);
        Assert.Equal(12, b.MaxItems);
        Assert.Empty(b.ToolTiers);
        Assert.Empty(b.SkillLevels);
        Assert.Empty(b.ProfessionPickerSkillsToRequeue);
        Assert.Equal(0, b.MineElevatorFloor);
        Assert.False(b.KitchenOnDay1);
        Assert.False(b.BusUnlocked);
        Assert.False(b.EarlyHorse);
        Assert.Empty(b.KeptBuildings);
        Assert.Empty(b.StartingAnimals);
    }
}
