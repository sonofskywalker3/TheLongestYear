using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Jeff, 2026-09-15, after a Nexus report of a player ejected from the Festival of Ice mid ice
/// fishing contest and left stuck on the fishing rod: "time should only be passing while they're
/// just wandering around, not in the middle of a dance or minigame of any kind."
/// </summary>
public class FestivalPhaseTests
{
    [Fact]
    public void Wandering_the_grounds_is_free_roam()
    {
        Assert.True(FestivalPhase.IsFreeRoam(festivalTimerMs: 0, playerControlSequence: true));
    }

    [Theory]
    [InlineData(120000)] // ice fishing contest just started
    [InlineData(52000)]  // egg hunt just started
    [InlineData(1)]      // last millisecond of either
    public void A_running_contest_timer_is_not_free_roam(int timerMs)
    {
        Assert.False(FestivalPhase.IsFreeRoam(timerMs, playerControlSequence: true));
    }

    [Fact]
    public void A_scripted_sequence_is_not_free_roam()
    {
        // Flower Dance, Luau soup, grange judging: the game holds the controls.
        Assert.False(FestivalPhase.IsFreeRoam(festivalTimerMs: 0, playerControlSequence: false));
    }

    [Fact]
    public void An_expired_timer_during_the_after_sequence_is_still_not_free_roam()
    {
        // The timer hit zero but the "afterIceFishing" script is still playing.
        Assert.False(FestivalPhase.IsFreeRoam(festivalTimerMs: 0, playerControlSequence: false));
    }
}
