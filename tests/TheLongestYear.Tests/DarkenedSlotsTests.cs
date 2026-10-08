using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: a slot the darkness emptied is marked purple until it is filled.</summary>
public class DarkenedSlotsTests
{
    [Fact]
    public void An_emptied_slot_is_marked_once()
    {
        var marks = new List<DonatedSlot>();
        Assert.True(DarkenedSlots.Mark(marks, 3, 1, "(O)24"));
        Assert.False(DarkenedSlots.Mark(marks, 3, 1, "(O)24"));
        Assert.Single(marks);
        Assert.True(DarkenedSlots.IsMarked(marks, 3, 1));
        Assert.False(DarkenedSlots.IsMarked(marks, 3, 2));
        Assert.False(DarkenedSlots.IsMarked(marks, 4, 1));
    }

    [Fact]
    public void The_mark_shows_only_while_the_slot_is_empty()
    {
        var marks = new List<DonatedSlot>();
        DarkenedSlots.Mark(marks, 3, 1, "(O)24");
        Assert.True(DarkenedSlots.Shows(marks, 3, 1, slotFilled: false));
        Assert.False(DarkenedSlots.Shows(marks, 3, 1, slotFilled: true));
        Assert.False(DarkenedSlots.Shows(null, 3, 1, slotFilled: false));
    }

    [Fact]
    public void Filling_a_slot_clears_its_mark_and_no_other()
    {
        var marks = new List<DonatedSlot>();
        DarkenedSlots.Mark(marks, 3, 1, "(O)24");
        DarkenedSlots.Mark(marks, 5, 0, "(O)340");
        Assert.Equal(1, DarkenedSlots.ClearFilled(marks, (b, i) => b == 3 && i == 1));
        Assert.False(DarkenedSlots.IsMarked(marks, 3, 1));
        Assert.True(DarkenedSlots.IsMarked(marks, 5, 0));
    }

    [Fact]
    public void Marks_clear_at_the_loop_reset()
    {
        var run = new RunState();
        DarkenedSlots.Mark(run.DarkenedSlots, 3, 1, "(O)24");
        run.BeginNewRun(1);
        Assert.Empty(run.DarkenedSlots);
    }

    [Fact]
    public void Marks_round_trip_with_the_run()
    {
        var run = new RunState();
        DarkenedSlots.Mark(run.DarkenedSlots, 3, 1, "(O)24");
        var back = System.Text.Json.JsonSerializer.Deserialize<RunState>(System.Text.Json.JsonSerializer.Serialize(run));
        Assert.True(DarkenedSlots.IsMarked(back.DarkenedSlots, 3, 1));
    }
}
