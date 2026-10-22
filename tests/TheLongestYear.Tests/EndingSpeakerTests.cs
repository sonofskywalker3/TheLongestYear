using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingSpeakerTests
{
    private static MetaState Meta(params (string npc, int fam, int loops)[] rows)
    {
        var m = new MetaState();
        foreach (var (npc, fam, loops) in rows)
        {
            m.VillagerFamiliarity[npc] = fam;
            var mem = new VillagerMemory();
            for (int i = 1; i <= loops; i++) mem.Loops.Add(i);
            m.VillagerMemory[npc] = mem;
        }
        return m;
    }

    [Fact]
    public void Highest_score_at_or_above_threshold_wins()
        => Assert.Equal("Pierre", EndingSpeaker.Pick(Meta(("Pierre", 90, 1), ("Robin", 70, 1)), 60, _ => true));

    [Fact]
    public void Nobody_below_threshold()
        => Assert.Null(EndingSpeaker.Pick(Meta(("Pierre", 59, 3)), 60, _ => true));

    [Fact]
    public void Ties_break_on_loops_then_name()
    {
        Assert.Equal("Robin", EndingSpeaker.Pick(Meta(("Pierre", 80, 1), ("Robin", 80, 2)), 60, _ => true));
        Assert.Equal("Pierre", EndingSpeaker.Pick(Meta(("Pierre", 80, 2), ("Robin", 80, 2)), 60, _ => true));
    }

    [Fact]
    public void Ineligible_villagers_are_skipped()
        => Assert.Equal("Robin", EndingSpeaker.Pick(Meta(("Pierre", 90, 1), ("Robin", 70, 1)), 60, n => n != "Pierre"));

    [Fact]
    public void Score_only_save_without_memory_still_qualifies()
    {
        var m = new MetaState();
        m.VillagerFamiliarity["Gus"] = 100;
        Assert.Equal("Gus", EndingSpeaker.Pick(m, 60, _ => true));
    }
}
