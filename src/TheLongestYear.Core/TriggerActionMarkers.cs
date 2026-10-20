using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Event ids that a Data/TriggerActions row marks seen through a <c>MarkEventSeen</c> action, the
/// same leak <see cref="MuseumRewardMarkers"/> closes for museum rewards. The rewind clears the
/// trigger-action record (<see cref="TriggerActionResetRules"/>), so these rows fire again when their
/// condition holds; if the marker were re-seeded from the cross-loop seen memory, anything gated on
/// it (a mod's gift scene, say) would play early next loop. Vanilla's own rows mark only legacy ids
/// (2111194, 2111294, 3912126 to 3912131) that nothing gates on today.
/// </summary>
public static class TriggerActionMarkers
{
    /// <summary>One Data/TriggerActions row: its id plus its <c>Action</c> and <c>Actions</c> fields
    /// (vanilla runs both).</summary>
    public readonly record struct Row(string? Id, string? Action, IEnumerable<string>? Actions);

    /// <summary>Every event id the rows mark seen. A row whose record the rewind keeps
    /// (<see cref="TriggerActionResetRules.ShouldKeep"/>) never fires again, so its markers must stay
    /// re-seeded and are left out.</summary>
    public static HashSet<string> EventIds(IEnumerable<Row>? rows, bool resendBetterStartGift)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (rows == null)
            return ids;
        foreach (Row row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || TriggerActionResetRules.ShouldKeep(row.Id, resendBetterStartGift))
                continue;
            if (MuseumRewardMarkers.MarkedEventId(row.Action) is string single)
                ids.Add(single);
            if (row.Actions == null)
                continue;
            foreach (string action in row.Actions)
                if (MuseumRewardMarkers.MarkedEventId(action) is string id)
                    ids.Add(id);
        }
        return ids;
    }
}
