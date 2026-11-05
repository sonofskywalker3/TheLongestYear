using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Event ids that grant a power: a Data/Powers row whose UnlockedCondition is
/// <c>PLAYER_HAS_SEEN_EVENT &lt;who&gt; &lt;id&gt;</c>. The scene itself is the unlock, so no command in
/// its script marks it for the unlock-grant scan, and re-seeding it as seen carried the power into
/// every later loop for free (Stardew Valley Expanded's masteries). Vanilla's two (Bear's Knowledge,
/// Spring Onion Mastery) are also named in <see cref="EventGatingTables.Default"/>.
/// </summary>
public static class PowerEventMarkers
{
    public const string SeenEventQuery = "PLAYER_HAS_SEEN_EVENT";

    /// <summary>Index of the first event id in "PLAYER_HAS_SEEN_EVENT &lt;who&gt; &lt;id&gt;...".</summary>
    private const int FirstEventIdArg = 2;

    /// <summary>Every event id the given unlock conditions read. A negated query is not a grant.</summary>
    public static HashSet<string> EventIds(IEnumerable<string?>? unlockedConditions)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (unlockedConditions == null)
            return ids;
        foreach (string? condition in unlockedConditions)
        {
            if (string.IsNullOrWhiteSpace(condition))
                continue;
            foreach (string query in condition.Split(','))
            {
                string[] args = ActionArgs.SplitBySpaceQuoteAware(query.Trim());
                if (args.Length <= FirstEventIdArg
                    || !string.Equals(args[0], SeenEventQuery, StringComparison.OrdinalIgnoreCase))
                    continue;
                for (int i = FirstEventIdArg; i < args.Length; i++)
                    if (!string.IsNullOrWhiteSpace(args[i]))
                        ids.Add(args[i]);
            }
        }
        return ids;
    }
}
