using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingMorningDeciderTests
{
    private static EndingSnapshot S(bool armed = true, bool ready = true, bool onFarm = true, bool busy = false,
        bool festival = false, bool seen = false, bool started = false)
        => new(armed, ready, onFarm, busy, festival, seen, started);

    [Fact] public void Not_armed_does_nothing() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(armed: false)));
    [Fact] public void Waits_for_the_farm() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(onFarm: false)));
    [Fact] public void Waits_while_busy() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(busy: true)));
    [Fact] public void Never_on_a_festival() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(festival: true)));
    [Fact] public void Starts_when_clear() => Assert.Equal(EndingAction.Start, EndingMorningDecider.Next(S()));
    [Fact] public void Finishes_when_seen_mail_lands_and_event_is_over()
        => Assert.Equal(EndingAction.Finish, EndingMorningDecider.Next(S(seen: true, started: true)));
    [Fact] public void Finishes_even_if_started_flag_was_lost_after_a_reload()
        => Assert.Equal(EndingAction.Finish, EndingMorningDecider.Next(S(seen: true, started: false)));
    [Fact] public void Rearms_when_the_event_ended_without_the_mail()
        => Assert.Equal(EndingAction.ReArm, EndingMorningDecider.Next(S(seen: false, started: true)));
    [Fact] public void Waits_while_event_runs()
        => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(busy: true, started: true)));
}
