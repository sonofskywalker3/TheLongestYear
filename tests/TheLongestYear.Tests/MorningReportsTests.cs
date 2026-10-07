using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Final review M1: a tamper report waits for the Junimos' scene at the farmhouse door;
/// every other report shows on waking, and showing them never drops a waiting tamper.</summary>
public class MorningReportsTests
{
    private static SabotageReport Of(SabotageKind kind) => new() { Kind = kind, Count = 1 };

    [Fact]
    public void Other_reports_show_on_waking_while_a_tamper_waits()
    {
        var blight = Of(SabotageKind.Blight);
        var tamper = Of(SabotageKind.Tampering);
        var reports = new List<SabotageReport> { tamper, blight };

        List<SabotageReport> shown = MorningReports.TakeShownNow(reports, tampersWait: true);

        Assert.Equal(new[] { blight }, shown);
        Assert.Equal(new[] { tamper }, reports);
    }

    [Fact]
    public void Two_waiting_tampers_both_stay()
    {
        var first = Of(SabotageKind.Tampering);
        var second = Of(SabotageKind.Tampering);
        var reports = new List<SabotageReport> { first, second };

        Assert.Empty(MorningReports.TakeShownNow(reports, tampersWait: true));
        Assert.Equal(new[] { first, second }, reports);
    }

    [Fact]
    public void With_no_scene_to_tell_it_a_tamper_shows_with_the_rest()
    {
        var reversion = Of(SabotageKind.Reversion);
        var tamper = Of(SabotageKind.Tampering);
        var reports = new List<SabotageReport> { reversion, tamper };

        List<SabotageReport> shown = MorningReports.TakeShownNow(reports, tampersWait: false);

        Assert.Equal(new[] { reversion, tamper }, shown);
        Assert.Empty(reports);
    }
}
