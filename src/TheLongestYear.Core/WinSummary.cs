using System.Collections.Generic;

namespace TheLongestYear.Core
{
    /// <summary>Single source of truth for the win payoff's loop-count line, used by the Year One
    /// Ending's continuation prompt (<c>dialog.ending.prompt</c>). The call site supplies its own
    /// surrounding lead-in text; this is just the one-loop-vs-many sentence.</summary>
    public static class WinSummary
    {
        /// <summary><paramref name="runNumber"/> is the attempt counter (incremented only on a full
        /// loop reset), so it reads as "which loop you won on". Loop 1 (or any non-positive defensive
        /// value) reads as a first-loop brag; later loops count the attempts.</summary>
        public static string LoopLine(int runNumber) =>
            runNumber <= 1
                ? Strings.Get("win.loop-line.first")
                : Strings.Get("win.loop-line.many", new Dictionary<string, string> { ["count"] = runNumber.ToString() });
    }
}
