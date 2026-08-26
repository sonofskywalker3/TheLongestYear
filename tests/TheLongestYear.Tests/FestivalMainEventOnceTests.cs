using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Jeff, 2026-08-26, watching emmalution's stream: she ran the Egg Hunt three times on the SAME day.
///
/// Vanilla never needs a guard here because a festival ends the day: the clock jumps past the
/// festival's end time, so you can't walk back in. TLY deliberately removes that (FestivalTimeFlow
/// keeps the hours real), which leaves the festival re-entrant for the rest of its window: walk out,
/// walk back into Town, Lewis offers the hunt again, and the prize can be farmed. Once per day.
/// </summary>
public class FestivalMainEventOnceTests
{
    [Fact]
    public void Nothing_played_yet_means_not_played()
    {
        var run = new RunState();

        Assert.False(FestivalMainEvent.AlreadyPlayed(run, "spring13", totalDays: 12));
    }

    [Fact]
    public void A_stamped_festival_counts_as_played_that_day()
    {
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);

        Assert.True(FestivalMainEvent.AlreadyPlayed(run, "spring13", totalDays: 12));
    }

    [Fact]
    public void The_stamp_does_not_carry_to_the_next_day()
    {
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);

        Assert.False(FestivalMainEvent.AlreadyPlayed(run, "spring13", totalDays: 13));
    }

    [Fact]
    public void A_different_festival_on_the_same_day_is_unaffected()
    {
        // Belt and braces: two festivals never share a day in vanilla, but the stamp should key
        // on the festival as well as the day so a content mod that adds one cannot be blocked.
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);

        Assert.False(FestivalMainEvent.AlreadyPlayed(run, "spring24", totalDays: 12));
    }

    [Fact]
    public void A_later_festival_replaces_the_earlier_stamp()
    {
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);
        FestivalMainEvent.MarkPlayed(run, "summer11", totalDays: 39);

        Assert.True(FestivalMainEvent.AlreadyPlayed(run, "summer11", totalDays: 39));
        Assert.False(FestivalMainEvent.AlreadyPlayed(run, "spring13", totalDays: 12));
    }

    [Fact]
    public void A_null_or_empty_festival_id_is_never_considered_played()
    {
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);

        Assert.False(FestivalMainEvent.AlreadyPlayed(run, null, totalDays: 12));
        Assert.False(FestivalMainEvent.AlreadyPlayed(run, "", totalDays: 12));
    }

    [Fact]
    public void A_null_run_never_throws()
    {
        Assert.False(FestivalMainEvent.AlreadyPlayed(null, "spring13", totalDays: 12));
        FestivalMainEvent.MarkPlayed(null, "spring13", totalDays: 12);   // no throw
    }

    [Fact]
    public void Marking_twice_on_the_same_day_stays_played()
    {
        var run = new RunState();
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);
        FestivalMainEvent.MarkPlayed(run, "spring13", totalDays: 12);

        Assert.True(FestivalMainEvent.AlreadyPlayed(run, "spring13", totalDays: 12));
    }
}
