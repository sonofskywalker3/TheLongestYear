using TheLongestYear.Core.Day28;
using Xunit;

namespace TheLongestYear.Tests;

public class Day28CutsceneDeciderTests
{
    private static Day28Action Next(Day28Branch branch, bool started, bool eventActive)
        => Day28CutsceneDecider.Next(new Day28Snapshot(branch, started, eventActive));

    [Fact]
    public void No_pending_branch_is_a_no_op()
    {
        Assert.Equal(Day28Action.None, Next(Day28Branch.None, started: false, eventActive: false));
        Assert.Equal(Day28Action.None, Next(Day28Branch.None, started: true, eventActive: true));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Pending_and_not_started_starts_the_cutscene(Day28Branch branch)
    {
        Assert.Equal(Day28Action.StartCutscene, Next(branch, started: false, eventActive: false));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Started_while_event_is_active_waits(Day28Branch branch)
    {
        Assert.Equal(Day28Action.Waiting, Next(branch, started: true, eventActive: true));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Started_and_event_ended_runs_the_continuation(Day28Branch branch)
    {
        Assert.Equal(Day28Action.RunContinuation, Next(branch, started: true, eventActive: false));
    }

    [Fact]
    public void An_active_event_before_start_still_waits_rather_than_double_starting()
    {
        // Defensive: some other event is up the instant we became pending.
        Assert.Equal(Day28Action.Waiting, Next(Day28Branch.Fail, started: false, eventActive: true));
    }
}
