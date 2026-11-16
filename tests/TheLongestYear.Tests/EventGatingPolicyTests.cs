using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class EventGatingPolicyTests
{
    private const int Spring = 0, Summer = 1;

    private static EventGatingTables Tables() => new EventGatingTables(
        replayable: new[] { "CAVE", "FURNACE" },
        holdUntilSpring5: new[] { "CAVE", "EARLY" },
        furnace: new[] { "FURNACE" },
        holdThresholdDay: 5);

    [Theory]
    [InlineData(Spring, 1)]
    [InlineData(Spring, 4)]
    public void Held_event_is_suppressed_before_spring_5(int season, int day)
    {
        var d = EventGatingPolicy.Decide("EARLY", season, day, furnaceKnownThisRun: false, Tables());
        Assert.Equal(EventGatingDecision.Suppress, d);
    }

    [Theory]
    [InlineData(Spring, 5)]
    [InlineData(Spring, 12)]
    [InlineData(Summer, 1)] // past Spring 5 entirely → not held
    public void Held_event_is_allowed_on_or_after_spring_5(int season, int day)
    {
        var d = EventGatingPolicy.Decide("EARLY", season, day, furnaceKnownThisRun: false, Tables());
        Assert.Equal(EventGatingDecision.Allow, d);
    }

    [Fact]
    public void Furnace_teach_is_suppressed_when_recipe_known_this_run()
    {
        // Late enough that the hold rule doesn't apply (FURNACE isn't a held event anyway).
        var d = EventGatingPolicy.Decide("FURNACE", Summer, 10, furnaceKnownThisRun: true, Tables());
        Assert.Equal(EventGatingDecision.Suppress, d);
    }

    [Fact]
    public void Furnace_teach_is_allowed_when_recipe_not_known_this_run()
    {
        var d = EventGatingPolicy.Decide("FURNACE", Summer, 10, furnaceKnownThisRun: false, Tables());
        Assert.Equal(EventGatingDecision.Allow, d);
    }

    [Fact]
    public void Unlisted_event_passes_through_to_vanilla()
    {
        var d = EventGatingPolicy.Decide("999999", Spring, 1, furnaceKnownThisRun: true, Tables());
        Assert.Equal(EventGatingDecision.Allow, d);
    }

    [Fact]
    public void Empty_event_id_is_allowed()
    {
        var d = EventGatingPolicy.Decide("", Spring, 1, furnaceKnownThisRun: false, Tables());
        Assert.Equal(EventGatingDecision.Allow, d);
    }

    [Fact]
    public void Replayable_ids_are_exposed_for_the_reset_reseed_exclusion()
    {
        Assert.Contains("CAVE", Tables().ReplayableEventIds);
        Assert.Contains("FURNACE", Tables().ReplayableEventIds);
        Assert.DoesNotContain("EARLY", Tables().ReplayableEventIds);
    }

    // An event the day change dropped (East Scarp's inn after passing out): the morning clears what it left.
    [Theory]
    [InlineData(true, true, true, false)]    // flag still up, HUD hidden: lower the flag, show the HUD
    [InlineData(false, true, false, false)]  // the game already lowered the flag: the HUD is still hidden
    [InlineData(true, false, true, true)]    // HUD already back
    public void A_dropped_overnight_event_is_cleared_in_the_morning(bool flagUp, bool expectHud, bool expectFlag, bool hudShown)
    {
        DroppedEventRepair repair = DroppedEventRule.Decide(startedAsDayEnded: true, eventNow: false, busy: false, eventFlagUp: flagUp, hudShown: hudShown);
        Assert.Equal(expectFlag, repair.LowerEventFlag);
        Assert.Equal(expectHud, repair.ShowHud);
    }

    [Theory]
    [InlineData(false, false, false)]  // no event ran as the day ended: an event flag this morning is someone else's
    [InlineData(true, true, false)]    // an event is running this morning
    [InlineData(true, false, true)]    // a farm event or a map change is in progress
    public void A_morning_with_no_dropped_event_is_left_alone(bool startedAsDayEnded, bool eventNow, bool busy)
        => Assert.False(DroppedEventRule.Decide(startedAsDayEnded, eventNow, busy, eventFlagUp: true, hudShown: false).Any);
}
