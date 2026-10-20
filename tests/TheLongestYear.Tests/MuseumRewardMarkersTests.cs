using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class MuseumRewardMarkersTests
{
    [Fact]
    public void Vanilla_sixty_donation_reward_marks_the_rusty_key_trigger()
    {
        var ids = MuseumRewardMarkers.EventIds(new List<IEnumerable<string>?>
        {
            new[] { "MarkEventSeen Host 295672" },
            null,
            new[] { "AddMail Current SomeFlag" },
        });
        Assert.Equal(new HashSet<string> { "295672" }, ids);
    }

    [Theory]
    [InlineData("MarkEventSeen Current 123", "123")]
    [InlineData("markeventseen All \"456\" true", "456")]
    [InlineData("MarkEventSeen Host 789 false", null)]
    [InlineData("MarkEventSeen Host 789 False", null)]
    [InlineData("MarkEventSeen Host 789 maybe", null)]
    [InlineData("MarkEventSeen Host 789 \"\"", "789")]
    [InlineData("MarkEventSeen Host \"Mod Gift Ready\" true", "Mod Gift Ready")]
    [InlineData("MarkEventSeen Host \" \"", null)]
    [InlineData("MarkEventSeen Host", null)]
    [InlineData("AddMail Host 295672", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Parses_one_action(string? action, string? expected)
        => Assert.Equal(expected, MuseumRewardMarkers.MarkedEventId(action));

    [Fact]
    public void Marker_makes_its_gated_scene_replay_too()
    {
        // Gunther's Farm visit is gated on the marker; with the marker not re-seeded, the chain
        // pass keeps the scene replayable as well, so the key only comes back at 60 donations.
        var flagged = MuseumRewardMarkers.EventIds(new[] { new[] { "MarkEventSeen Host 295672" } });
        EventGatingTables.PropagateChains(
            new[] { ("66", "66/e 295672/t 600 700/H") }, flagged, chainRoots: null, exclude: null);
        Assert.Contains("295672", flagged);
        Assert.Contains("66", flagged);
    }
}
