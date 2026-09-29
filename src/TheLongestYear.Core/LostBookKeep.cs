using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Keep Lost Books (Jeff, 2026-09-29; tanky24u asked on Nexus, 2026-09-24): with it, a
/// rewind leaves the found count (NetWorldState.LostBooksFound) and each book's read marker alone,
/// so artifact spots, fishing chests and the mines stop handing out books already found. Vanilla
/// only drops a Lost Book while the count is under 21, and the next one found is the next number.</summary>
public static class LostBookKeep
{
    public const string UpgradeId = "keep_lost_books";
    private const string ReadMarkerPrefix = "lb_";
    private const string FoundFlag = "lostBookFound";

    /// <summary>The mail flags a kept library needs: every "lb_&lt;n&gt;" read marker plus the
    /// "lostBookFound" flag that lets books drop at all.</summary>
    public static IReadOnlyList<string> MailToKeep(IEnumerable<string> mail)
        => (mail ?? Array.Empty<string>())
            .Where(m => m == FoundFlag
                        || (m.StartsWith(ReadMarkerPrefix, StringComparison.Ordinal) && m.Length > ReadMarkerPrefix.Length))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
