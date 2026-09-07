using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VillagerMemoryTests
{
    private static VillagerDaySignals Day(string npc, bool talked = false, int gifts = 0, int hearts = 0,
        bool birthday = false, params string[] heartIds)
        => new(npc, talked, gifts, hearts, birthday, heartIds);

    [Fact]
    public void Counts_and_score_move_together()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", talked: true, gifts: 1) }, loopNumber: 1);
        Assert.Equal(4, m.VillagerFamiliarity["Pierre"]);
        var mem = m.VillagerMemory["Pierre"];
        Assert.Equal(1, mem.Talks);
        Assert.Equal(1, mem.Gifts);
        Assert.Equal(new List<int> { 1 }, mem.Loops);
        Assert.Equal(new List<int> { 1 }, mem.GiftLoops);
    }

    [Fact]
    public void Birthday_gift_records_the_loop_once()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 2);
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 2);
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 3);
        var mem = m.VillagerMemory["Pierre"];
        Assert.Equal(3, mem.BirthdayGifts);
        Assert.Equal(new List<int> { 2, 3 }, mem.BirthdayGiftLoops);
    }

    [Fact]
    public void Heart_events_track_loops_per_event_id()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Abigail", hearts: 1, heartIds: "4") }, 1);
        FamiliarityRollup.Apply(m, new[] { Day("Abigail", hearts: 1, heartIds: "4") }, 3);
        Assert.Equal(new List<int> { 1, 3 }, m.VillagerMemory["Abigail"].HeartEventLoops["4"]);
        Assert.Equal(2, m.VillagerMemory["Abigail"].HeartEvents);
    }

    [Fact]
    public void A_silent_day_writes_nothing()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre") }, 1);
        Assert.False(m.VillagerMemory.ContainsKey("Pierre"));
        Assert.False(m.VillagerFamiliarity.ContainsKey("Pierre"));
    }
}
