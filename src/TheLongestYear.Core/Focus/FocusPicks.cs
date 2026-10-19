using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The pick flow over a run's dealt hands. A pick is final: there is no un-pick.</summary>
public static class FocusPicks
{
    public static bool NeedsDeal(RunState run) => run.FocusDealtRun != run.RunNumber;

    /// <summary>The first hand in theme order that has cards and no pick yet.</summary>
    public static FocusHand? NextToPick(RunState run)
        => run.FocusHands.OrderBy(h => h.Theme).FirstOrDefault(h => h.Chains.Count > 0 && h.Chosen == null);

    public static bool Pending(RunState run) => NeedsDeal(run) || NextToPick(run) != null;

    public static bool Choose(RunState run, FocusTheme theme, string chainId)
    {
        FocusHand? hand = run.FocusHandFor(theme);
        if (hand == null || hand.Chosen != null || !hand.Chains.Contains(chainId)) return false;
        hand.Chosen = chainId;
        return true;
    }

    public static void Install(RunState run, List<FocusHand> hands)
    {
        run.FocusHands = hands;
        run.FocusDealtRun = run.RunNumber;
    }
}
