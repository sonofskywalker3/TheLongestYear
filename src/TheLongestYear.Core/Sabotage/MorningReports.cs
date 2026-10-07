using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Which of the night's reports show now (final review M1, Jeff 2026-10-07). A tamper
/// report waits for the Junimos' scene at the farmhouse door while that scene can tell it; every
/// other report shows on waking, as it always has.</summary>
public static class MorningReports
{
    /// <summary>Take the reports that show now out of <paramref name="reports"/> and return them in
    /// order. With <paramref name="tampersWait"/>, tamper reports stay behind for their scene.</summary>
    public static List<SabotageReport> TakeShownNow(List<SabotageReport> reports, bool tampersWait)
    {
        if (reports is null) throw new ArgumentNullException(nameof(reports));
        var shown = new List<SabotageReport>(reports.Count);
        foreach (SabotageReport report in reports)
            if (!(tampersWait && report.Kind == SabotageKind.Tampering))
                shown.Add(report);
        reports.RemoveAll(r => shown.Contains(r));
        return shown;
    }
}
