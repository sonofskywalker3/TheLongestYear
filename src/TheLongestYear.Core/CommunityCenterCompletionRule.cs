namespace TheLongestYear.Core;

/// <summary>What the patched <c>Farmer.hasCompletedCommunityCenter()</c> should answer.</summary>
public enum CcCompletionAnswer
{
    /// <summary>Let vanilla's mail-based check answer.</summary>
    Vanilla,
    /// <summary>Force "not complete" (rooms still open on the live board).</summary>
    No,
    /// <summary>Force "complete" (the hall itself is restored).</summary>
    Yes,
}

/// <summary>
/// The rule behind the <c>hasCompletedCommunityCenter</c> patch. Vanilla's answer is pure mail,
/// and Gifts of the Junimos restore room mails at reset, so with the loop active the board is
/// consulted first: any open room fails closed. Once the hall itself is restored (every vanilla
/// area flag set by the restoration cutscenes, or the goodbye-dance <c>ccIsComplete</c> mail) the
/// hall is the authority and the answer is yes regardless of the board walk, because a stray or
/// stale bundle entry could otherwise read a finished hall as incomplete and silently block
/// everything downstream of vanilla's completion query: Willy's back-room letter, Robin's
/// community upgrade, the town's post-completion state (Nexus bug 1130863).
/// </summary>
public static class CommunityCenterCompletionRule
{
    public static CcCompletionAnswer Decide(bool runActive, bool allAreasRestored, bool ccIsCompleteMail, bool everyRoomDoneOnBoard)
    {
        if (!runActive) return CcCompletionAnswer.Vanilla;
        if (allAreasRestored || ccIsCompleteMail) return CcCompletionAnswer.Yes;
        return everyRoomDoneOnBoard ? CcCompletionAnswer.Vanilla : CcCompletionAnswer.No;
    }
}
