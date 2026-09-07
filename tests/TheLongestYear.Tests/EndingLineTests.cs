using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingLineTests
{
    private static VillagerMemory Mem(int[]? bday = null, int[]? gifts = null, (string id, int[] loops)[]? hearts = null)
    {
        var m = new VillagerMemory();
        if (bday != null) m.BirthdayGiftLoops.AddRange(bday);
        if (gifts != null) m.GiftLoops.AddRange(gifts);
        if (hearts != null) foreach (var (id, loops) in hearts) m.HeartEventLoops[id] = new List<int>(loops);
        return m;
    }

    [Fact]
    public void Birthday_in_two_loops_is_tier_one()
        => Assert.Equal(EndingLineTier.BirthdayGift, EndingLine.Tier(Mem(bday: new[] { 1, 2 }), out _));

    [Fact]
    public void Birthday_in_one_loop_does_not_count()
        => Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(bday: new[] { 2 }), out _));

    [Fact]
    public void Heart_event_needs_two_loops_and_a_scene_entry()
    {
        string knownId = EndingLine.SceneTable.Keys.First();
        Assert.Equal(EndingLineTier.HeartEvent, EndingLine.Tier(Mem(hearts: new[] { (knownId, new[] { 1, 3 }) }), out string? scene));
        Assert.Equal(knownId, scene);
        Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(hearts: new[] { ("999999", new[] { 1, 3 }) }), out _));
        Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(hearts: new[] { (knownId, new[] { 1 }) }), out _));
    }

    [Fact]
    public void Gifts_in_two_loops_is_tier_three()
        => Assert.Equal(EndingLineTier.Gifts, EndingLine.Tier(Mem(gifts: new[] { 1, 2 }), out _));

    [Fact]
    public void Score_only_save_is_tier_four()
        => Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(null, out _));

    [Fact]
    public void Keys_use_voice_override_when_present()
    {
        Assert.Equal("event.ending.crack.tier1.Shane", EndingLine.MiddleKey("Shane", EndingLineTier.BirthdayGift));
        Assert.Equal("event.ending.crack.tier1", EndingLine.MiddleKey("Pierre", EndingLineTier.BirthdayGift));
    }

    [Fact]
    public void Multiple_qualifying_heart_events_pick_lowest_id_numerically()
    {
        // "34" sorts before "6" lexicographically but not numerically. The rule is lowest id
        // first by numeric value, so with both qualifying the scene must be the one for id "6".
        Assert.Equal(EndingLineTier.HeartEvent, EndingLine.Tier(
            Mem(hearts: new[] { ("34", new[] { 1, 2 }), ("6", new[] { 1, 2 }) }), out string? scene));
        Assert.Equal("6", scene);
    }

    [Fact]
    public void SceneTable_ids_are_numeric_values_are_scene_keys_no_duplicates()
    {
        var seenValues = new HashSet<string>();
        foreach (var (id, key) in EndingLine.SceneTable)
        {
            Assert.True(int.TryParse(id, out _), $"scene id '{id}' does not parse as an int");
            Assert.StartsWith("event.ending.scene.", key);
            Assert.True(seenValues.Add(key), $"duplicate scene key '{key}'");
        }
    }
}
