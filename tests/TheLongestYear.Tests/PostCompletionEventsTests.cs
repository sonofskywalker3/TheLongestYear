using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Guards the rule that the ending's current-run hand-off id never survives a loop
/// (spec 2026-09-06 section 3). <c>FarmerReset.Apply</c> and <c>ModEntry.RecordSeenEvents</c> both
/// take a live <c>Farmer</c> / <c>MetaStore</c>, so neither has a pure seam to drive from a test;
/// what is testable is the helper both of them filter through, plus the shape of the filter itself.
/// </summary>
public class PostCompletionEventsTests
{
    [Fact]
    public void The_ceremony_id_is_the_vanilla_completion_event()
        => Assert.Equal("191393", PostCompletionEvents.CeremonyEventId);

    [Theory]
    [InlineData("191393", true)]
    [InlineData("60367", false)]    // vanilla intro, deliberately re-seeded every loop
    [InlineData("191394", false)]   // near miss: no prefix matching
    [InlineData("19139", false)]
    [InlineData("", false)]
    public void IsHandedOffOnly_matches_the_ceremony_id_exactly(string id, bool expected)
        => Assert.Equal(expected, PostCompletionEvents.IsHandedOffOnly(id));

    /// <summary>The shape of both call sites: a memory/re-seed list filtered through the helper keeps
    /// every ordinary id and drops the hand-off one.</summary>
    [Fact]
    public void Filtering_a_seen_list_drops_only_the_hand_off_id()
    {
        var seen = new List<string> { "12", "191393", "60367", "191393" };

        List<string> kept = seen.Where(id => !PostCompletionEvents.IsHandedOffOnly(id)).ToList();

        Assert.Equal(new[] { "12", "60367" }, kept);
    }
}
