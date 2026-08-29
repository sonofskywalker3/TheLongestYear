using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Which entries of the farmer's <c>triggerActionsRun</c> record survive a loop reset.
/// Vanilla records every fired <c>Data/TriggerActions</c> row there (MarkActionApplied defaults
/// true) and never re-fires it. The reset wipes hearts, mail and events but, until 0.16.154,
/// never this record, so the twelve heart-gated invite mails (Abigail 8, Penny 10, the Elliott
/// letters, ...) fired once per SAVE and the 8 and 10 heart events were unreachable in loop 2+.
/// Mod gifts keyed on DayStarted (Better Start's Robin letter) went the same way.
/// <para>
/// Rule: clear everything except (1) rows conditioned on <c>PLAYER_MONEY_EARNED</c>, because
/// <c>totalMoneyEarned</c> is a lifetime stat the mod never rewinds and clearing them would
/// re-send every earned Mom/Dad/Tribune tier on day 1 of each loop, and (2) Better Start's gift
/// when the player turned "Re-send Better Start gift each loop" off. A recorded id with no data
/// row (a removed mod) is cleared too.
/// </para>
/// </summary>
public static class TriggerActionResetRules
{
    /// <summary>Game-state query that only makes sense against a lifetime counter.</summary>
    public const string LifetimeMoneyQuery = "PLAYER_MONEY_EARNED";

    /// <summary>Better Start's unique mod id (Nexus 32131); its trigger action ids carry this prefix.</summary>
    public const string BetterStartIdPrefix = "nebulouscharlotte.betterstart";

    /// <summary>True if the recorded action <paramref name="id"/> must stay marked as run.</summary>
    /// <param name="id">The trigger action id as recorded in <c>triggerActionsRun</c>.</param>
    /// <param name="condition">The row's Condition from Data/TriggerActions; null when the id has
    /// no row any more.</param>
    /// <param name="resendBetterStartGift">The config toggle; false keeps Better Start's record.</param>
    public static bool ShouldKeep(string id, string? condition, bool resendBetterStartGift)
    {
        if (condition != null && condition.Contains(LifetimeMoneyQuery, StringComparison.Ordinal))
            return true;
        if (!resendBetterStartGift && id.StartsWith(BetterStartIdPrefix, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>The ids to remove from the record.</summary>
    /// <param name="recorded">Every id currently in <c>triggerActionsRun</c>.</param>
    /// <param name="conditionById">Condition per Data/TriggerActions row id (null condition = unconditional).</param>
    public static List<string> IdsToClear(
        IEnumerable<string> recorded,
        IReadOnlyDictionary<string, string?> conditionById,
        bool resendBetterStartGift)
    {
        var clear = new List<string>();
        foreach (string id in recorded)
        {
            conditionById.TryGetValue(id, out string? condition);
            if (!ShouldKeep(id, condition, resendBetterStartGift))
                clear.Add(id);
        }
        return clear;
    }
}
