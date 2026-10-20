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
/// Rule: clear everything except Better Start's gift when the player turned "Re-send Better Start
/// gift each loop" off. Until 0.19.124 rows conditioned on <c>PLAYER_MONEY_EARNED</c> (the
/// Mom/Dad/Tribune money mails) were kept too, because <c>totalMoneyEarned</c> was a lifetime stat
/// and clearing them re-sent every earned tier on day 1. The rewind now zeroes money earned (Jeff,
/// 2026-10-20: it counts toward Perfection), so those mails are cleared like any other and arrive
/// again when this year's earnings reach their threshold.
/// </para>
/// </summary>
public static class TriggerActionResetRules
{
    /// <summary>Better Start's unique mod id (Nexus 32131); its trigger action ids carry this prefix.</summary>
    public const string BetterStartIdPrefix = "nebulouscharlotte.betterstart";

    /// <summary>True if the recorded action <paramref name="id"/> must stay marked as run.</summary>
    /// <param name="id">The trigger action id as recorded in <c>triggerActionsRun</c>.</param>
    /// <param name="resendBetterStartGift">The config toggle; false keeps Better Start's record.</param>
    public static bool ShouldKeep(string id, bool resendBetterStartGift)
        => !resendBetterStartGift && id.StartsWith(BetterStartIdPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The ids to remove from the record.</summary>
    /// <param name="recorded">Every id currently in <c>triggerActionsRun</c>.</param>
    /// <param name="resendBetterStartGift">The config toggle; false keeps Better Start's record.</param>
    public static List<string> IdsToClear(IEnumerable<string> recorded, bool resendBetterStartGift)
    {
        var clear = new List<string>();
        foreach (string id in recorded)
        {
            if (!ShouldKeep(id, resendBetterStartGift))
                clear.Add(id);
        }
        return clear;
    }
}
