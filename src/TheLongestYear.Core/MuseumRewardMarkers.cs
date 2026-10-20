using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Event ids that a museum reward marks seen through a <c>MarkEventSeen</c> reward action
/// (Data/MuseumRewards RewardActions). They are reward bookkeeping, not scenes: vanilla's
/// 60-donation reward marks "295672" seen, and Gunther's Farm visit (event 66, key
/// "66/e 295672/t 600 700/H") hands over the Rusty Key once that id is seen. The rewind empties the
/// museum and wipes the rewards' mail flags, so the reward pays out again at 60 donations; its
/// marker has to rewind with it. Re-seeding it from the cross-loop seen memory let event 66 play on
/// the first morning of the next loop and hand the key back for free (sarahwinchester97, 0.19.97).
/// </summary>
public static class MuseumRewardMarkers
{
    public const string MarkEventSeenAction = "MarkEventSeen";

    /// <summary>Index of the event id in "MarkEventSeen &lt;player&gt; &lt;eventId&gt; [seen]".</summary>
    private const int EventIdArg = 2;

    /// <summary>Index of the optional "seen" bool; "false" un-marks the event instead.</summary>
    private const int SeenArg = 3;

    /// <summary>Every event id the given reward action lists mark seen. Null lists and malformed
    /// actions are skipped; an action whose optional fourth argument is "false" (an un-mark) is not
    /// a marker.</summary>
    public static HashSet<string> EventIds(IEnumerable<IEnumerable<string>?>? rewardActionLists)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (rewardActionLists == null)
            return ids;
        foreach (IEnumerable<string>? actions in rewardActionLists)
        {
            if (actions == null)
                continue;
            foreach (string action in actions)
            {
                string? id = MarkedEventId(action);
                if (id != null)
                    ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>The event id one action marks seen, or null if it is not a MarkEventSeen. Arguments
    /// split like vanilla's (<see cref="ActionArgs.SplitBySpaceQuoteAware"/>), so a quoted id keeps
    /// its spaces. As in TriggerActionManager.MarkEventSeen, a blank id is invalid and the optional
    /// "seen" argument must be a bool: "false" un-marks, anything unparsable makes the action fail.</summary>
    public static string? MarkedEventId(string? action)
    {
        string[] args = ActionArgs.SplitBySpaceQuoteAware(action);
        if (args.Length <= EventIdArg
            || !string.Equals(args[0], MarkEventSeenAction, StringComparison.OrdinalIgnoreCase))
            return null;
        if (args.Length > SeenArg && args[SeenArg].Length > 0
            && (!bool.TryParse(args[SeenArg], out bool seen) || !seen))
            return null;
        string id = args[EventIdArg];
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
