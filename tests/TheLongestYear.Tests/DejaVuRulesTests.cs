using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class DejaVuRulesTests
{
    private static MetaState Meta(int resets, string npc, int fam)
    {
        var m = new MetaState { CompletedResets = resets };
        m.VillagerFamiliarity[npc] = fam;
        return m;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(59, 0)]
    [InlineData(60, 1)]
    [InlineData(179, 1)]
    [InlineData(180, 2)]
    public void Tier_boundaries(int fam, int tier) => Assert.Equal(tier, DejaVuRules.Tier(fam, 60));

    [Fact]
    public void Never_in_loop_one_or_below_threshold()
    {
        Assert.False(DejaVuRules.IsEligible(Meta(0, "Pierre", 500), new RunState(), "Pierre", 10, 60));
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Pierre", 59), new RunState(), "Pierre", 10, 60));
        Assert.True(DejaVuRules.IsEligible(Meta(1, "Pierre", 60), new RunState(), "Pierre", 10, 60));
    }

    [Fact]
    public void Per_villager_and_weekly_caps()
    {
        var run = new RunState();
        run.DejaVuShownTo.Add("Pierre");
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Pierre", 100), run, "Pierre", 10, 60));
        var run2 = new RunState { DejaVuLastDay = 10 };
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Haley", 100), run2, "Haley", 16, 60));   // 6 days later
        Assert.True(DejaVuRules.IsEligible(Meta(1, "Haley", 100), run2, "Haley", 17, 60));    // 7 days later
    }

    [Fact]
    public void TryPick_rolls_the_chance_and_stamps_the_caps()
    {
        var cfg = new GameplayConfig();
        var meta = Meta(1, "Pierre", 200);
        var run = new RunState();
        Assert.Equal(0, DejaVuRules.TryPick(meta, run, "Pierre", 30, cfg, _ => 50, force: false));   // roll 50 >= 6: miss
        Assert.Empty(run.DejaVuShownTo);
        Assert.Equal(2, DejaVuRules.TryPick(meta, run, "Pierre", 30, cfg, _ => 3, force: false));    // roll 3 < 6: hit, tier 2
        Assert.Contains("Pierre", run.DejaVuShownTo);
        Assert.Equal(30, run.DejaVuLastDay);
        Assert.Equal(0, DejaVuRules.TryPick(meta, run, "Pierre", 31, cfg, _ => 0, force: false));    // capped now
    }

    [Fact]
    public void Force_bypasses_chance_and_caps_but_not_the_config_switch()
    {
        var cfg = new GameplayConfig();
        var run = new RunState { DejaVuLastDay = 30 };
        run.DejaVuShownTo.Add("Pierre");
        Assert.Equal(1, DejaVuRules.TryPick(Meta(1, "Pierre", 60), run, "Pierre", 31, cfg, _ => 99, force: true));
        Assert.Equal(1, DejaVuRules.TryPick(Meta(0, "Pierre", 10), run, "Pierre", 31, cfg, _ => 99, force: true));   // force even below threshold: tier floor 1
    }

    [Fact]
    public void Disabled_config_never_picks()
    {
        var cfg = new GameplayConfig { EnableDejaVuDialogue = false };
        Assert.Equal(0, DejaVuRules.TryPick(Meta(1, "Pierre", 200), new RunState(), "Pierre", 30, cfg, _ => 0, force: false));
        Assert.Equal(0, DejaVuRules.TryPick(Meta(1, "Pierre", 200), new RunState(), "Pierre", 30, cfg, _ => 0, force: true));
    }
}
