using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class TriggerActionMarkersTests
{
    [Fact]
    public void Vanilla_legacy_marker_rows_are_collected_from_action_and_actions()
    {
        var ids = TriggerActionMarkers.EventIds(new[]
        {
            new TriggerActionMarkers.Row("Mail_Abigail_8heart", "AddMail Current abbySpiritBoard", null),
            new TriggerActionMarkers.Row("Legacy_A", "MarkEventSeen Current 2111194", null),
            new TriggerActionMarkers.Row("Legacy_B", null, new[] { "MarkEventSeen Current 3912126", "MarkEventSeen Current 3912127" }),
            new TriggerActionMarkers.Row("Both", "MarkEventSeen Current 1", new[] { "MarkEventSeen Current 2" }),
        }, resendBetterStartGift: true);
        Assert.Equal(new HashSet<string> { "2111194", "3912126", "3912127", "1", "2" }, ids);
    }

    [Fact]
    public void Mod_gift_marker_is_collected()
    {
        var ids = TriggerActionMarkers.EventIds(new[]
        {
            new TriggerActionMarkers.Row("Some.Mod_Gift", "MarkEventSeen Host \"Some.Mod_GiftReady\"", null),
        }, resendBetterStartGift: true);
        Assert.Equal(new HashSet<string> { "Some.Mod_GiftReady" }, ids);
    }

    [Fact]
    public void Rows_whose_record_survives_the_rewind_keep_their_marker()
    {
        var rows = new[]
        {
            new TriggerActionMarkers.Row(TriggerActionResetRules.BetterStartIdPrefix + "_RobinLetter", "MarkEventSeen Current 77", null),
        };
        Assert.Empty(TriggerActionMarkers.EventIds(rows, resendBetterStartGift: false));
        Assert.Equal(new HashSet<string> { "77" }, TriggerActionMarkers.EventIds(rows, resendBetterStartGift: true));
    }

    [Fact]
    public void Rows_without_an_id_and_null_input_are_skipped()
    {
        Assert.Empty(TriggerActionMarkers.EventIds(null, true));
        Assert.Empty(TriggerActionMarkers.EventIds(new[] { new TriggerActionMarkers.Row(null, "MarkEventSeen Current 5", null) }, true));
    }
}
