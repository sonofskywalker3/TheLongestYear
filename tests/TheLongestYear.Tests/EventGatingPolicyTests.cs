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
}
